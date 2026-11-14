using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Creature;
using Husnisse.Flow;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Husnisse.LegsSmoke
{
    /// <summary>
    /// Throwaway smoke probe for 0.1.4. Builds a 20 m walkway, a bowl at one end and a chest at the
    /// other, drops coal near the bowl and watches the nisse walk it. Then walls off the middle with
    /// a 2 m doorway and drops more coal, to see the fade and the report instead of a stuck nisse.
    /// The mod does the work; this only builds the scene and reports what happened.
    /// </summary>
    [BepInPlugin(Guid, "Husnisse Legs Smoke", "0.1.0")]
    public class LegsSmokePlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.husnisse.legssmoke";

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Container _chest;
        private NisseActor _nisse;
        private Vector3 _origin = new Vector3(0f, 40f, 0f);

        private int _samples;
        private int _maxStepX10;
        private int _fades;

        private void Awake()
        {
            Logger.LogInfo("legssmoke: loaded");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var waited = 0f;
            while (waited < 180f)
            {
                if (ZNet.instance != null && ZNetScene.instance != null && Pathfinding.instance != null
                    && ZNet.instance.IsServer()) break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                Logger.LogWarning("legssmoke: not a server; nothing to smoke");
                yield break;
            }

            try
            {
                var harmony = new Harmony("husnisse.legssmoke.hold");
                harmony.Patch(typeof(ZNetScene).GetMethod("CreateDestroyObjects", Priv),
                    prefix: new HarmonyMethod(typeof(HoldPatch).GetMethod("Prefix")));
                Logger.LogInfo("legssmoke: culling suppressed");
            }
            catch (Exception e)
            {
                Logger.LogWarning("legssmoke: culling patch failed: " + e.Message);
            }

            if (HusnisseConfig.DebugLogging != null) HusnisseConfig.DebugLogging.Value = true;
            yield return new WaitForSeconds(5f);
            Logger.LogInfo("legssmoke: begin");

            // ---- the room: a 20 m floor strip, a bowl at one end, a chest at the other.
            var floors = BuildStrip(_origin, 0f, 21);
            Logger.LogInfo("legssmoke: floors=" + floors);

            _spawned.Add(Spawn("husnisse_bowl", _origin + Vector3.up * 0.1f, Quaternion.Euler(0f, 90f, 0f)));
            yield return new WaitForSeconds(2f);

            var chestGo = Spawn("piece_chest_wood", _origin + new Vector3(15f, 0.1f, 0f), Quaternion.identity);
            _spawned.Add(chestGo);
            _chest = chestGo != null ? chestGo.GetComponent<Container>() : null;
            Logger.LogInfo("legssmoke: chest=" + (_chest != null));

            yield return new WaitForSeconds(2f);
            _nisse = FindNisse();
            Logger.LogInfo("legssmoke: nisse=" + (_nisse != null) + " at " + (_nisse != null ? _nisse.transform.position.ToString("F1") : "-"));

            DropCoal(new Vector3(2f, 0.4f, 0f));
            DropCoal(new Vector3(3.5f, 0.4f, 0f));
            DropCoal(new Vector3(5f, 0.4f, 0f));

            // ---- walk: 15 m out and back, watch it happen.
            yield return Watch(70f, "walk");

            // ---- the enclosed room: walls on every side but a 2 m doorway facing the walkway.
            var walls = BuildRoom(_origin + new Vector3(14f, 0f, 0f));
            Logger.LogInfo("legssmoke: walls=" + walls);
            yield return new WaitForSeconds(3f);

            // Poke the tile until the walls are actually baked; the first plan in a stale tile is
            // the lie this whole test exists to avoid. The tile rebuilds at most once per interval.
            var open = 0;
            for (var i = 0; i < 7; i++)
            {
                open += ProbePath(new Vector3(1.5f, 40.1f, 0f), new Vector3(13.85f, 40.1f, 0f), "doorway" + i);
                yield return new WaitForSeconds(3f);
            }
            Logger.LogInfo("legssmoke: doorway_open_count=" + open);

            if (open > 0)
            {
                // A free-standing 2 m gap turned out to be walkable on this build. Close the doorway
                // so the fade path still gets its proof.
                var closed = CloseDoorway(_origin + new Vector3(12f, 0.1f, 0f));
                Logger.LogInfo("legssmoke: doorway_closed_pieces=" + closed);
                yield return new WaitForSeconds(3f);
                for (var i = 0; i < 7; i++)
                {
                    ProbePath(new Vector3(1.5f, 40.1f, 0f), new Vector3(13.85f, 40.1f, 0f), "closed" + i);
                    yield return new WaitForSeconds(3f);
                }
            }

            DropCoal(new Vector3(2f, 0.4f, 0.4f));
            DropCoal(new Vector3(5f, 0.4f, -0.4f));
            yield return Watch(80f, "doorway");

            Logger.LogInfo("legssmoke: summary samples=" + _samples + " max_step_x10=" + _maxStepX10 +
                           " fades=" + _fades + " chest_stacks=" + ChestStacks() +
                           " queued=" + Queued() + " trips=" + TripRunner.Count);

            foreach (var go in _spawned)
            {
                if (go) Destroy(go);
            }

            Logger.LogInfo("legssmoke: done");
        }

        private static class HoldPatch
        {
            public static bool Prefix()
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ watch

        private IEnumerator Watch(float seconds, string label)
        {
            var startStacks = ChestStacks();
            var start = _nisse != null ? _nisse.transform.position : Vector3.zero;
            var last = start;
            var elapsed = 0f;
            var reportedIdle = false;

            while (elapsed < seconds)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;

                if (_nisse == null) _nisse = FindNisse();
                if (_nisse == null) continue;

                var position = _nisse.transform.position;
                var step = Horizontal(last, position);
                var stepX10 = Mathf.RoundToInt(step * 10f);
                if (stepX10 > _maxStepX10) _maxStepX10 = stepX10;
                _samples++;

                var trip = ClaimRegistry.Count > 0 ? TripRunner.Get(ClaimRegistry.All()[0]) : null;
                var state = _nisse.GetComponent<NisseMotion>();
                if (state != null) _fades = state.Fades;
                Logger.LogInfo("legssmoke: " + label + " t=" + elapsed.ToString("0") +
                               " pos=" + position.ToString("F1") +
                               " step=" + step.ToString("0.00") +
                               " motion=" + (state != null ? state.State.ToString() : "-") +
                               " trip=" + (trip != null ? trip.ShortState() : "idle") +
                               " chest=" + ChestStacks() +
                               " queued=" + Queued());

                if (trip == null && ChestStacks() > startStacks && !reportedIdle)
                {
                    reportedIdle = true;
                    Logger.LogInfo("legssmoke: " + label + " delivered at t=" + elapsed.ToString("0"));
                }

                last = position;
            }

            Logger.LogInfo("legssmoke: " + label + " end chest=" + ChestStacks() + " start_chest=" + startStacks +
                           " moved_from=" + start.ToString("F1"));
        }

        // ------------------------------------------------------------------ scene

        private int BuildStrip(Vector3 origin, float y, int count)
        {
            var floor = ZNetScene.instance.GetPrefab("wood_floor");
            if (floor == null) floor = ZNetScene.instance.GetPrefab("wood_floor_1x1");
            if (floor == null) return 0;

            var built = 0;
            for (var i = 0; i < count; i++)
            {
                var go = Spawn(floor.name, origin + new Vector3(i * 1f, y, 0f), Quaternion.identity);
                if (go != null) built++;
            }
            return built;
        }

        private int BuildWall(Vector3 center)
        {
            var wall = ZNetScene.instance.GetPrefab("wood_wall");
            if (wall == null) wall = ZNetScene.instance.GetPrefab("wood_wall_half");
            if (wall == null) return 0;

            var built = 0;
            // Two pieces 2 m apart, turned across the walkway, leave a 2 m doorway between their
            // inner edges — the measured shape that navmesh will not bridge.
            foreach (var z in new[] { -2f, 2f })
            {
                var go = Spawn(wall.name, center + new Vector3(0f, 0f, z), Quaternion.Euler(0f, 90f, 0f));
                if (go != null) built++;
            }
            return built;
        }

        /// <summary>
        /// A 3x3 floor room with a wall on every side and one 2 m doorway in its west face: the
        /// exact shape the earlier spike measured, and the case a real base produces (a chest in a
        /// closed room). The chest at (15,40,0) needs reaching through that doorway.
        /// </summary>
        private int BuildRoom(Vector3 origin)
        {
            var wall = ZNetScene.instance.GetPrefab("wood_wall");
            if (wall == null) wall = ZNetScene.instance.GetPrefab("wood_wall_half");
            if (wall == null) return 0;

            var floors = BuildStrip(origin + new Vector3(-2f, 0f, 0f), 0f, 5);
            var floor = ZNetScene.instance.GetPrefab("wood_floor") != null ? "wood_floor" : "wood_floor_1x1";
            for (var x = -2; x <= 2; x += 2)
            {
                Spawn(floor, origin + new Vector3(x, 0f, -2f), Quaternion.identity);
                Spawn(floor, origin + new Vector3(x, 0f, 2f), Quaternion.identity);
            }
            Logger.LogInfo("legssmoke: room floors=" + floors);

            var y = 0.1f;
            var built = 0;

            // West face: pieces at z=-2 and z=+2 leave the 2 m doorway at z=0.
            built += WallAt(wall, origin + new Vector3(-2f, y, -2f), true);
            built += WallAt(wall, origin + new Vector3(-2f, y, 2f), true);

            // East face, closed.
            for (var z = -2; z <= 2; z += 2) built += WallAt(wall, origin + new Vector3(2f, y, z), true);

            // North and south faces, closed.
            for (var x = -2; x <= 2; x += 2)
            {
                built += WallAt(wall, origin + new Vector3(x, y, 2f), false);
                built += WallAt(wall, origin + new Vector3(x, y, -2f), false);
            }

            return built;
        }

        private int WallAt(GameObject wall, Vector3 position, bool alongZ)
        {
            var go = Spawn(wall.name, position, alongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            return go != null ? 1 : 0;
        }

        /// <summary>Fills the 2 m doorway so the room has no opening at all.</summary>
        private int CloseDoorway(Vector3 westFace)
        {
            var wall = ZNetScene.instance.GetPrefab("wood_wall");
            if (wall == null) wall = ZNetScene.instance.GetPrefab("wood_wall_half");
            return wall == null ? 0 : WallAt(wall, westFace, true);
        }

        /// <summary>Asks for a full path and says what came back, after poking the tile.</summary>
        private int ProbePath(Vector3 from, Vector3 to, string label)
        {
            var full = new List<Vector3>();
            var ok = Pathfinding.instance.GetPath(from, to, full, Pathfinding.AgentType.Humanoid,
                requireFullPath: true, cleanup: true);
            var end = full.Count > 0 ? full[full.Count - 1] : Vector3.zero;
            Logger.LogInfo("legssmoke: bake_probe " + label + " full=" + ok + " corners=" + full.Count +
                           " end_h=" + Horizontal(end, to).ToString("0.00"));
            return ok ? 1 : 0;
        }

        private GameObject Spawn(string prefabName, Vector3 pos, Quaternion rot)
        {
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Logger.LogWarning("legssmoke: no prefab " + prefabName);
                return null;
            }

            var hash = prefabName.GetStableHashCode();
            var zdo = ZDOMan.instance.CreateNewZDO(pos, hash);
            zdo.SetPrefab(hash);

            var go = typeof(ZNetScene).GetMethod("CreateObject", Priv)
                ?.Invoke(ZNetScene.instance, new object[] { zdo }) as GameObject;
            if (go == null)
            {
                Logger.LogWarning("legssmoke: CreateObject null for " + prefabName);
                return null;
            }

            go.transform.rotation = rot;
            var wear = go.GetComponent<WearNTear>();
            if (wear) Destroy(wear);
            return go;
        }

        private void DropCoal(Vector3 offset)
        {
            var go = Spawn("Coal", _origin + offset, Quaternion.identity);
            if (go != null) _spawned.Add(go);
        }

        private NisseActor FindNisse()
        {
            if (_nisse != null) return _nisse;
            var all = Object.FindObjectsByType<NisseActor>(FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>How many coal items are in the chest, not how many stacks.</summary>
        private int ChestStacks()
        {
            var inventory = _chest != null ? _chest.GetInventory() : null;
            return inventory != null ? inventory.CountItems("$item_coal") : -1;
        }

        private int Queued()
        {
            return ClaimRegistry.Count > 0 ? FlowQueue.Count(ClaimRegistry.All()[0]) : -1;
        }

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
