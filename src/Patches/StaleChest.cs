using System.Reflection;
using HarmonyLib;
using Husnisse.Core;
using Husnisse.Localization;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// A client's copy of a chest is only current while it owns the ZDO. The nisse works on the
    /// server and takes the ZDO for a moment; vanilla notices — <c>InventoryGui.UpdateContainer</c>
    /// hides the container panel when the local peer is not the owner — but it leaves the window
    /// open on a stale grid, and every move the player then makes reads or writes that stale copy: a
    /// stack taken out of the stale grid lands in the pockets while the chest keeps it (a
    /// duplicate), and a stack put in vanishes (a loss).
    ///
    /// Vanilla also leaves the container's local <c>m_inUse</c> flag set, because <c>SetInUse</c>
    /// only writes while the caller owns the ZDO and the close path is a no-op afterwards. With that
    /// flag stuck, <c>Container.Load</c> refuses to run and the chest never reloads from the ZDO
    /// again — reopening it shows the same stale grid it had.
    ///
    /// So: close the window the moment ownership is gone, and clear the stuck flag so the game's own
    /// reload check brings the real contents back.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
    public static class StaleChestPatch
    {
        private static readonly FieldInfo CurrentContainer =
            AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        private static readonly MethodInfo CloseContainer =
            AccessTools.Method(typeof(InventoryGui), "CloseContainer");

        private static float _nextMessage;

        private static void Postfix(InventoryGui __instance)
        {
            if (__instance == null || CurrentContainer == null || CloseContainer == null) return;

            var container = CurrentContainer.GetValue(__instance) as Container;
            if (container == null) return;

            var view = ContainerNet.View(container);
            if (view == null || !view.IsValid() || view.IsOwner()) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            CloseContainer.Invoke(__instance, null);

            if (Time.time >= _nextMessage)
            {
                _nextMessage = Time.time + 3f;
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.ChestTaken));
            }

            Log.Debug("Chest", "closed the window on " + container.name +
                               ": its ZDO is not this peer's any more");
        }
    }

    /// <summary>
    /// Clear the local in-use flag on a container this peer does not own. Vanilla's close path calls
    /// <c>SetInUse(false)</c>, which returns without doing anything when the caller is not the owner,
    /// so the flag stays true and blocks <c>Container.Load</c>. A non-owner's flag means nothing to
    /// the game — the visual state comes from the ZDO's own <c>inUse</c> — so clearing it is safe and
    /// it is what lets the chest reload.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
    public static class StaleInUsePatch
    {
        private static readonly FieldInfo InUse = AccessTools.Field(typeof(Container), "m_inUse");

        private static void Postfix(Container __instance)
        {
            if (__instance == null || InUse == null) return;
            if (__instance.IsOwner()) return;
            if (!(bool)InUse.GetValue(__instance)) return;

            InUse.SetValue(__instance, false);
            Log.Debug("Chest", "cleared a stuck in-use flag on " + __instance.name);
        }
    }
}
