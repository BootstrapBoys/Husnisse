using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Husnisse.ClientProbe
{
    /// <summary>
    /// Client-side probe: builds a bowl in front of the local player, verifies the food guard,
    /// hover text and container UI, and leaves a screenshot plus a log. Test-only; never shipped.
    /// </summary>
    [BepInPlugin(Guid, "Husnisse Client Probe", "0.1.0")]
    public class ClientProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "bootstrapboys.husnisse.clientprobe7";
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        private void Awake()
        {
            Logger.LogInfo("clientprobe: loaded");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var waited = 0f;
            while (waited < 120f)
            {
                if (Player.m_localPlayer != null && ZNetScene.instance != null && ObjectDB.instance != null)
                    break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }

            var player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null || ObjectDB.instance == null)
            {
                Logger.LogWarning("clientprobe: player/world not ready");
                yield break;
            }

            Logger.LogInfo("clientprobe: player=" + player.GetPlayerName() +
                           " pos=" + player.transform.position.ToString("F2") +
                           " server=" + (ZNet.instance != null && !ZNet.instance.IsServer()));
            yield return new WaitForSeconds(5f);

            // Jotunn's vanilla-prefabs event did not fire for Husnisse on this client, so the
            // build piece was never registered. Invoke its private registration once for this probe.
            var husnisseType = Type.GetType("Husnisse.HusnissePlugin, Husnisse");
            var register = husnisseType != null
                ? husnisseType.GetMethod("RegisterContent", BindingFlags.Static | BindingFlags.NonPublic)
                : null;
            if (register != null)
            {
                try
                {
                    register.Invoke(null, null);
                    Logger.LogInfo("clientprobe: forced Husnisse content registration");
                }
                catch (Exception e)
                {
                    Logger.LogWarning("clientprobe: registration invoke failed: " + e);
                }
            }
            else
            {
                Logger.LogWarning("clientprobe: RegisterContent not found");
            }

            // The mod's Awake aborted before PatchAll on this client. Apply its Harmony patches
            // here so the UI/food guard can still be exercised; this is test scaffolding only.
            try
            {
                var harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony");
                if (harmonyType != null && husnisseType != null)
                {
                    var harmony = Activator.CreateInstance(harmonyType, "husnisse.clientprobe");
                    var patchAll = harmonyType.GetMethod("PatchAll", new[] { typeof(Assembly) });
                    patchAll?.Invoke(harmony, new object[] { husnisseType.Assembly });
                    Logger.LogInfo("clientprobe: applied Husnisse Harmony patches manually");
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("clientprobe: manual Harmony patch failed: " + e);
            }
            yield return new WaitForSeconds(1f);

            var prefab = ZNetScene.instance.GetPrefab("husnisse_bowl");
            GameObject go = null;
            Container container = null;

            if (prefab != null)
            {
                var pos = player.transform.position + player.transform.forward * 1.8f + Vector3.up * 0.2f;
                var hash = "husnisse_bowl".GetStableHashCode();
                var zdo = ZDOMan.instance.CreateNewZDO(pos, hash);
                zdo.SetPrefab(hash);
                zdo.Set("creator", Game.instance.GetPlayerProfile().GetPlayerID());

                var create = typeof(ZNetScene).GetMethod("CreateObject", Priv);
                go = create != null ? create.Invoke(ZNetScene.instance, new object[] { zdo }) as GameObject : null;
                if (go != null)
                {
                    Logger.LogInfo("clientprobe: bowl spawned at " + go.transform.position.ToString("F2") +
                                   " zdo=" + zdo.m_uid + " active=" + go.activeSelf);
                    container = go.GetComponentInChildren<Container>(true);
                }
            }
            else
            {
                // The client did not receive a husnisse_bowl prefab in ZNetScene. Wait for the
                // server-spawned test bowl and find it in the loaded scene instead.
                Logger.LogWarning("clientprobe: prefab missing; waiting for a server-spawned bowl");
                var bowlType = Type.GetType("Husnisse.Bowl.BowlPiece, Husnisse");
                for (var attempt = 0; attempt < 20 && container == null; attempt++)
                {
                    foreach (var candidate in Object.FindObjectsOfType<Container>())
                    {
                        if (bowlType != null && candidate.GetComponent(bowlType) != null)
                        {
                            container = candidate;
                            go = candidate.gameObject;
                            break;
                        }
                    }
                    if (container == null) yield return new WaitForSeconds(1f);
                }
                if (container != null)
                    Logger.LogInfo("clientprobe: found server bowl at " + go.transform.position.ToString("F2"));

                if (container == null)
                {
                    Logger.LogWarning("clientprobe: no server bowl; building a local cauldron clone");
                    var cauldron = ZNetScene.instance.GetPrefab("piece_cauldron");
                    if (cauldron != null)
                    {
                        go = Object.Instantiate(cauldron, player.transform.position + player.transform.forward * 1.8f, Quaternion.identity);
                        go.SetActive(false);
                        var oldContainer = go.GetComponentInChildren<Container>(true);
                        if (oldContainer != null) Object.DestroyImmediate(oldContainer);
                        container = go.AddComponent<Container>();
                        container.m_name = "Test Porridge Bowl";
                        container.m_width = 4;
                        container.m_height = 1;
                        container.m_privacy = Container.PrivacySetting.Public;
                        container.m_checkGuardStone = false;
                        container.m_autoDestroyEmpty = false;
                        var bowlType2 = Type.GetType("Husnisse.Bowl.BowlPiece, Husnisse");
                        if (bowlType2 != null && go.GetComponent(bowlType2) == null) go.AddComponent(bowlType2);
                        go.SetActive(true);
                        Logger.LogInfo("clientprobe: local cauldron clone at " + go.transform.position.ToString("F2"));
                    }
                }
            }

            if (container == null)
            {
                Logger.LogError("clientprobe: no bowl container found");
                yield break;
            }

            container.m_privacy = Container.PrivacySetting.Public;
            yield return new WaitForSeconds(2f);

            var hover = container.GetHoverText();
            Logger.LogInfo("clientprobe: hover=" + (hover ?? "<null>"));

            var inv = container.GetInventory();
            if (inv == null)
            {
                Logger.LogError("clientprobe: no inventory");
                yield break;
            }

            var coal = ObjectDB.instance.GetItemPrefab("Coal");
            var coalData = coal != null ? coal.GetComponent<ItemDrop>().m_itemData.Clone() : null;
            var guardType = Type.GetType("Husnisse.Bowl.BowlFoodGuard, Husnisse");
            if (guardType != null)
            {
                var registerGuard = guardType.GetMethod("Register", BindingFlags.Static | BindingFlags.Public);
                var isBowlGuard = guardType.GetMethod("IsBowl", BindingFlags.Static | BindingFlags.Public);
                var isFoodGuard = guardType.GetMethod("IsFood", BindingFlags.Static | BindingFlags.Public);
                registerGuard?.Invoke(null, new object[] { container });
                Logger.LogInfo("clientprobe: guard isBowl=" + (isBowlGuard?.Invoke(null, new object[] { inv }) ?? "?") +
                               " coalIsFood=" + (isFoodGuard?.Invoke(null, new object[] { coalData }) ?? "?"));
            }
            var coalCan = coalData != null && inv.CanAddItem(coalData, 1);
            var coalAdded = coalData != null && inv.AddItem(coalData);
            Logger.LogInfo("clientprobe: coal can=" + coalCan + " added=" + coalAdded +
                           " items=" + inv.NrOfItems());

            GameObject food = null;
            foreach (var item in ObjectDB.instance.m_items)
            {
                var drop = item != null ? item.GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null &&
                    drop.m_itemData.m_shared.m_food > 0f)
                {
                    food = item;
                    break;
                }
            }

            if (food != null)
            {
                var foodData = food.GetComponent<ItemDrop>().m_itemData.Clone();
                var foodCan = inv.CanAddItem(foodData, 1);
                var foodAdded = inv.AddItem(foodData);
                Logger.LogInfo("clientprobe: food=" + food.name + " can=" + foodCan +
                               " added=" + foodAdded + " items=" + inv.NrOfItems());
            }
            else
            {
                Logger.LogWarning("clientprobe: no food prefab found");
            }

            Logger.LogInfo("clientprobe: pantry slots=" + inv.GetWidth() + "x" + inv.GetHeight() +
                           " items=" + inv.NrOfItems());
            var opened = container.Interact(player, false, false);
            Logger.LogInfo("clientprobe: interact=" + opened);

            yield return new WaitForSeconds(2f);
            var shot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "husnisse-client-probe.png");
            ScreenCapture.CaptureScreenshot(shot);
            Logger.LogInfo("clientprobe: screenshot=" + shot);
            Logger.LogInfo("clientprobe: done");
        }
    }
}
