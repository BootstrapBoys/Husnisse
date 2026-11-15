using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace Husnisse.LegsClient
{
    /// <summary>
    /// Test-only client observer for the 0.1.4 visual gate. It watches the replicated nisse in the
    /// client process, not the server's: frame-by-frame position steps, the animator's locomotion
    /// blend, and the NisseFade alpha the fade RPC drives. Screenshots are taken while the creature
    /// moves or fades, so the walk and the threshold fade are photographed at the moment they
    /// happen rather than at whatever moment a human asked.
    ///
    /// Commands arrive in %USERPROFILE%\husnisse-probe.cmd, one per line:
    ///   teleport &lt;x&gt; &lt;y&gt; &lt;z&gt;
    ///   face &lt;x&gt; &lt;z&gt; [pitch]
    ///   shot &lt;name&gt;
    ///   watch &lt;seconds&gt; [minShotInterval]
    ///   where
    ///   hud [count]      dump the last HUD lines MessageHud actually showed
    ///   revive [off]     god mode on (default) or off, and respawn if dead
    /// Never shipped; lives in probe/.
    /// </summary>
    [BepInPlugin(Guid, "Husnisse Legs Client Probe", "0.1.0")]
    public class LegsClientPlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.husnisse.legsclient";

        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

        private static readonly string CommandFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "husnisse-probe.cmd");

        private static readonly string CaptureDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "husnisse-captures");

        private Type _fadeType;
        private Type _actorType;
        private Type _claimRegistryType;

        private string _lastCommands = string.Empty;
        private float _commandTimer;
        private bool _watching;

        private int _hudSeen = -1;
        private string _hudText = string.Empty;
        private static FieldInfo _messageTextField;
        private static PropertyInfo _textProperty;

        private void Awake()
        {
            Logger.LogInfo("lc loaded " + Info.Metadata.Version);
            StartCoroutine(HudWatch());
            StartCoroutine(Run());
        }

        // ------------------------------------------------------------------ HUD

        /// <summary>
        /// The HUD is where a "message" claim is decided, and a screenshot alone cannot prove the
        /// absence of one. MessageHud keeps every shown line in m_messageLog (GetLog()) and the
        /// TopLeft text currently on screen in m_messageText, so both are polled and logged: a line
        /// the server sends either shows up here or does not.
        /// </summary>
        private IEnumerator HudWatch()
        {
            while (true)
            {
                try { PollHud(); }
                catch (Exception e) { Logger.LogWarning("lc hud poll failed: " + e.Message); }
                yield return null;
            }
        }

        private void PollHud()
        {
            var hud = MessageHud.instance;
            if (hud == null) return;

            if (_hudSeen < 0)
            {
                _hudSeen = hud.GetLog().Count;
                Logger.LogInfo("lc hud baseline entries=" + _hudSeen + " text=\"" + HudText(hud) + "\"");
            }

            var text = HudText(hud);
            if (text != _hudText)
            {
                _hudText = text;
                if (!string.IsNullOrEmpty(text)) Logger.LogInfo("lc hud text \"" + text + "\"");
            }

            var log = hud.GetLog();
            if (log.Count < _hudSeen) _hudSeen = log.Count;
            while (_hudSeen < log.Count)
            {
                Logger.LogInfo("lc hud msg \"" + log[_hudSeen] + "\"");
                _hudSeen++;
            }
        }

        /// <summary>
        /// The TopLeft line currently on screen. Reflected rather than referenced: the field is a
        /// TMP_Text, and referencing TextMeshPro from the probe drags in the whole UI assembly chain
        /// for one string.
        /// </summary>
        private static string HudText(MessageHud hud)
        {
            try
            {
                if (_messageTextField == null)
                    _messageTextField = typeof(MessageHud).GetField("m_messageText", Instance);
                if (_messageTextField == null) return string.Empty;

                var label = _messageTextField.GetValue(hud);
                if (label == null) return string.Empty;

                if (_textProperty == null) _textProperty = label.GetType().GetProperty("text", Instance);
                return _textProperty != null ? (_textProperty.GetValue(label, null) as string) ?? string.Empty : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void HudDump(string[] parts)
        {
            var hud = MessageHud.instance;
            if (hud == null) { Logger.LogWarning("lc hud: no MessageHud"); return; }

            var count = 10;
            if (parts.Length > 1) int.TryParse(parts[1], out count);

            var log = hud.GetLog();
            var from = Mathf.Max(0, log.Count - count);
            Logger.LogInfo("lc hud dump entries=" + log.Count + " hidden=" + Hud.IsUserHidden() +
                           " text=\"" + HudText(hud) + "\"");
            for (var i = from; i < log.Count; i++) Logger.LogInfo("lc hud[" + i + "] \"" + log[i] + "\"");
        }

        private IEnumerator Run()
        {
            var waited = 0f;
            while (waited < 300f)
            {
                if (Player.m_localPlayer != null && ZNetScene.instance != null &&
                    ZNet.instance != null && !ZNet.instance.IsServer())
                    break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            var player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null || ZNet.instance.IsServer())
            {
                Logger.LogWarning("lc no client player; aborting");
                yield break;
            }

            _fadeType = Type.GetType("Husnisse.Creature.NisseFade, Husnisse");
            _actorType = Type.GetType("Husnisse.Creature.NisseActor, Husnisse");
            _claimRegistryType = Type.GetType("Husnisse.Claims.ClaimRegistry, Husnisse");

            Logger.LogInfo("lc ready player=" + player.GetPlayerName() +
                           " pos=" + player.transform.position.ToString("F2") +
                           " world=" + (ZNet.instance.GetWorldName() ?? "?") +
                           " hudHidden=" + Hud.IsUserHidden() +
                           " types fade=" + (_fadeType != null) + " actor=" + (_actorType != null));
            Logger.LogInfo("lc command_file=" + CommandFile);

            while (true)
            {
                _commandTimer += Time.deltaTime;
                if (_commandTimer >= 0.5f && !_watching)
                {
                    _commandTimer = 0f;
                    RunCommandFile();
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ commands

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
                Logger.LogWarning("lc command file unreadable: " + e.Message);
                return;
            }

            if (string.IsNullOrEmpty(text) || text == _lastCommands) return;
            _lastCommands = text;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                try { Execute(line); }
                catch (Exception e) { Logger.LogWarning("lc command '" + line + "' failed: " + (e.InnerException ?? e)); }
            }
        }

        private void Execute(string line)
        {
            Logger.LogInfo("lc cmd: " + line);
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0].ToLowerInvariant();

            switch (verb)
            {
                case "teleport": Teleport(parts); break;
                case "face": Face(parts); break;
                case "cam": Cam(parts); break;
                case "shot": Shot(parts.Length > 1 ? parts[1] : "lc-shot"); break;
                case "watch": StartWatch(parts); break;
                case "where": Where(); break;
                case "claims": Claims(); break;
                case "hud": HudDump(parts); break;
                case "revive": Revive(parts); break;
                default: Logger.LogWarning("lc unknown command: " + line); break;
            }
        }

        /// <summary>
        /// Keep the observer's character out of the death screen: a world spawn on a mountain kills
        /// an idle client with cold in a couple of minutes, and a dead player cannot be teleported
        /// or photographed. God mode is the least invasive way to hold the client still.
        /// </summary>
        private void Revive(string[] parts)
        {
            var player = Player.m_localPlayer;
            if (player == null) { Logger.LogWarning("lc revive: no player"); return; }

            var god = parts.Length > 1 ? parts[1].ToLowerInvariant() != "off" : true;
            player.SetGodMode(god);

            Logger.LogInfo("lc revive god=" + god + " dead=" + player.IsDead() +
                           " health=" + player.GetHealth().ToString("0") +
                           " pos=" + player.transform.position.ToString("F2"));

            if (player.IsDead() && Game.instance != null)
            {
                Game.instance.RequestRespawn(0f);
                Logger.LogInfo("lc revive requested respawn");
            }
        }

        private void Teleport(string[] parts)
        {
            var player = Player.m_localPlayer;
            if (player == null) { Logger.LogWarning("lc teleport: no player"); return; }
            if (parts.Length < 4) { Logger.LogWarning("lc usage: teleport <x> <y> <z>"); return; }

            float x, y, z;
            if (!float.TryParse(parts[1], out x) || !float.TryParse(parts[2], out y) || !float.TryParse(parts[3], out z))
            {
                Logger.LogWarning("lc teleport: bad coordinates");
                return;
            }

            var from = player.transform.position;
            var target = new Vector3(x, y, z);
            player.TeleportTo(target, Quaternion.identity, true);

            var body = player.GetComponent<Rigidbody>();
            if (body != null) body.velocity = Vector3.zero;

            Logger.LogInfo("lc teleport from=" + from.ToString("F2") + " to=" + player.transform.position.ToString("F2") +
                           " requested=" + target.ToString("F2"));
        }

        private void Face(string[] parts)
        {
            if (parts.Length < 3) { Logger.LogWarning("lc usage: face <x> <z> [pitch]"); return; }

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
            Logger.LogInfo("lc face " + x + "," + z + " pitch=" + pitch +
                           " yaw=" + player.transform.eulerAngles.y.ToString("0"));
        }

        /// <summary>
        /// A fixed camera: the game camera follows the player's look direction, which drifts, so a
        /// shot framed by `face` is only roughly aimed. This parks the render camera at a chosen
        /// point looking at a chosen target, and the player's own view stops fighting it.
        /// </summary>
        private void Cam(string[] parts)
        {
            if (parts.Length < 7)
            {
                Logger.LogWarning("lc usage: cam <x> <y> <z> <tx> <ty> <tz>");
                return;
            }

            float x, y, z, tx, ty, tz;
            if (!float.TryParse(parts[1], out x) || !float.TryParse(parts[2], out y) || !float.TryParse(parts[3], out z) ||
                !float.TryParse(parts[4], out tx) || !float.TryParse(parts[5], out ty) || !float.TryParse(parts[6], out tz))
            {
                Logger.LogWarning("lc cam: bad coordinates");
                return;
            }

            var camera = Camera.main;
            if (camera == null) { Logger.LogWarning("lc cam: no main camera"); return; }

            var gameCamera = GameCamera.instance;
            if (gameCamera != null) gameCamera.enabled = false;

            var position = new Vector3(x, y, z);
            var target = new Vector3(tx, ty, tz);
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);

            Logger.LogInfo("lc cam pos=" + position.ToString("F2") + " target=" + target.ToString("F2"));
        }

        private void Where()
        {
            var player = Player.m_localPlayer;
            if (player == null) { Logger.LogWarning("lc where: no player"); return; }
            var nisse = FindNisse();
            Logger.LogInfo("lc where player=" + player.transform.position.ToString("F2") +
                           " yaw=" + player.transform.eulerAngles.y.ToString("0") +
                           " nisse=" + (nisse != null ? CharacterPosition(nisse).ToString("F2") : "-") +
                           " peers=" + (ZNet.instance != null ? ZNet.instance.GetPeers().Count : -1));
        }

        private void Claims()
        {
            if (_claimRegistryType == null) { Logger.LogWarning("lc no ClaimRegistry type"); return; }
            try
            {
                _claimRegistryType.GetMethod("RevalidateAll", Static)?.Invoke(null, null);
                var all = _claimRegistryType.GetMethod("All", Static)?.Invoke(null, null) as IList;
                if (all == null) { Logger.LogWarning("lc ClaimRegistry.All() unreadable"); return; }

                foreach (var claim in all)
                {
                    if (claim == null) continue;
                    var type = claim.GetType();
                    Logger.LogInfo("lc claim " + Read(type, claim, "BowlId") +
                                   " source=" + Read(type, claim, "SourceName") +
                                   " r=" + Read(type, claim, "Radius") +
                                   " suppressed=" + Read(type, claim, "Suppressed"));
                }
                Logger.LogInfo("lc claims total=" + all.Count);
            }
            catch (Exception e)
            {
                Logger.LogWarning("lc claims failed: " + (e.InnerException ?? e).Message);
            }
        }

        // ------------------------------------------------------------------ watch

        private void StartWatch(string[] parts)
        {
            var seconds = 60f;
            var minShot = 1.2f;
            if (parts.Length > 1) float.TryParse(parts[1], out seconds);
            if (parts.Length > 2) float.TryParse(parts[2], out minShot);

            if (_watching) { Logger.LogWarning("lc watch already running"); return; }
            StartCoroutine(WatchRoutine(seconds, minShot));
        }

        private IEnumerator WatchRoutine(float seconds, float minShot)
        {
            _watching = true;
            Logger.LogInfo("lc watch start seconds=" + seconds.ToString("0") + " minShot=" + minShot.ToString("0.0"));

            Character nisse = null;
            var lastPos = Vector3.zero;
            var haveLast = false;
            var elapsed = 0f;
            var secondStep = 0f;
            var frames = 0;
            var shotTimer = 0f;
            var maxStep = 0f;
            var secondMaxStep = 0f;
            var maxAnim = 0f;
            var fades = 0;
            var shots = 0;
            var prevAlpha = 1f;
            var minAlpha = 1f;

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                secondStep += Time.deltaTime;
                frames++;

                if (nisse == null) nisse = FindNisse();
                if (nisse != null)
                {
                    var pos = CharacterPosition(nisse);
                    if (!haveLast)
                    {
                        lastPos = pos;
                        haveLast = true;
                    }

                    var step = Horizontal(lastPos, pos);
                    lastPos = pos;
                    if (step > maxStep) maxStep = step;
                    if (step > secondMaxStep) secondMaxStep = step;

                    var anim = AnimatorSpeed(nisse);
                    if (anim > maxAnim) maxAnim = anim;

                    var alpha = 1f;
                    var busy = false;
                    if (_fadeType != null)
                    {
                        var fade = nisse.GetComponent(_fadeType);
                        if (fade != null)
                        {
                            alpha = (float)Read(_fadeType, fade, "Alpha");
                            busy = (bool)Read(_fadeType, fade, "Busy");
                        }
                    }

                    if (alpha < minAlpha) minAlpha = alpha;

                    if (prevAlpha >= 0.995f && alpha < 0.99f)
                    {
                        fades++;
                        Logger.LogInfo("lc fade out start t=" + elapsed.ToString("0.00") +
                                       " pos=" + pos.ToString("F2") + " alpha=" + alpha.ToString("0.00"));
                        // Catch the body mid-fade, not only before and after it.
                        TakeShot("lc-fade-" + fades + "-a", ref shotTimer, minShot, ref shots);
                    }
                    else if (alpha > prevAlpha + 0.15f && prevAlpha < 0.9f)
                    {
                        Logger.LogInfo("lc fade in t=" + elapsed.ToString("0.00") +
                                       " pos=" + pos.ToString("F2") + " alpha=" + alpha.ToString("0.00"));
                        TakeShot("lc-fade-" + fades + "-b", ref shotTimer, minShot, ref shots);
                    }
                    prevAlpha = alpha;

                    var moving = step > 0.005f || anim > 0.05f;
                    if (moving || busy || alpha < 0.98f)
                    {
                        TakeShot("lc-walk", ref shotTimer, minShot, ref shots);
                    }

                    if (secondStep >= 1f)
                    {
                        Logger.LogInfo("lc t=" + elapsed.ToString("0") +
                                       " pos=" + pos.ToString("F2") +
                                       " step_sum=" + secondMaxStep.ToString("0.00") +
                                       " anim=" + anim.ToString("0.00") +
                                       " alpha=" + alpha.ToString("0.00") +
                                       " busy=" + busy +
                                       " frames=" + frames);
                        secondStep = 0f;
                        secondMaxStep = 0f;
                    }
                }
                else if (secondStep >= 1f)
                {
                    Logger.LogInfo("lc t=" + elapsed.ToString("0") + " nisse=-");
                    secondStep = 0f;
                }

                yield return null;
            }

            Logger.LogInfo("lc watch done seconds=" + elapsed.ToString("0") +
                           " frames=" + frames +
                           " max_step=" + maxStep.ToString("0.000") +
                           " max_anim=" + maxAnim.ToString("0.00") +
                           " min_alpha=" + minAlpha.ToString("0.00") +
                           " fades=" + fades +
                           " shots=" + shots);
            _watching = false;
        }

        private void TakeShot(string name, ref float timer, float minShot, ref int shots)
        {
            if (timer > 0f) { timer -= Time.deltaTime; return; }
            timer = minShot;
            shots++;
            Shot(name + "-" + shots.ToString("00"));
        }

        // ------------------------------------------------------------------ helpers

        private Character FindNisse()
        {
            var all = Character.GetAllCharacters();
            for (var i = 0; i < all.Count; i++)
            {
                var character = all[i];
                if (character == null) continue;
                if (character.gameObject.name.ToLowerInvariant().Contains("husnisse")) return character;
            }

            if (_actorType == null) return null;
            var actors = UnityEngine.Object.FindObjectsByType(_actorType, FindObjectsSortMode.None);
            return actors.Length > 0 ? actors[0] as Character : null;
        }

        private static Vector3 CharacterPosition(Character character)
        {
            var visual = character.GetVisual();
            return visual != null ? visual.transform.position : character.transform.position;
        }

        /// <summary>The Greyling controller blends locomotion on forward_speed; a non-zero value is
        /// the animation actually playing, not a body sliding along a path.</summary>
        private static float AnimatorSpeed(Character character)
        {
            try
            {
                var visual = character.GetVisual();
                if (visual == null) return 0f;
                var animator = visual.GetComponentInChildren<Animator>();
                if (animator == null) return 0f;
                return Mathf.Abs(animator.GetFloat("forward_speed"));
            }
            catch
            {
                return 0f;
            }
        }

        private void Shot(string name)
        {
            Directory.CreateDirectory(CaptureDir);
            var path = Path.Combine(CaptureDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Logger.LogInfo("lc shot " + path);
        }

        private static object Read(Type type, object instance, string property)
        {
            var info = type.GetProperty(property, Instance);
            return info != null ? info.GetValue(instance, null) : null;
        }

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
