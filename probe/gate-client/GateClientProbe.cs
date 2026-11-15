using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace Husnisse.GateClient
{
    /// <summary>
    /// Test-only client driver for the Husnisse gate: drain, per-bowl Alt feeding, order RPC round
    /// trip, invulnerability under a raid, relog persistence.
    ///
    /// It calls the same entry points the game calls — Container.Interact for the bowl and for Alt
    /// on a chest, the Interactable components on the nisse — and reads the mod's own ZDO state and
    /// hover text back in the client process, so every line is what a player would see rather than
    /// what the server decided. HUD lines are sampled from MessageHud because they never reach a log.
    ///
    /// Commands arrive in %USERPROFILE%\husnisse-probe.cmd, one per line:
    ///   state | containers | claims | inv | where
    ///   hover &lt;prefab&gt;
    ///   use &lt;prefab&gt;            (plain interact)
    ///   alt &lt;prefab&gt;            (Alt-interact)
    ///   open &lt;prefab&gt;           (plain interact, then report the container GUI)
    ///   close
    ///   give &lt;item&gt; &lt;amount&gt; [slotY]
    ///   move &lt;prefab&gt; &lt;item&gt; &lt;amount&gt;    player -> container
    ///   take &lt;prefab&gt; &lt;item&gt; &lt;amount&gt;    container -> player
    ///   watch &lt;seconds&gt; [interval]
    ///   shot &lt;name&gt;
    /// Never shipped; lives in probe/.
    /// </summary>
    [BepInPlugin(Guid, "Husnisse Gate Client Probe", "0.1.0")]
    public class GateClientPlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.husnisse.gateclient";

        private static readonly string CommandFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "husnisse-probe.cmd");

        private static readonly string CaptureDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "husnisse-captures");

        private string _lastCommands = string.Empty;
        private float _commandTimer;
        private bool _busy;
        private string _lastHudCenter = string.Empty;
        private string _lastHudTop = string.Empty;

        private void Awake()
        {
            Logger.LogInfo("gc loaded " + Info.Metadata.Version);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var waited = 0f;
            while (waited < 600f)
            {
                var player = Player.m_localPlayer;
                if (player != null && ZNetScene.instance != null && ZNet.instance != null && !ZNet.instance.IsServer())
                {
                    var pos = player.transform.position;
                    if (Math.Abs(pos.x) < 500000f && Math.Abs(pos.z) < 500000f) break;
                }
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            var local = Player.m_localPlayer;
            if (local == null || ZNet.instance == null || ZNet.instance.IsServer())
            {
                Logger.LogWarning("gc no client player; aborting");
                yield break;
            }

            Logger.LogInfo("gc ready player=" + local.GetPlayerName() +
                           " id=" + local.GetPlayerID() +
                           " pos=" + Fmt(local.transform.position) +
                           " world=" + (ZNet.instance.GetWorldName() ?? "?") +
                           " peers=" + ZNet.instance.GetPeers().Count);
            Logger.LogInfo("gc command_file=" + CommandFile);

            while (true)
            {
                PollHud();
                _commandTimer += Time.deltaTime;
                if (_commandTimer >= 0.5f && !_busy)
                {
                    _commandTimer = 0f;
                    RunCommandFile();
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ HUD

        /// <summary>
        /// MessageHud lines are drawn, not logged. Sample the two fields text lands in and log a
        /// change, which is the only client-side evidence that a centre or top-left line was shown.
        /// </summary>
        private void PollHud()
        {
            var hud = MessageHud.instance;
            if (hud == null) return;

            var centre = TextOf(hud.m_messageCenterText);
            if (centre != null && centre.Length > 0 && centre != _lastHudCenter)
            {
                _lastHudCenter = centre;
                Say("hud center='" + centre.Replace("\n", " | ") + "'");
            }

            var top = TextOf(hud.m_messageText);
            if (top != null && top.Length > 0 && top != _lastHudTop)
            {
                _lastHudTop = top;
                Say("hud top='" + top.Replace("\n", " | ") + "'");
            }
        }

        private static string TextOf(object tmpText)
        {
            if (tmpText == null) return null;
            try
            {
                var property = tmpText.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                return property != null ? property.GetValue(tmpText, null) as string : null;
            }
            catch { return null; }
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
                Logger.LogWarning("gc command file unreadable: " + e.Message);
                return;
            }

            if (string.IsNullOrEmpty(text) || text == _lastCommands) return;
            _lastCommands = text;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                try { Execute(line); }
                catch (Exception e) { Logger.LogWarning("gc command '" + line + "' failed: " + (e.InnerException ?? e)); }
            }
        }

        private void Execute(string line)
        {
            Say("cmd: " + line);
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0].ToLowerInvariant();

            switch (verb)
            {
                case "state": LogState("state"); break;
                case "containers": Containers(); break;
                case "claims": Claims(); break;
                case "inv": Say("inv " + Items(Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null)); break;
                case "where": Where(); break;
                case "hover": Hover(parts.Length > 1 ? parts[1] : "husnisse_bowl"); break;
                case "use": Use(parts.Length > 1 ? parts[1] : "husnisse_bowl", false); break;
                case "alt": Use(parts.Length > 1 ? parts[1] : "husnisse_bowl", true); break;
                case "open": StartOpen(parts.Length > 1 ? parts[1] : "husnisse_bowl"); break;
                case "close": CloseGui(); break;
                case "give": Give(parts); break;
                case "move": Move(parts, true); break;
                case "take": Move(parts, false); break;
                case "watch": StartWatch(parts); break;
                case "shot": Shot(parts.Length > 1 ? parts[1] : "gc-shot"); break;
                case "cam": Cam(parts); break;
                case "face": Face(parts); break;
                case "teleport": Teleport(parts); break;
                case "god": God(parts); break;
                case "heal": Heal(); break;
                case "quit": Quit(); break;
                default: Logger.LogWarning("gc unknown command: " + line); break;
            }
        }

        // ------------------------------------------------------------------ readings

        private void LogState(string prefix)
        {
            var player = Player.m_localPlayer;
            if (player == null) { Say(prefix + " no player"); return; }

            Say(prefix + " player=" + player.GetPlayerName() + " id=" + player.GetPlayerID() +
                " pos=" + Fmt(player.transform.position) +
                " nisse=" + (FindNisse() != null ? "present" : "absent"));

            var bowl = FindContainer("husnisse_bowl");
            if (bowl == null)
            {
                Say(prefix + " bowl=-");
                return;
            }

            var view = bowl.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo != null)
            {
                Say(prefix + " bowl feeding=" + zdo.GetBool("husnisse_bowl_feeding", false) +
                    " satiety=" + zdo.GetFloat("husnisse_bowl_satiety", -1f).ToString("0.##") +
                    " mood=" + zdo.GetFloat("husnisse_bowl_mood", -1f).ToString("0.##") +
                    " ownerZdo=" + zdo.GetLong("husnisse_bowl_owner", -1L) +
                    " authority=" + zdo.GetString("husnisse_claim_authority", "-") +
                    " claimRadius=" + zdo.GetFloat("husnisse_claim_radius", -1f).ToString("0.##") +
                    " claimOwner=" + zdo.GetLong("husnisse_claim_owner", -1L) +
                    " localZdoOwner=" + (view != null && view.IsOwner()));
            }
            Say(prefix + " pantry " + Items(bowl.GetInventory()));
            Say(prefix + " hover='" + Flatten(bowl.GetHoverText()) + "'");

            var nisse = FindNisse();
            if (nisse != null)
            {
                Say(prefix + " nisse hp=" + nisse.GetHealth().ToString("0.#") + "/" + nisse.GetMaxHealth().ToString("0.#") +
                    " pos=" + Fmt(nisse.transform.position));
            }
            else
            {
                Say(prefix + " nisse=-");
            }
        }

        private void Where()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var nisse = FindNisse();
            Say("where player=" + Fmt(player.transform.position) +
                " nisse=" + (nisse != null ? Fmt(nisse.transform.position) : "-") +
                " peers=" + (ZNet.instance != null ? ZNet.instance.GetPeers().Count : -1));
        }

        private void Hover(string prefab)
        {
            var container = FindContainer(prefab);
            if (container != null)
            {
                Say("hover " + container.name + "='" + Flatten(container.GetHoverText()) + "'");
                return;
            }

            var nisse = FindNisse();
            if (nisse != null && prefab.StartsWith("husnisse", StringComparison.OrdinalIgnoreCase))
            {
                var hoverable = nisse.GetComponent<Hoverable>();
                Say("hover nisse='" + (hoverable != null ? Flatten(hoverable.GetHoverText()) : "-") + "'");
                return;
            }

            Say("hover: no object for " + prefab);
        }

        private void Containers()
        {
            var all = UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None);
            var count = 0;
            foreach (var container in all)
            {
                if (container == null) continue;
                if (container.GetComponent("BowlPiece") != null) continue;
                count++;
                Say("container " + container.name + " at " + Fmt(container.transform.position) + " " + Items(container.GetInventory()));
            }
            Say("containers total=" + count);
        }

        private void Claims()
        {
            var type = Type.GetType("Husnisse.Claims.ClaimRegistry, Husnisse");
            if (type == null) { Say("claims: mod types not loaded"); return; }

            try
            {
                var all = type.GetMethod("All", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null) as IList;
                if (all == null) { Say("claims: ClaimRegistry.All() unreadable"); return; }

                foreach (var claim in all)
                {
                    if (claim == null) continue;
                    var ct = claim.GetType();
                    Say("claim " + Read(ct, claim, "BowlId") +
                        " source=" + Read(ct, claim, "SourceName") +
                        " owner=" + Read(ct, claim, "OwnerId") +
                        " r=" + Read(ct, claim, "Radius") +
                        " suppressed=" + Read(ct, claim, "Suppressed"));
                }
                Say("claims total=" + all.Count);
            }
            catch (Exception e)
            {
                Say("claims failed: " + (e.InnerException ?? e).Message);
            }
        }

        // ------------------------------------------------------------------ interaction

        private void Use(string prefab, bool alt)
        {
            var player = Player.m_localPlayer;
            if (player == null) { Say("use: no player"); return; }

            var container = FindContainer(prefab);
            if (container != null)
            {
                bool result;
                try { result = container.Interact(player, false, alt); }
                catch (Exception e) { Say("interact " + container.name + " threw " + (e.InnerException ?? e).Message); return; }
                Say("interact " + container.name + " alt=" + alt + " -> " + result);
                return;
            }

            var nisse = FindNisse();
            if (nisse != null && prefab.StartsWith("husnisse", StringComparison.OrdinalIgnoreCase))
            {
                var handlers = nisse.GetComponentsInChildren<MonoBehaviour>(true).OfType<Interactable>().ToList();
                if (handlers.Count == 0) { Say("interact nisse: no Interactable"); return; }
                foreach (var handler in handlers)
                {
                    bool result;
                    try { result = handler.Interact(player, false, alt); }
                    catch (Exception e) { Say("interact " + handler.GetType().Name + " threw " + (e.InnerException ?? e).Message); continue; }
                    Say("interact " + handler.GetType().Name + " alt=" + alt + " -> " + result);
                }
                return;
            }

            Say("use: no object for " + prefab);
        }

        private void StartOpen(string prefab)
        {
            Use(prefab, false);
            if (!_busy) StartCoroutine(AfterOpen());
        }

        private IEnumerator AfterOpen()
        {
            _busy = true;
            yield return new WaitForSeconds(2f);

            var current = GuiContainer();
            if (current == null)
            {
                Say("open gui=none");
            }
            else
            {
                Say("open gui=" + current.name + " " + Items(current.GetInventory()) +
                    " inUse=" + current.IsInUse() + " owner=" + current.IsOwner());
            }

            _busy = false;
        }

        private void CloseGui()
        {
            var gui = InventoryGui.instance;
            if (gui == null) { Say("close: no InventoryGui"); return; }
            var was = GuiContainer();
            gui.Hide();
            Say("close was=" + (was != null ? was.name : "none"));
            StartCoroutine(AfterClose());
        }

        private IEnumerator AfterClose()
        {
            _busy = true;
            yield return new WaitForSeconds(1.5f);
            var bowl = FindContainer("husnisse_bowl");
            if (bowl != null)
            {
                var view = bowl.GetComponent<ZNetView>();
                var current = GuiContainer();
                Say("close after: gui=" + (current != null ? current.name : "none") +
                    " localZdoOwner=" + (view != null && view.IsOwner()) +
                    " " + Items(bowl.GetInventory()));
            }
            _busy = false;
        }

        /// <summary>InventoryGui.m_currentContainer is private; the open container is read by field.</summary>
        private static Container GuiContainer()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return null;
            var field = typeof(InventoryGui).GetField("m_currentContainer",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? field.GetValue(gui) as Container : null;
        }

        private void Give(string[] parts)
        {
            if (parts.Length < 3) { Say("usage: give <item> <amount> [slotY]"); return; }

            var player = Player.m_localPlayer;
            var inventory = player != null ? player.GetInventory() : null;
            if (inventory == null) { Say("give: no inventory"); return; }

            var amount = ParseInt(parts[2], 1);
            var slotY = parts.Length > 3 ? ParseInt(parts[3], 1) : 1;
            var item = inventory.AddItem(parts[1], amount, 1, 0, 0L, string.Empty,
                                         new Vector2i(0, slotY), false, false, false);
            Say("give " + parts[1] + "x" + amount + " slot=(0," + slotY + ") item=" + (item != null) +
                " " + Items(inventory));
        }

        private void Move(string[] parts, bool intoContainer)
        {
            if (parts.Length < 4) { Say("usage: move|take <prefab> <item> <amount>"); return; }

            var player = Player.m_localPlayer;
            var container = FindContainer(parts[1]);
            if (player == null || container == null) { Say("move: no player or container " + parts[1]); return; }

            var containerInventory = container.GetInventory();
            var playerInventory = player.GetInventory();
            if (containerInventory == null || playerInventory == null) { Say("move: no inventories"); return; }

            var source = intoContainer ? playerInventory : containerInventory;
            var destination = intoContainer ? containerInventory : playerInventory;

            var item = FindItem(source, parts[2]);
            if (item == null)
            {
                Say("move: '" + parts[2] + "' not in " + (intoContainer ? "player" : "container"));
                return;
            }

            var amount = Mathf.Min(ParseInt(parts[3], 1), item.m_stack);
            var x = -1;
            var y = -1;
            if (intoContainer)
            {
                for (var row = 0; row < containerInventory.GetHeight() && x < 0; row++)
                {
                    for (var col = 0; col < containerInventory.GetWidth(); col++)
                    {
                        if (containerInventory.GetItemAt(col, row) != null) continue;
                        x = col;
                        y = row;
                        break;
                    }
                }
            }

            bool moved;
            try { moved = destination.MoveItemToThis(source, item, amount, x, y); }
            catch (Exception e) { Say("move threw " + (e.InnerException ?? e).Message); return; }

            Say("move " + (intoContainer ? "into" : "out of") + " " + container.name +
                " '" + parts[2] + "'x" + amount + " ok=" + moved +
                " container[" + Items(containerInventory) + "]" +
                " player[" + Items(playerInventory) + "]");
        }

        // ------------------------------------------------------------------ watch

        private void StartWatch(string[] parts)
        {
            var seconds = 30f;
            var interval = 5f;
            if (parts.Length > 1) float.TryParse(parts[1], out seconds);
            if (parts.Length > 2) float.TryParse(parts[2], out interval);

            if (_busy) { Logger.LogWarning("gc watch already running"); return; }
            StartCoroutine(WatchRoutine(seconds, Mathf.Max(0.5f, interval)));
        }

        private IEnumerator WatchRoutine(float seconds, float interval)
        {
            _busy = true;
            Say("watch start seconds=" + seconds.ToString("0") + " interval=" + interval.ToString("0.0"));
            var elapsed = 0f;
            var step = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                step += Time.deltaTime;
                if (step >= interval)
                {
                    step = 0f;
                    LogState("watch t=" + elapsed.ToString("0"));
                }
                yield return null;
            }
            Say("watch done seconds=" + elapsed.ToString("0"));
            _busy = false;
        }

        private void Shot(string name)
        {
            Directory.CreateDirectory(CaptureDir);
            var path = Path.Combine(CaptureDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Say("shot " + path);
        }

        /// <summary>
        /// A graceful quit, not a process kill: Game.OnApplicationQuit saves the character, so the
        /// relog can join with the same character and therefore the same player id. A killed
        /// process loses the character and the relog would silently test a stranger instead.
        /// </summary>
        private void Quit()
        {
            Say("quit requested (character saves on OnApplicationQuit)");
            Application.Quit();
        }

        /// <summary>
        /// A fresh character has no armour and dies to cold or a raid in minutes, which ends a
        /// scene and takes the evidence with it. God mode is the test's seat belt, not the subject.
        /// </summary>
        private void God(string[] parts)
        {
            var player = Player.m_localPlayer;
            if (player == null) { Say("god: no player"); return; }

            var on = parts.Length < 2 || !parts[1].Equals("off", StringComparison.OrdinalIgnoreCase);
            player.SetGodMode(on);
            Say("god=" + player.InGodMode());
        }

        private void Heal()
        {
            var player = Player.m_localPlayer;
            if (player == null) { Say("heal: no player"); return; }
            player.SetHealth(player.GetMaxHealth());
            Say("heal hp=" + player.GetHealth().ToString("0") + "/" + player.GetMaxHealth().ToString("0"));
        }

        private void Teleport(string[] parts)
        {
            if (parts.Length < 4) { Say("usage: teleport <x> <y> <z>"); return; }

            var player = Player.m_localPlayer;
            if (player == null) { Say("teleport: no player"); return; }

            float x, y, z;
            if (!float.TryParse(parts[1], out x) || !float.TryParse(parts[2], out y) || !float.TryParse(parts[3], out z))
            {
                Say("teleport: bad coordinates");
                return;
            }

            var from = player.transform.position;
            var target = new Vector3(x, y, z);
            player.TeleportTo(target, Quaternion.identity, true);

            var body = player.GetComponent<Rigidbody>();
            if (body != null) body.velocity = Vector3.zero;

            Say("teleport from=" + Fmt(from) + " requested=" + Fmt(target) + " now=" + Fmt(player.transform.position));
        }

        /// <summary>
        /// Park the render camera: the game camera follows the player's look and drifts, so a shot
        /// framed by `face` is only roughly aimed. This aims it at a chosen target for a fixed frame.
        /// </summary>
        private void Cam(string[] parts)
        {
            if (parts.Length < 7) { Say("usage: cam <x> <y> <z> <tx> <ty> <tz>"); return; }

            float x, y, z, tx, ty, tz;
            if (!float.TryParse(parts[1], out x) || !float.TryParse(parts[2], out y) || !float.TryParse(parts[3], out z) ||
                !float.TryParse(parts[4], out tx) || !float.TryParse(parts[5], out ty) || !float.TryParse(parts[6], out tz))
            {
                Say("cam: bad coordinates");
                return;
            }

            var camera = Camera.main;
            if (camera == null) { Say("cam: no main camera"); return; }

            var gameCamera = GameCamera.instance;
            if (gameCamera != null) gameCamera.enabled = false;

            var position = new Vector3(x, y, z);
            var target = new Vector3(tx, ty, tz);
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
            Say("cam pos=" + Fmt(position) + " target=" + Fmt(target));
        }

        private void Face(string[] parts)
        {
            if (parts.Length < 3) { Say("usage: face <x> <z> [pitch]"); return; }

            var player = Player.m_localPlayer;
            if (player == null) { Say("face: no player"); return; }

            float x, z, pitch = 18f;
            if (!float.TryParse(parts[1], out x) || !float.TryParse(parts[2], out z)) return;
            if (parts.Length > 3) float.TryParse(parts[3], out pitch);

            var direction = new Vector3(x - player.transform.position.x, 0f, z - player.transform.position.z);
            if (direction.sqrMagnitude > 0.01f)
            {
                player.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            player.SetMouseLookForward();
            player.SetMouseLook(new Vector2(0f, pitch));
            player.FaceLookDirection();
            Say("face " + x + "," + z + " pitch=" + pitch + " yaw=" + player.transform.eulerAngles.y.ToString("0"));
        }

        // ------------------------------------------------------------------ helpers

        private static Container FindContainer(string prefab)
        {
            var player = Player.m_localPlayer;
            var origin = player != null ? player.transform.position : Vector3.zero;

            Container best = null;
            var bestDistance = float.MaxValue;
            foreach (var container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (container == null) continue;
                if (!container.gameObject.name.StartsWith(prefab, StringComparison.OrdinalIgnoreCase)) continue;

                var distance = Vector3.Distance(origin, container.transform.position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = container;
            }
            return best;
        }

        private static Character FindNisse()
        {
            var all = Character.GetAllCharacters();
            for (var i = 0; i < all.Count; i++)
            {
                var character = all[i];
                if (character == null) continue;
                if (character.gameObject.name.IndexOf("husnisse", StringComparison.OrdinalIgnoreCase) >= 0) return character;
            }

            var actorType = Type.GetType("Husnisse.Creature.NisseActor, Husnisse");
            if (actorType == null) return null;
            var actors = UnityEngine.Object.FindObjectsByType(actorType, FindObjectsSortMode.None);
            return actors.Length > 0 ? actors[0] as Character : null;
        }

        private static ItemDrop.ItemData FindItem(Inventory inventory, string name)
        {
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;
                if (item.m_shared.m_name == name || item.m_shared.m_name == "$item_" + name.ToLowerInvariant()) return item;
                if (string.Equals(item.m_shared.m_name, name, StringComparison.OrdinalIgnoreCase)) return item;
            }
            return null;
        }

        private static string Items(Inventory inventory)
        {
            if (inventory == null) return "none";

            var stacks = 0;
            var total = 0;
            var parts = new List<string>();
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;
                stacks++;
                total += item.m_stack;
                parts.Add(item.m_shared.m_name.Replace("$item_", "") + "x" + item.m_stack);
            }
            return "stacks=" + stacks + " items=" + total + " [" + string.Join(",", parts.ToArray()) + "]";
        }

        private static object Read(Type type, object instance, string property)
        {
            var info = type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            return info != null ? info.GetValue(instance, null) : null;
        }

        private static string Flatten(string text)
        {
            return text == null ? "-" : text.Replace("\n", " | ");
        }

        private static string Fmt(Vector3 position)
        {
            return position.x.ToString("0.00") + "," + position.y.ToString("0.00") + "," + position.z.ToString("0.00");
        }

        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text, out value) ? value : fallback;
        }

        private void Say(string message)
        {
            Logger.LogInfo("gc " + message);
        }
    }
}
