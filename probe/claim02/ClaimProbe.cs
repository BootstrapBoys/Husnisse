using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace Husnisse.ClaimProbe
{
    /// <summary>
    /// Test-only client observer for the 0.2b gate. It watches the mod's own claim objects in the
    /// client process (not the server's), logs every change, and executes a small command file so a
    /// screenshot can be taken at an exact moment: the range ring, the hover text, a preview.
    /// Never shipped; lives in probe/.
    /// </summary>
    [BepInPlugin(Guid, "Husnisse Claim Probe", "0.2.0")]
    public class ClaimProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.husnisse.claimprobe02";

        private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

        private static readonly string CommandFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "husnisse-probe.cmd");

        private static readonly string CaptureDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "husnisse-captures");

        private string _lastCommands = string.Empty;
        private string _lastSnapshot = string.Empty;
        private float _pollTimer;
        private float _commandTimer;

        private Type _registryType;
        private Type _claimType;
        private Type _previewType;
        private Type _ringType;
        private Type _diagnosticsType;

        private void Awake()
        {
            Logger.LogInfo("c02 loaded " + Info.Metadata.Version);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var waited = 0f;
            while (waited < 240f)
            {
                if (Player.m_localPlayer != null && ZNetScene.instance != null &&
                    ObjectDB.instance != null && ZNet.instance != null && !ZNet.instance.IsServer())
                    break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            var player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null || ZNet.instance.IsServer())
            {
                Logger.LogWarning("c02 no client player; aborting");
                yield break;
            }

            Logger.LogInfo("c02 ready player=" + player.GetPlayerName() +
                           " pos=" + player.transform.position.ToString("F2") +
                           " world=" + (ZNet.instance.GetWorldName() ?? "?"));
            ResolveTypes();

            while (true)
            {
                _pollTimer += Time.deltaTime;
                _commandTimer += Time.deltaTime;

                if (_pollTimer >= 2f)
                {
                    _pollTimer = 0f;
                    PollClaims();
                }

                if (_commandTimer >= 1f)
                {
                    _commandTimer = 0f;
                    RunCommandFile();
                }

                yield return null;
            }
        }

        private void ResolveTypes()
        {
            _registryType = Type.GetType("Husnisse.Claims.ClaimRegistry, Husnisse");
            _claimType = Type.GetType("Husnisse.Claims.Claim, Husnisse");
            _previewType = Type.GetType("Husnisse.Claims.ClaimPreview, Husnisse");
            _ringType = Type.GetType("Husnisse.Claims.ClaimRing, Husnisse");
            _diagnosticsType = Type.GetType("Husnisse.Core.Diagnostics, Husnisse");

            Logger.LogInfo("c02 types registry=" + (_registryType != null) +
                           " claim=" + (_claimType != null) +
                           " preview=" + (_previewType != null) +
                           " ring=" + (_ringType != null));
        }

        private void PollClaims()
        {
            if (_registryType == null)
            {
                Logger.LogWarning("c02 mod types missing; is Husnisse loaded?");
                return;
            }

            try
            {
                _registryType.GetMethod("RevalidateAll", Static)?.Invoke(null, null);
                var all = _registryType.GetMethod("All", Static)?.Invoke(null, null) as IList;
                if (all == null) { Logger.LogWarning("c02 ClaimRegistry.All() unreadable"); return; }

                var lines = new List<string>();
                foreach (var claim in all)
                {
                    if (claim == null) continue;
                    var type = claim.GetType();
                    lines.Add("c02 claim " + Read(type, claim, "BowlId") +
                              " source=" + Read(type, claim, "SourceName") +
                              " ward=" + Read(type, claim, "WardId") +
                              " owner=" + Read(type, claim, "OwnerId") +
                              " r=" + Format((float)Read(type, claim, "Radius")) +
                              " center=" + FormatPoint((Vector3)Read(type, claim, "Center")) +
                              " native_r=" + Format((float)Read(type, claim, "NativeRadius")) +
                              " suppressed=" + Read(type, claim, "Suppressed") +
                              " code=" + Read(type, claim, "RefusalCode"));
                }

                var snapshot = string.Join("|", lines.ToArray());
                if (snapshot == _lastSnapshot) return;
                _lastSnapshot = snapshot;

                foreach (var line in lines) Logger.LogInfo(line);
                Logger.LogInfo("c02 claims total=" + lines.Count);
            }
            catch (Exception e)
            {
                Logger.LogWarning("c02 poll failed: " + (e.InnerException ?? e).Message);
            }
        }

        private void RunCommandFile()
        {
            string text;
            try
            {
                if (!File.Exists(CommandFile)) return;
                text = File.ReadAllText(CommandFile).Trim();
            }
            catch (Exception e)
            {
                Logger.LogWarning("c02 command file unreadable: " + e.Message);
                return;
            }

            if (string.IsNullOrEmpty(text) || text == _lastCommands) return;
            _lastCommands = text;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                try { Execute(line); }
                catch (Exception e) { Logger.LogWarning("c02 command '" + line + "' failed: " + (e.InnerException ?? e)); }
            }
        }

        private void Execute(string line)
        {
            Logger.LogInfo("c02 cmd: " + line);
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0].ToLowerInvariant();

            switch (verb)
            {
                case "claims":
                    _lastSnapshot = string.Empty;
                    PollClaims();
                    break;

                case "status":
                    Status();
                    break;

                case "shot":
                    Shot(parts.Length > 1 ? parts[1] : "shot");
                    break;

                case "ring":
                    Ring(parts);
                    break;

                case "preview":
                    Preview(parts);
                    break;

                case "place":
                    Place();
                    break;

                case "exitplace":
                    ExitPlace();
                    break;

                case "hover":
                    Hover();
                    break;

                case "face":
                    Face(parts);
                    break;

                default:
                    Logger.LogWarning("c02 unknown command: " + line);
                    break;
            }
        }

        private void Status()
        {
            if (_diagnosticsType == null) { Logger.LogWarning("c02 no diagnostics type"); return; }
            var lines = _diagnosticsType.GetMethod("Lines", Static)?.Invoke(null, null) as IList;
            if (lines == null) { Logger.LogWarning("c02 Diagnostics.Lines() unreadable"); return; }
            foreach (var line in lines) Logger.LogInfo("c02 status " + line);
            Logger.LogInfo("c02 status lines=" + lines.Count);
        }

        private void Shot(string name)
        {
            Directory.CreateDirectory(CaptureDir);
            var path = Path.Combine(CaptureDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Logger.LogInfo("c02 shot " + path);
        }

        private void Ring(string[] parts)
        {
            if (_ringType == null) { Logger.LogWarning("c02 no ring type"); return; }

            float x = 0f, z = 0f, radius = 20f, seconds = 8f, yOffset = 0f;
            if (parts.Length > 2) { float.TryParse(parts[1], out x); float.TryParse(parts[2], out z); }
            if (parts.Length > 3) float.TryParse(parts[3], out radius);
            if (parts.Length > 4) float.TryParse(parts[4], out seconds);
            if (parts.Length > 5) float.TryParse(parts[5], out yOffset);

            var center = new Vector3(x, Player.m_localPlayer.transform.position.y + yOffset, z);
            var showFor = _ringType.GetMethod("ShowFor", Static);
            showFor?.Invoke(null, new object[] { center, radius, new Color(0.55f, 0.95f, 0.55f, 0.85f), seconds });
            Logger.LogInfo("c02 ring center=" + FormatPoint(center) + " r=" + Format(radius));
        }

        private void Preview(string[] parts)
        {
            if (_previewType == null) { Logger.LogWarning("c02 no preview type"); return; }

            float x = 0f, z = 0f, radius = 0f;
            long playerId = 0L;
            if (parts.Length > 2) { float.TryParse(parts[1], out x); float.TryParse(parts[2], out z); }
            if (parts.Length > 3) float.TryParse(parts[3], out radius);
            if (parts.Length > 4) long.TryParse(parts[4], out playerId);

            var evaluate = _previewType.GetMethod("Evaluate", Static);
            var result = evaluate?.Invoke(null, new object[] { new Vector3(x, 0f, z), playerId, radius });
            Logger.LogInfo("c02 preview " + (result ?? "<none>"));
        }

        private void Place()
        {
            var player = Player.m_localPlayer;
            if (player == null) { Logger.LogWarning("c02 place: no player"); return; }

            var ghostField = typeof(Player).GetField("m_placementGhost", BindingFlags.Instance | BindingFlags.NonPublic);
            if (ghostField == null) { Logger.LogWarning("c02 place: no ghost field"); return; }

            // The piece table's available list is filtered by known recipes, and a test character has
            // not learned the bowl's recipe. The placement preview only reads m_placementGhost, so
            // this puts a real bowl prefab there and lets the game position it exactly as it would
            // for a player holding the hammer. Probe-only shortcut; the mod's patch is untouched.
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("husnisse_bowl") : null;
            if (prefab == null) { Logger.LogWarning("c02 place: no husnisse_bowl prefab"); return; }

            var old = ghostField.GetValue(player) as GameObject;
            if (old != null) UnityEngine.Object.Destroy(old);

            var ghost = UnityEngine.Object.Instantiate(prefab);
            ghost.name = "husnisse_bowl";
            ghost.SetActive(true);
            ghostField.SetValue(player, ghost);

            player.SetMouseLookForward();
            player.SetMouseLook(new Vector2(0f, 25f));
            player.FaceLookDirection();
            Logger.LogInfo("c02 place ghost=" + ghost.transform.position.ToString("F2") + " active=" + ghost.activeSelf);
        }

        private void ExitPlace()
        {
            var player = Player.m_localPlayer;
            var ghostField = typeof(Player).GetField("m_placementGhost", BindingFlags.Instance | BindingFlags.NonPublic);
            var ghost = ghostField != null ? ghostField.GetValue(player) as GameObject : null;
            if (ghost != null) UnityEngine.Object.Destroy(ghost);
            ghostField?.SetValue(player, null);
            if (_ringType != null) _ringType.GetMethod("Hide", Static)?.Invoke(null, null);
            Logger.LogInfo("c02 exitplace");
        }

        private void Hover()
        {
            var player = Player.m_localPlayer;
            var bowlType = Type.GetType("Husnisse.Bowl.BowlPiece, Husnisse");
            if (bowlType == null) { Logger.LogWarning("c02 no BowlPiece type"); return; }

            BowlMatch closest = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType(bowlType))
            {
                var component = candidate as Component;
                if (component == null) continue;

                var distance = Vector3.Distance(component.transform.position, player.transform.position);
                if (closest == null || distance < closest.Distance)
                {
                    closest = new BowlMatch { Component = component, Distance = distance };
                }
            }

            if (closest == null) { Logger.LogWarning("c02 no bowl in scene"); return; }

            var hover = bowlType.GetMethod("HoverText", Instance);
            var text = hover?.Invoke(closest.Component, null) as string;
            Logger.LogInfo("c02 hover bowl=" + closest.Component.transform.position.ToString("F2") +
                           " distance=" + Format(closest.Distance) +
                           " text='" + (text ?? "<null>").Replace("\n", " / ") + "'");
        }

        private void Face(string[] parts)
        {
            if (parts.Length < 3) { Logger.LogWarning("c02 usage: face <x> <z> [pitch]"); return; }

            float x, z, pitch = 18f;
            if (!float.TryParse(parts[1], out x) || !float.TryParse(parts[2], out z)) return;
            if (parts.Length > 3) float.TryParse(parts[3], out pitch);

            var player = Player.m_localPlayer;
            var target = new Vector3(x, player.transform.position.y, z);
            var direction = target - player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                player.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            player.SetMouseLookForward();
            player.SetMouseLook(new Vector2(0f, pitch));
            player.FaceLookDirection();
            Logger.LogInfo("c02 face " + x + "," + z + " pitch=" + pitch +
                           " yaw=" + player.transform.eulerAngles.y.ToString("0"));
        }

        private static object Read(Type type, object instance, string property)
        {
            var info = type.GetProperty(property, Instance);
            return info != null ? info.GetValue(instance, null) : null;
        }

        private static string Format(float value)
        {
            return value.ToString("0.#");
        }

        private static string FormatPoint(Vector3 point)
        {
            return point.x.ToString("0.#") + "," + point.z.ToString("0.#");
        }

        private sealed class BowlMatch
        {
            public Component Component;
            public float Distance;
        }
    }
}
