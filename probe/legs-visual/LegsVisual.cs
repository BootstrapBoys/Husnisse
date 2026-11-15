using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Creature;
using Husnisse.Flow;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Husnisse.LegsVisual
{
    /// <summary>
    /// Server-side scene driver for the 0.1.4 client visual gate. It does not test anything itself:
    /// it builds the walkway, the bowl, the chest, the room and the drops on command, then reports
    /// what the mod did. A real client watches and photographs; this end supplies the scene and the
    /// server-side numbers that must agree with what the client saw.
    ///
    /// Commands arrive in BepInEx/config/legs-visual.cmd, one per line, and every distinct file body
    /// runs once:
    ///   build [x y z]   destroy old bowls, build a 3x21 floor strip, bowl at the origin, chest at +15
    ///   drop            three coal near the bowl (starts a walk trip)
    ///   drop2           two more coal (the fade trip, after the room is closed)
    ///   room            a 3x3 room around the chest with one 2 m doorway facing the strip
    ///   close           wall the doorway shut
    ///   state           one status line
    ///   watch &lt;s&gt;      sample once a second for s seconds
    ///   probe           ask for a full path across the strip and report what came back
    ///   route           report the visible players/peers, then push a marked HUD line to each peer
    ///   cleanup         destroy the scene this probe built
    /// Never shipped; lives in probe/.
    /// </summary>
    [BepInPlugin(Guid, "Husnisse Legs Visual", "0.1.0")]
    public class LegsVisualPlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.husnisse.legsvisual";

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly Vector3 DefaultOrigin = new Vector3(0f, 40f, 0f);

        private static readonly string CommandFile =
            Path.Combine(Paths.ConfigPath, "legs-visual.cmd");

        private static readonly string OwnLog =
            Path.Combine(Paths.BepInExRootPath, "BSBLogs", "LegsVisual.log");

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private Vector3 _origin = DefaultOrigin;
        private Container _chest;
        private NisseActor _nisse;
        private string _lastCommands = string.Empty;
        private int _peerCount = -1;
        private bool _built;

        private int _samples;
        private int _maxStepX10;
        private int _fades;

        private void Awake()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OwnLog) ?? Paths.BepInExRootPath);
            Say("loading " + Info.Metadata.Version);
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
                Say("not a server; nothing to drive");
                yield break;
            }

            try
            {
                var harmony = new Harmony("husnisse.legsvisual.hold");
                harmony.Patch(typeof(ZNetScene).GetMethod("CreateDestroyObjects", Priv),
                    prefix: new HarmonyMethod(typeof(HoldPatch).GetMethod("Prefix")));
                Say("culling suppressed");
            }
            catch (Exception e)
            {
                Say("culling patch failed: " + e.Message);
            }

            if (HusnisseConfig.DebugLogging != null) HusnisseConfig.DebugLogging.Value = true;
            yield return new WaitForSeconds(4f);
            Say("ready command_file=" + CommandFile);

            while (true)
            {
                TrackPeers();
                PollCommands();
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ------------------------------------------------------------------ commands

        private void PollCommands()
        {
            string text;
            try
            {
                if (!File.Exists(CommandFile)) return;
                text = File.ReadAllText(CommandFile).Trim();
            }
            catch (Exception e)
            {
                Say("command file unreadable: " + e.Message);
                return;
            }

            if (string.IsNullOrEmpty(text) || text == _lastCommands) return;
            _lastCommands = text;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var verb = parts[0].ToLowerInvariant();
                Say("cmd: " + line);

                try
                {
                    switch (verb)
                    {
                        case "build": Build(parts); break;
                        case "drop": Drop(3, 1f); break;
                        case "drop2": Drop(2, 1.5f); break;
                        case "room": Room(); break;
                        case "close": Close(); break;
                        case "state": State(); break;
                        case "watch": Watch(parts); break;
                        case "probe": Probe(); break;
                        case "route": Route(); break;
                        case "cleanup": Cleanup(); break;
                        case "peers": Peers(); break;
                        default: Say("unknown command: " + line); break;
                    }
                }
                catch (Exception e)
                {
                    Say("command '" + line + "' failed: " + (e.InnerException ?? e));
                }
            }
        }

        private void Build(string[] parts)
        {
            if (_built) Cleanup();

            _origin = DefaultOrigin;
            if (parts.Length >= 5)
            {
                float x, y, z;
                if (float.TryParse(parts[1], out x) && float.TryParse(parts[2], out y) && float.TryParse(parts[3], out z))
                    _origin = new Vector3(x, y, z);
            }

            DestroyExistingBowls();

            // Three rows, so a client can stand beside the walking line instead of on it.
            var floors = 0;
            for (var row = -1; row <= 1; row++)
            {
                floors += BuildStrip(_origin + new Vector3(0f, 0f, row * 2f), 0f, 21);
            }

            var bowlGo = Spawn("husnisse_bowl", _origin + Vector3.up * 0.1f, Quaternion.Euler(0f, 90f, 0f));
            if (bowlGo != null) _spawned.Add(bowlGo);

            var chestGo = Spawn("piece_chest_wood", _origin + new Vector3(15f, 0.1f, 0f), Quaternion.identity);
            if (chestGo != null)
            {
                _spawned.Add(chestGo);
                _chest = chestGo.GetComponent<Container>();
            }

            _built = true;
            Say("built origin=" + _origin.ToString("F1") + " floors=" + floors +
                " bowl=" + (bowlGo != null) + " chest=" + (_chest != null));
        }

        private void Drop(int count, float spread)
        {
            if (!_built) { Say("drop before build"); return; }

            var points = new[]
            {
                new Vector3(2f, 0.4f, 0f),
                new Vector3(3.5f, 0.4f, 0f),
                new Vector3(5f, 0.4f, 0f),
                new Vector3(2.5f, 0.4f, spread * 0.4f),
                new Vector3(4.5f, 0.4f, -spread * 0.4f),
            };

            var spawned = 0;
            for (var i = 0; i < count && i < points.Length; i++)
            {
                var go = Spawn("Coal", _origin + points[i], Quaternion.identity);
                if (go == null) continue;
                _spawned.Add(go);
                spawned++;
            }
            Say("drop count=" + spawned + " at " + _origin.ToString("F1"));
        }

        private void Room()
        {
            if (!_built) { Say("room before build"); return; }
            var walls = BuildRoom(_origin + new Vector3(14f, 0f, 0f));
            Say("room walls=" + walls);
        }

        private void Close()
        {
            var closed = CloseDoorway(_origin + new Vector3(12f, 0.1f, 0f));
            Say("doorway closed pieces=" + closed);
        }

        private void State()
        {
            var claim = ClaimRegistry.Count > 0 ? ClaimRegistry.All()[0] : null;
            var trip = claim != null ? TripRunner.Get(claim) : null;
            var actor = FindNisse();
            var motion = actor != null ? actor.GetComponent<NisseMotion>() : null;

            Say("state nisse=" + (actor != null ? actor.transform.position.ToString("F2") : "-") +
                " motion=" + (motion != null ? motion.State.ToString() : "-") +
                " fades=" + (motion != null ? motion.Fades : _fades) +
                " trip=" + (trip != null ? trip.ShortState() : "idle") +
                " chest=" + ChestCoal() +
                " queued=" + (claim != null ? FlowQueue.Count(claim) : -1) +
                " trips=" + TripRunner.Count +
                " bowls=" + BowlCount() +
                " claims=" + ClaimRegistry.Count +
                " peers=" + PeerLine());
        }

        private void Watch(string[] parts)
        {
            var seconds = 30f;
            if (parts.Length > 1) float.TryParse(parts[1], out seconds);
            StartCoroutine(WatchRoutine(seconds));
        }

        private IEnumerator WatchRoutine(float seconds)
        {
            var elapsed = 0f;
            var last = Vector3.zero;
            var have = false;

            while (elapsed < seconds)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;

                var actor = FindNisse();
                if (actor == null) { Say("watch t=" + elapsed.ToString("0") + " nisse=-"); continue; }

                var position = actor.transform.position;
                if (!have) { last = position; have = true; }

                var step = Horizontal(last, position);
                var stepX10 = Mathf.RoundToInt(step * 10f);
                if (stepX10 > _maxStepX10) _maxStepX10 = stepX10;
                _samples++;

                var motion = actor.GetComponent<NisseMotion>();
                if (motion != null) _fades = motion.Fades;

                var claim = ClaimRegistry.Count > 0 ? ClaimRegistry.All()[0] : null;
                var trip = claim != null ? TripRunner.Get(claim) : null;

                Say("watch t=" + elapsed.ToString("0") +
                    " pos=" + position.ToString("F2") +
                    " step=" + step.ToString("0.00") +
                    " motion=" + (motion != null ? motion.State.ToString() : "-") +
                    " trip=" + (trip != null ? trip.ShortState() : "idle") +
                    " chest=" + ChestCoal() +
                    " queued=" + (claim != null ? FlowQueue.Count(claim) : -1));

                last = position;
            }

            Say("watch done samples=" + _samples + " max_step_x10=" + _maxStepX10 + " fades=" + _fades);
        }

        private void Probe()
        {
            var from = _origin + new Vector3(1.5f, 0.1f, 0f);
            var to = _origin + new Vector3(13.85f, 0.1f, 0f);
            var full = new List<Vector3>();
            var ok = Pathfinding.instance.GetPath(from, to, full, Pathfinding.AgentType.Humanoid,
                requireFullPath: true, cleanup: true);
            Say("bake_probe full=" + ok + " corners=" + full.Count);
        }

        /// <summary>
        /// The HUD-routing experiment, and the only reason this command exists: report what the
        /// server can actually see, then push a marked line to every connected peer through the
        /// game's own client-side "ShowMessage" handler. The client probe reports which markers
        /// reached <c>MessageHud.GetLog()</c>, so delivery is measured on the client, not inferred
        /// here.
        /// </summary>
        private void Route()
        {
            var players = Player.GetAllPlayers();
            Say("route players=" + players.Count +
                " peers=" + (ZNet.instance != null ? ZNet.instance.GetPeers().Count : -1) +
                " isServer=" + (ZNet.instance != null && ZNet.instance.IsServer()));

            // Kept deliberately: on a dedicated server this is 0, and that is the finding this
            // command was added to record. Player.Message is unreachable when there are no players.
            if (players.Count == 0) Say("route Player.GetAllPlayers() is empty on this peer");

            if (ZNet.instance == null || ZRoutedRpc.instance == null)
            {
                Say("route: no ZNet / ZRoutedRpc");
                return;
            }

            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer == null) continue;
                Say("route peer uid=" + peer.m_uid +
                    " name=\"" + peer.m_playerName + "\"" +
                    " ready=" + peer.IsReady() +
                    " refpos=" + peer.GetRefPos().ToString("F2") +
                    " character=" + peer.m_characterID);

                if (!peer.IsReady()) { Say("route skipped: peer not ready"); continue; }

                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage",
                    (int)MessageHud.MessageType.TopLeft, "PROBE-B ShowMessage peer");
                Say("route B sent to uid=" + peer.m_uid);
            }
        }

        private void Cleanup()
        {
            foreach (var go in _spawned)
            {
                if (go) Destroy(go);
            }
            _spawned.Clear();
            _chest = null;
            _nisse = null;
            _built = false;
            DestroyExistingBowls();
            Say("cleanup done bowls=" + BowlCount());
        }

        // ------------------------------------------------------------------ scene

        private int BuildStrip(Vector3 origin, float y, int count)
        {
            var floor = ZNetScene.instance.GetPrefab("wood_floor");
            if (floor == null) floor = ZNetScene.instance.GetPrefab("wood_floor_1x1");
            if (floor == null) { Say("no floor prefab"); return 0; }

            var built = 0;
            for (var i = 0; i < count; i++)
            {
                var go = Spawn(floor.name, origin + new Vector3(i * 1f, y, 0f), Quaternion.identity);
                if (go == null) continue;
                _spawned.Add(go);
                built++;
            }
            return built;
        }

        /// <summary>
        /// A 3x3 floor room with a wall on every side and one 2 m doorway in its west face. The
        /// chest at origin+15 needs reaching through that doorway.
        /// </summary>
        private int BuildRoom(Vector3 origin)
        {
            var wall = ZNetScene.instance.GetPrefab("wood_wall");
            if (wall == null) wall = ZNetScene.instance.GetPrefab("wood_wall_half");
            if (wall == null) { Say("no wall prefab"); return 0; }

            var floors = BuildStrip(origin + new Vector3(-2f, 0f, 0f), 0f, 5);
            var floorName = ZNetScene.instance.GetPrefab("wood_floor") != null ? "wood_floor" : "wood_floor_1x1";
            for (var x = -2; x <= 2; x += 2)
            {
                var a = Spawn(floorName, origin + new Vector3(x, 0f, -2f), Quaternion.identity);
                var b = Spawn(floorName, origin + new Vector3(x, 0f, 2f), Quaternion.identity);
                if (a != null) _spawned.Add(a);
                if (b != null) _spawned.Add(b);
            }
            Say("room floors=" + floors);

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
            if (go == null) return 0;
            _spawned.Add(go);
            return 1;
        }

        private int CloseDoorway(Vector3 westFace)
        {
            var wall = ZNetScene.instance.GetPrefab("wood_wall");
            if (wall == null) wall = ZNetScene.instance.GetPrefab("wood_wall_half");
            return wall == null ? 0 : WallAt(wall, westFace, true);
        }

        private GameObject Spawn(string prefabName, Vector3 pos, Quaternion rot)
        {
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Say("no prefab " + prefabName);
                return null;
            }

            var hash = prefabName.GetStableHashCode();
            var zdo = ZDOMan.instance.CreateNewZDO(pos, hash);
            zdo.SetPrefab(hash);

            var go = typeof(ZNetScene).GetMethod("CreateObject", Priv)
                ?.Invoke(ZNetScene.instance, new object[] { zdo }) as GameObject;
            if (go == null)
            {
                Say("CreateObject null for " + prefabName);
                return null;
            }

            go.transform.rotation = rot;
            var wear = go.GetComponent<WearNTear>();
            if (wear) Destroy(wear);
            return go;
        }

        /// <summary>A saved world may still carry a bowl from the last session, and a second bowl in
        /// one claim is suppressed — the nisse would never come.</summary>
        private void DestroyExistingBowls()
        {
            var bowlType = Type.GetType("Husnisse.Bowl.BowlPiece, Husnisse");
            if (bowlType == null) return;

            foreach (var found in Object.FindObjectsByType(bowlType, FindObjectsSortMode.None))
            {
                var component = found as Component;
                if (component == null) continue;
                Say("destroying existing bowl at " + component.transform.position.ToString("F1"));
                var view = component.GetComponent<ZNetView>();
                if (view != null && view.IsValid()) view.Destroy();
                else Destroy(component.gameObject);
            }
        }

        private int BowlCount()
        {
            var bowlType = Type.GetType("Husnisse.Bowl.BowlPiece, Husnisse");
            return bowlType == null ? -1 : Object.FindObjectsByType(bowlType, FindObjectsSortMode.None).Length;
        }

        private NisseActor FindNisse()
        {
            if (_nisse != null) return _nisse;
            var all = Object.FindObjectsByType<NisseActor>(FindObjectsSortMode.None);
            return all.Length > 0 ? (_nisse = all[0]) : null;
        }

        private int ChestCoal()
        {
            var inventory = _chest != null ? _chest.GetInventory() : null;
            return inventory != null ? inventory.CountItems("$item_coal") : -1;
        }

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        // ------------------------------------------------------------------ peers

        private void TrackPeers()
        {
            var count = ZNet.instance != null ? ZNet.instance.GetPeers().Count : 0;
            if (count == _peerCount) return;
            _peerCount = count;

            if (count == 0) { Say("peer gone; peers=0"); return; }

            foreach (var peer in ZNet.instance.GetPeers())
            {
                var pos = peer.GetRefPos();
                Say("peer joined; peers=" + count + " refpos=" + pos.ToString("F2"));
            }
        }

        private void Peers()
        {
            Say("peers=" + PeerLine());
        }

        private static string PeerLine()
        {
            if (ZNet.instance == null) return "?";
            var peers = ZNet.instance.GetPeers();
            var sb = new StringBuilder();
            sb.Append(peers.Count);
            foreach (var peer in peers)
            {
                sb.Append(' ').Append(peer.GetRefPos().ToString("F1"));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ logging

        private static class HoldPatch
        {
            public static bool Prefix()
            {
                return false;
            }
        }

        /// <summary>Own log file: LogOutput.log is held open by the running server and cannot be
        /// read over SSH until shutdown.</summary>
        private void Say(string message)
        {
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
            Logger.LogInfo("lv " + message);
            try
            {
                using (var writer = new StreamWriter(OwnLog, true))
                {
                    writer.WriteLine(line);
                    writer.Flush();
                }
            }
            catch
            {
                // A logging failure must not take the scene down.
            }
        }
    }
}
