using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Husnisse.LegsProbe
{
    /// <summary>
    /// Throwaway probe answering the two facts 0.1.4 rests on:
    ///   1. Does a dedicated server bake player-built floors into its own navmesh, so a server-owned
    ///      creature can path along them without any terrain zone loaded?
    ///   2. Which shader property can fade the Greyling-derived nisse, and which animator triggers
    ///      does its controller actually have?
    /// Logs only. Never mutates anything outside the test world.
    /// </summary>
    [BepInPlugin(Guid, "Husnisse Legs Probe", "0.1.0")]
    public class LegsProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.husnisse.legsprobe";

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Awake()
        {
            Logger.LogInfo("legsprobe: loaded");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var waited = 0f;
            while (waited < 180f)
            {
                if (ZNet.instance != null && ZNetScene.instance != null && Pathfinding.instance != null) break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            if (ZNet.instance == null || ZNetScene.instance == null || Pathfinding.instance == null)
            {
                Logger.LogWarning("legsprobe: runtime not ready");
                yield break;
            }

            // ZNetScene destroys anything whose sector is not active. Without zones on a dedicated
            // server that is everything, including the test floor. Same effect as the harness `hold`.
            try
            {
                var harmony = new Harmony("husnisse.legsprobe.hold");
                var target = typeof(ZNetScene).GetMethod("CreateDestroyObjects", Priv);
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(HoldPatch).GetMethod("Prefix")));
                Logger.LogInfo("legsprobe: culling suppressed");
            }
            catch (Exception e)
            {
                Logger.LogWarning("legsprobe: culling patch failed: " + e.Message);
            }

            Logger.LogInfo("legsprobe: mode=" + (ZNet.instance.IsServer() ? "server" : "client"));
            yield return new WaitForSeconds(6f);
            Logger.LogInfo("legsprobe: begin");
            Logger.LogInfo("legsprobe: nav_update_interval=" + Pathfinding.instance.m_updateInterval +
                           " tile=" + Pathfinding.instance.m_tileSize +
                           " pathfinding_enabled=" + Pathfinding.instance.enabled +
                           " go_active=" + Pathfinding.instance.gameObject.activeInHierarchy);

            DumpCreature();

            // A self-supporting 20 m walkway at the origin: the server's reference position, so
            // ZNetScene's sector pass keeps it, and no terrain is needed under it.
            var y = 40f;
            var start = new Vector3(0f, y + 0.2f, 0f);
            var end = new Vector3(20f, y + 0.2f, 0f);
            var built = BuildWalkway(new Vector3(0f, y, 0f), 21);
            Logger.LogInfo("legsprobe: walkway_pieces=" + built + " start=" + start + " end=" + end);
            DescribeWalkway(new Vector3(10f, y, 0f));

            yield return new WaitForSeconds(3f);

            for (var i = 0; i < 12; i++)
            {
                CountColliders(new Vector3(10f, y, 0f));
                NavMeshStats();
                PathTest("leg" + i, start, end, true);
                PathTest("leg" + i + "_near", start, new Vector3(10f, y + 0.2f, 0f), false);
                yield return new WaitForSeconds(3f);
            }

            SamplePoint("start", start);
            SamplePoint("end", end);

            foreach (var go in _spawned)
            {
                if (go) Destroy(go);
            }

            Logger.LogInfo("legsprobe: done");
        }

        private static class HoldPatch
        {
            public static bool Prefix()
            {
                return false;
            }
        }

        // --------------------------------------------------------------- creature

        private void DumpCreature()
        {
            var prefab = ZNetScene.instance.GetPrefab("Greyling");
            if (prefab == null)
            {
                Logger.LogWarning("legsprobe: no Greyling prefab");
                return;
            }

            var go = Object.Instantiate(prefab, new Vector3(0f, 39.5f, -8f), Quaternion.identity);
            _spawned.Add(go);

            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                for (var i = 0; i < animator.parameterCount; i++)
                {
                    var p = animator.GetParameter(i);
                    Logger.LogInfo("legsprobe: anim_param[" + i + "]=" + p.name + " type=" + p.type);
                }
            }
            else
            {
                Logger.LogWarning("legsprobe: no Animator on Greyling");
            }

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            Logger.LogInfo("legsprobe: renderers=" + renderers.Length);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var mat = renderer.sharedMaterial;
                if (mat == null || mat.shader == null)
                {
                    Logger.LogInfo("legsprobe: renderer " + renderer.name + " has no material");
                    continue;
                }

                var floats = new List<string>();
                foreach (var name in mat.GetPropertyNames(MaterialPropertyType.Float))
                {
                    floats.Add(name + "=" + mat.GetFloat(name).ToString("0.###"));
                }

                var vectors = new List<string>();
                foreach (var name in mat.GetPropertyNames(MaterialPropertyType.Vector))
                {
                    var v = mat.GetVector(name);
                    vectors.Add(name + "=(" + v.x.ToString("0.##") + "," + v.y.ToString("0.##") + "," +
                                v.z.ToString("0.##") + "," + v.w.ToString("0.##") + ")");
                }

                var textures = mat.GetPropertyNames(MaterialPropertyType.Texture);
                var hasAlpha = mat.HasProperty("_Alpha") ? "yes" : "no";
                var hasCutoff = mat.HasProperty("_Cutoff") ? "yes" : "no";
                var hasColor = mat.HasProperty("_Color") ? "yes" : "no";
                Logger.LogInfo("legsprobe: renderer=" + renderer.name + " shader=" + mat.shader.name +
                               " floats=[" + string.Join(", ", floats.ToArray()) + "]" +
                               " vectors=[" + string.Join(", ", vectors.ToArray()) + "]" +
                               " textures=" + textures.Length +
                               " hasAlpha=" + hasAlpha + " hasCutoff=" + hasCutoff + " hasColor=" + hasColor);
            }
        }

        // ------------------------------------------------------------------ house

        private int BuildWalkway(Vector3 origin, int count)
        {
            var floor = ZNetScene.instance.GetPrefab("wood_floor");
            if (floor == null) floor = ZNetScene.instance.GetPrefab("wood_floor_1x1");
            if (floor == null)
            {
                Logger.LogWarning("legsprobe: no wood floor prefab");
                return 0;
            }

            var built = 0;
            for (var i = 0; i < count; i++)
            {
                if (Spawn(floor, origin + new Vector3(i * 1f, 0f, 0f), Quaternion.identity)) built++;
            }
            return built;
        }

        private bool Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            try
            {
                var hash = prefab.name.GetStableHashCode();
                var zdo = ZDOMan.instance.CreateNewZDO(pos, hash);
                zdo.SetPrefab(hash);

                var go = typeof(ZNetScene).GetMethod("CreateObject", Priv)
                    ?.Invoke(ZNetScene.instance, new object[] { zdo }) as GameObject;
                if (go == null) return false;

                go.transform.rotation = rot;
                var wear = go.GetComponent<WearNTear>();
                if (wear) Destroy(wear);
                _spawned.Add(go);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogWarning("legsprobe: spawn failed " + e.GetType().Name + ": " + e.Message);
                return false;
            }
        }

        private void DescribeWalkway(Vector3 around)
        {
            foreach (var go in _spawned)
            {
                if (go == null || go.GetComponent<Character>() != null) continue;
                var collider = go.GetComponentInChildren<Collider>(true);
                Logger.LogInfo("legsprobe: piece " + go.name + " active=" + go.activeInHierarchy +
                               " layer=" + LayerMask.LayerToName(go.layer) +
                               " at=" + go.transform.position.ToString("F1") +
                               " collider=" + (collider != null ? collider.GetType().Name + "/enabled=" + collider.enabled : "none"));
                break;
            }
        }

        private void CountColliders(Vector3 center)
        {
            var hits = Physics.OverlapBox(center + Vector3.up * 0.1f, new Vector3(11f, 1f, 3f),
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            var layers = new Dictionary<string, int>();
            foreach (var hit in hits)
            {
                var name = LayerMask.LayerToName(hit.gameObject.layer);
                int count;
                layers.TryGetValue(name, out count);
                layers[name] = count + 1;
            }

            var parts = new List<string>();
            foreach (var pair in layers) parts.Add(pair.Key + "=" + pair.Value);
            Logger.LogInfo("legsprobe: colliders_at_walkway total=" + hits.Length + " [" + string.Join(", ", parts.ToArray()) + "]");
        }

        private void NavMeshStats()
        {
            var tri = NavMesh.CalculateTriangulation();
            Logger.LogInfo("legsprobe: navmesh_vertices=" + (tri.vertices != null ? tri.vertices.Length : 0) +
                           " navmesh_tris=" + (tri.indices != null ? tri.indices.Length / 3 : 0));
        }

        // ------------------------------------------------------------------ paths

        private void PathTest(string label, Vector3 from, Vector3 to, bool full)
        {
            var pf = Pathfinding.instance;
            var partial = new List<Vector3>();
            var okPartial = pf.GetPath(from, to, partial, Pathfinding.AgentType.Humanoid,
                requireFullPath: false, cleanup: true);
            var partialEnd = partial.Count > 0 ? partial[partial.Count - 1] : Vector3.zero;

            var fullPath = new List<Vector3>();
            var okFull = pf.GetPath(from, to, fullPath, Pathfinding.AgentType.Humanoid,
                requireFullPath: true, cleanup: true);
            var fullEnd = fullPath.Count > 0 ? fullPath[fullPath.Count - 1] : Vector3.zero;

            var have = pf.HavePath(from, to, Pathfinding.AgentType.Humanoid);
            if (!full && okPartial && !okFull) return; // off-path probes are expected to fail

            Logger.LogInfo(string.Format(
                "legsprobe: path[{0}] have={1} partial={2}/{3} end_h={4:F2} full={5}/{6} end_h={7:F2}",
                label, have, okPartial, partial.Count, Horizontal(partialEnd, to),
                okFull, fullPath.Count, Horizontal(fullEnd, to)));
        }

        private void SamplePoint(string label, Vector3 p)
        {
            var found = NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas);
            Logger.LogInfo(string.Format("legsprobe: navsample[{0}] found={1} dist={2:F2} at={3}",
                label, found, found ? Vector3.Distance(p, hit.position) : -1f, found ? hit.position.ToString("F2") : "-"));
        }

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
