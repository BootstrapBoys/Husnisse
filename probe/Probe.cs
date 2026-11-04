using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace NisseProbe
{
    /// <summary>
    /// Throwaway probe answering two questions Nisse's design depends on:
    ///   1. Where does a chest's contents live, and is it readable without opening the chest?
    ///   2. Does the runtime navmesh cover a player-built floor, and can GetPath reach a point inside it?
    /// Logs only. Never mutates anything outside the test world.
    /// </summary>
    [BepInPlugin(Guid, "Nisse Probe", "0.1.0")]
    public class ProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.nisseprobe";

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Awake()
        {
            Logger.LogInfo("probe: loaded");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var waited = 0f;
            while (waited < 300f)
            {
                if (ZNet.instance != null && ZNetScene.instance != null
                    && ZoneSystem.instance != null && Pathfinding.instance != null)
                    break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            if (ZNet.instance == null)
            {
                Logger.LogWarning("probe: no ZNet, nothing to probe");
                yield break;
            }
            if (Pathfinding.instance == null)
            {
                Logger.LogError("probe: Pathfinding.instance is null, aborting");
                yield break;
            }

            Logger.LogInfo("probe: mode=" + (ZNet.instance.IsServer() ? "server" : "client"));

            yield return new WaitForSeconds(6f);
            Logger.LogInfo("probe: begin");

            LogLayers();
            LogContainers();

            yield return ProbePaths();

            foreach (var go in _spawned)
            {
                if (go) Destroy(go);
            }
            Logger.LogInfo("probe: done");
        }

        // ------------------------------------------------------------------ layers

        private void LogLayers()
        {
            var mask = Pathfinding.instance.m_layers.value;
            Logger.LogInfo(string.Format("probe: nav_layers=0x{0:X8} water_layers=0x{1:X8}",
                mask, Pathfinding.instance.m_waterLayers.value));

            for (var i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                Logger.LogInfo(string.Format("probe: nav_layer[{0}]={1}", i, LayerMask.LayerToName(i)));
            }

            foreach (var n in new[] { "piece", "piece_nonsolid", "static_solid", "terrain_solid", "Default", "character", "vehicle", "viewblock" })
            {
                Logger.LogInfo(string.Format("probe: layer_id[{0}]={1}", n, LayerMask.NameToLayer(n)));
            }

            Logger.LogInfo(string.Format("probe: nav_update_interval={0} tile_size={1}",
                Pathfinding.instance.m_updateInterval, Pathfinding.instance.m_tileSize));
        }

        // -------------------------------------------------------------- containers

        private void LogContainers()
        {
            var containers = Object.FindObjectsOfType<Container>();
            if (containers.Length == 0)
            {
                // The fixture world ships no chests; one is needed to measure where contents live.
                var go = SpawnPrefabAt("piece_chest_wood", GroundRaw(new Vector3(4f, 0f, 4f)) + Vector3.up * 0.2f);
                if (go != null) containers = new[] { go.GetComponentInChildren<Container>() };
                Logger.LogInfo("probe: spawned_test_chest=" + (go != null));
            }
            var withItems = 0;
            long bytes = 0;
            var stacks = 0;

            var sw = Stopwatch.StartNew();
            foreach (var c in containers)
            {
                if (c == null) continue;
                var nv = c.GetComponent<ZNetView>();
                var zdo = nv != null ? nv.GetZDO() : null;
                if (zdo != null)
                {
                    var arr = zdo.GetByteArray("items");
                    if (arr != null && arr.Length > 0)
                    {
                        withItems++;
                        bytes += arr.Length;
                    }
                }
                var inv = c.GetInventory();
                if (inv != null) stacks += inv.NrOfItems();
            }
            sw.Stop();

            Logger.LogInfo(string.Format(
                "probe: containers={0} with_items={1} item_bytes={2} stacks={3} scan_ms={4:F2}",
                containers.Length, withItems, bytes, stacks, sw.Elapsed.TotalMilliseconds));

            // Does the ZDO byte array track an add, and is it readable without the container opened?
            var chest = containers.Length > 0 ? containers[0] : null;
            if (chest == null) return;

            var view = chest.GetComponent<ZNetView>();
            var before = view != null && view.GetZDO() != null ? view.GetZDO().GetByteArray("items") : null;
            Logger.LogInfo("probe: chest_zdo_before_bytes=" + (before == null ? 0 : before.Length));

            var inv2 = chest.GetInventory();
            var wood = ZNetScene.instance.GetPrefab("Wood");
            if (inv2 != null && wood != null)
            {
                inv2.AddItem(wood, 50);
                Logger.LogInfo("probe: chest_count_after_add=" + inv2.CountItems("$item_wood"));
            }

            var after = view != null && view.GetZDO() != null ? view.GetZDO().GetByteArray("items") : null;
            Logger.LogInfo("probe: chest_zdo_after_bytes=" + (after == null ? 0 : after.Length));
        }

        // ------------------------------------------------------------------ paths

        private IEnumerator ProbePaths()
        {
            // A dedicated server never instantiates a Player object it can Find; the peer list is
            // the only reliable source of "where is the human".
            Vector3? playerPos = null;
            var waitedForPlayer = 0f;
            while (playerPos == null && waitedForPlayer < 900f)
            {
                playerPos = TryGetPlayerPosition();
                if (playerPos != null) break;
                yield return new WaitForSeconds(2f);
                waitedForPlayer += 2f;
            }
            Logger.LogInfo("probe: waited_for_player_s=" + waitedForPlayer + " found=" + playerPos.HasValue);

            var hint = playerPos.HasValue
                ? playerPos.Value + new Vector3(10f, 0f, 0f)
                : FindLoadedSite();

            var ground = GroundRaw(hint);
            var center = new Vector3(ground.x, ground.y + 0.05f, ground.z);
            var inside = center + Vector3.up * 0.2f;
            var outside = GroundRaw(new Vector3(ground.x + 8f, 0f, ground.z)) + Vector3.up * 0.2f;

            Logger.LogInfo(string.Format("probe: site={0} ground_raycast_y={1:F2} outside={2}", center, ground.y, outside));

            ReportPoint("control_inside", inside);
            PathTest("pre_house", outside, inside);
            yield return null;

            // Build first: the room plus its walkway is the test surface, and neither depends on
            // the zone being ready — pieces carry their own colliders.
            var built = BuildHouse(center);
            Logger.LogInfo("probe: house_pieces=" + built);

            // Path from the far end of the walkway, through the doorway, to the room centre.
            var walkStart = center + new Vector3(0f, 0.2f, -10.5f);
            PathTest("pre_poll", walkStart, inside);

            for (var i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(3f);
                PathTest("poll" + i, walkStart, inside);
            }

            ReportPoint("house_inside", inside);
        }

        private static bool IsPlausible(Vector3 p)
        {
            // Unspawned characters sit at the (1000000, 1000000) sentinel; real world coords are small.
            return Mathf.Abs(p.x) < 500000f && Mathf.Abs(p.z) < 500000f && Mathf.Abs(p.y) < 10000f;
        }

        private static Vector3? TryGetPlayerPosition()
        {
            try
            {
                // Client, or a host-and-play world: the local player is authoritative.
                var local = Player.m_localPlayer;
                if (local != null)
                {
                    var localPos = local.transform.position;
                    if (IsPlausible(localPos)) return localPos;
                    return null;
                }

                // Dedicated server: the peer's reference position is what sector streaming uses.
                var peersField = typeof(ZNet).GetField("m_peers", BindingFlags.Instance | BindingFlags.NonPublic);
                var peers = peersField?.GetValue(ZNet.instance) as System.Collections.IList;
                if (peers != null && peers.Count > 0)
                {
                    var peer = peers[0] as ZNetPeer;
                    if (peer != null)
                    {
                        var pos = peer.GetRefPos();
                        if (pos != Vector3.zero && IsPlausible(pos)) return pos;
                    }
                }

                // Fallback: the public player list, empty unless the player shares position.
                var playersField = typeof(ZNet).GetField("m_players", BindingFlags.Instance | BindingFlags.NonPublic);
                var list = playersField?.GetValue(ZNet.instance) as System.Collections.IList;
                if (list == null || list.Count == 0) return null;

                var info = list[0];
                var posField = info.GetType().GetField("m_position");
                if (posField == null) return null;
                var fallback = (Vector3)posField.GetValue(info);
                return fallback == Vector3.zero || !IsPlausible(fallback) ? (Vector3?)null : fallback;
            }
            catch
            {
                return null;
            }
        }

        private static Vector3 FindLoadedSite()
        {
            var rocks = Object.FindObjectsOfType<MineRock5>();
            if (rocks.Length > 0) return rocks[0].transform.position + new Vector3(12f, 0f, 0f);

            var characters = Object.FindObjectsOfType<Character>();
            if (characters.Length > 0) return characters[0].transform.position + new Vector3(8f, 0f, 0f);

            return new Vector3(15f, 0f, 15f);
        }

        private void ReportPoint(string label, Vector3 p)
        {
            var found = NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas);
            Logger.LogInfo(string.Format("probe: navsample[{0}] found={1} dist={2:F2}",
                label, found, found ? Vector3.Distance(p, hit.position) : -1f));

            RaycastHit rh;
            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out rh, 8f))
            {
                Logger.LogInfo(string.Format("probe: solid[{0}] collider={1} layer={2} y={3:F2}",
                    label, rh.collider.name, LayerMask.LayerToName(rh.collider.gameObject.layer), rh.point.y));
            }
            else
            {
                Logger.LogInfo("probe: solid[" + label + "] none");
            }
        }

        private void PathTest(string label, Vector3 from, Vector3 to)
        {
            var pf = Pathfinding.instance;

            var have = false;
            try
            {
                have = pf.HavePath(from, to, Pathfinding.AgentType.Humanoid);
            }
            catch (Exception e)
            {
                Logger.LogWarning("probe: " + label + " HavePath threw " + e.GetType().Name);
            }

            var partial = new List<Vector3>();
            var okPartial = pf.GetPath(from, to, partial, Pathfinding.AgentType.Humanoid, requireFullPath: false, cleanup: true);
            var partialEnd = partial.Count > 0 ? partial[partial.Count - 1] : Vector3.zero;

            var full = new List<Vector3>();
            var okFull = pf.GetPath(from, to, full, Pathfinding.AgentType.Humanoid, requireFullPath: true, cleanup: true);
            var fullEnd = full.Count > 0 ? full[full.Count - 1] : Vector3.zero;

            Logger.LogInfo(string.Format(
                "probe: path[{0}] have={1} partial={2}/{3} partial_end_h={4:F2} | full={5}/{6} full_end_h={7:F2} full_end_y={8:F2} target_y={9:F2}",
                label, have, okPartial, partial.Count, Horizontal(partialEnd, to),
                okFull, full.Count, Horizontal(fullEnd, to), fullEnd.y, to.y));
        }

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        // ------------------------------------------------------------------ house

        private int BuildHouse(Vector3 center)
        {
            var floor = FirstPrefab("wood_floor", "wood_floor_1x1", "floor_wood");
            var wall = FirstPrefab("wood_wall", "wood_wall_half", "wall_wood");

            Logger.LogInfo(string.Format("probe: prefab_floor={0} prefab_wall={1}",
                floor != null ? floor.name : "none", wall != null ? wall.name : "none"));

            if (floor == null) return 0;

            var built = 0;
            var y = center.y;

            for (var ix = -1; ix <= 1; ix++)
            {
                for (var iz = -1; iz <= 1; iz++)
                {
                    if (Spawn(floor, new Vector3(center.x + ix * 2f, y, center.z + iz * 2f), Quaternion.identity)) built++;
                }
            }

            // Walkway out through the doorway, so the path test never depends on terrain.
            for (var k = 0; k < 4; k++)
            {
                if (Spawn(floor, new Vector3(center.x, y, center.z - 4f - k * 2f), Quaternion.identity)) built++;
            }

            if (wall == null) return built;

            var wy = y + 1f;
            for (var i = -1; i <= 1; i++)
            {
                if (Spawn(wall, new Vector3(center.x + 3f, wy, center.z + i * 2f), Quaternion.Euler(0f, 90f, 0f))) built++;
                if (Spawn(wall, new Vector3(center.x - 3f, wy, center.z + i * 2f), Quaternion.Euler(0f, 90f, 0f))) built++;
                if (Spawn(wall, new Vector3(center.x + i * 2f, wy, center.z + 3f), Quaternion.identity)) built++;
                if (i != 0 && Spawn(wall, new Vector3(center.x + i * 2f, wy, center.z - 3f), Quaternion.identity)) built++;
            }

            return built;
        }

        private bool Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            try
            {
                // A client cannot author world ZDOs; local instantiation is enough, because the
                // navmesh that matters here is built per-peer from local colliders.
                if (ZNet.instance != null && !ZNet.instance.IsServer())
                {
                    var local = Object.Instantiate(prefab, pos, rot);
                    var localWear = local.GetComponent<WearNTear>();
                    if (localWear) Destroy(localWear);
                    _spawned.Add(local);
                    return true;
                }

                var hash = prefab.name.GetStableHashCode();
                var zdo = ZDOMan.instance.CreateNewZDO(pos, hash);
                zdo.SetPrefab(hash);

                var go = typeof(ZNetScene).GetMethod("CreateObject", Priv)
                    ?.Invoke(ZNetScene.instance, new object[] { zdo }) as GameObject;
                if (go == null) return false;

                go.transform.rotation = rot;

                // Pieces placed without support are torn down by WearNTear; the test only needs colliders.
                var wear = go.GetComponent<WearNTear>();
                if (wear) Destroy(wear);

                _spawned.Add(go);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogWarning("probe: spawn failed " + e.GetType().Name + ": " + e.Message);
                return false;
            }
        }

        private static GameObject FirstPrefab(params string[] names)
        {
            foreach (var n in names)
            {
                var p = ZNetScene.instance.GetPrefab(n);
                if (p != null) return p;
            }
            return null;
        }

        private static Vector3 GroundAt(Vector3 around)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(around.x, 300f, around.z), Vector3.down, out hit, 600f))
            {
                return hit.point + Vector3.up * 0.5f;
            }
            return new Vector3(around.x, 1f, around.z);
        }

        private static Vector3 GroundRaw(Vector3 around)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(around.x, 300f, around.z), Vector3.down, out hit, 600f))
            {
                return hit.point;
            }
            return new Vector3(around.x, 1f, around.z);
        }

        private GameObject SpawnPrefabAt(string name, Vector3 pos)
        {
            var prefab = ZNetScene.instance.GetPrefab(name);
            if (prefab == null)
            {
                Logger.LogWarning("probe: prefab missing: " + name);
                return null;
            }
            return Spawn(prefab, pos, Quaternion.identity) ? _spawned[_spawned.Count - 1] : null;
        }
    }
}
