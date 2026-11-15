using System;
using System.Reflection;
using HarmonyLib;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// Repaints the crafting panel once after a craft.
    ///
    /// InventoryGui builds its recipe list from HaveRequirements and only rebuilds it on specific
    /// events, so after a craft consumes materials the list can keep the tint it was painted with —
    /// every recipe looking unaffordable while the craft button, which reads live counts, still
    /// works. This is the same fix Quartermaster_Integrations carries (our code), kept here because
    /// a craft from the claim moves two sources at once: the load leaves the chest and the craft
    /// spends the pockets, and the row after it has to agree with both.
    ///
    /// DoCrafting marks a repaint pending; the next idle frame calls the private
    /// UpdateCraftingPanel(false), never while a craft timer is still running.
    /// </summary>
    public static class CraftRepaint
    {
        private static bool _pending;
        private static MethodInfo _updatePanel;
        private static FieldInfo _craftTimer;

        public static void Bind(Harmony harmony)
        {
            try
            {
                var doCrafting = AccessTools.Method(typeof(InventoryGui), "DoCrafting", new[] { typeof(Player) });
                _updatePanel = AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel", new[] { typeof(bool) });
                _craftTimer = AccessTools.Field(typeof(InventoryGui), "m_craftTimer");

                if (doCrafting == null || _updatePanel == null)
                {
                    Log.Warn("[Supply] repaint bindings incomplete: DoCrafting=" + (doCrafting != null) +
                             " UpdateCraftingPanel=" + (_updatePanel != null) + "; the recipe list may stay stale.");
                    return;
                }

                harmony.Patch(doCrafting,
                    postfix: new HarmonyMethod(typeof(CraftRepaint), nameof(DoCraftingPostfix)));
            }
            catch (Exception ex)
            {
                Log.Warn("[Supply] repaint binding failed: " + ex.Message);
            }
        }

        public static void MarkPending()
        {
            _pending = true;
        }

        private static void DoCraftingPostfix()
        {
            MarkPending();
        }

        /// <summary>Called from the inventory GUI's per-frame postfix; never repaints mid-craft.</summary>
        public static void FlushIfIdle(InventoryGui gui)
        {
            if (!_pending || gui == null || _updatePanel == null) return;

            var timer = _craftTimer != null ? _craftTimer.GetValue(gui) : null;
            if (timer is float running && running >= 0f) return;

            _pending = false;

            try
            {
                _updatePanel.Invoke(gui, new object[] { false });
            }
            catch (Exception ex)
            {
                Log.Debug("Supply", "could not repaint the recipe list: " + ex.Message);
            }
        }
    }
}
