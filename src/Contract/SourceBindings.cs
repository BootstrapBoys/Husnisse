using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Contract
{
    /// <summary>
    /// Reflection bindings for the two player-side places the contract counts beside the claim:
    /// ExtraSlots' rows and AdventureBackpacks' equipped pack. Both mods are soft dependencies, so
    /// nothing here is compiled against them and a missing mod answers "absent" — the contract says
    /// an absent source is skipped, not failed.
    /// </summary>
    public static class SourceBindings
    {
        public const string ExtraSlotsGuid = "shudnal.ExtraSlots";
        public const string AdventureBackpacksGuid = "vapok.mods.adventurebackpacks";

        private static bool _slotsScanned;
        private static Func<ItemDrop.ItemData, bool> _isItemInSlot;
        private static Func<Vector2i, bool> _isGridPositionASlot;

        private static bool _backpackScanned;
        private static MethodInfo _tryGetEquippedBackpack;

        /// <summary>ExtraSlots extends the player's grid; its rows are a source, not a second inventory.</summary>
        public static bool SlotsBound
        {
            get { return BindSlots() != null; }
        }

        /// <summary>True when the equipped backpack answers. A bound API with no pack equipped answers false.</summary>
        public static bool BackpackBound
        {
            get { return BindBackpack() != null; }
        }

        /// <summary>Whether one item sits in an ExtraSlots row. False when ExtraSlots is absent.</summary>
        public static bool IsExtraSlotItem(ItemDrop.ItemData item)
        {
            if (item == null) return false;
            var isItemInSlot = BindSlots();
            if (isItemInSlot == null) return false;

            try
            {
                return isItemInSlot(item);
            }
            catch (Exception ex)
            {
                Log.Debug("Contract", "ExtraSlots item probe failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>Whether a grid cell belongs to ExtraSlots. Diagnostics only.</summary>
        public static bool IsExtraSlotCell(Vector2i cell)
        {
            var isCell = BindSlots() == null ? null : _isGridPositionASlot;
            if (isCell == null) return false;

            try
            {
                return isCell(cell);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The equipped AdventureBackpacks inventory, or null when absent or empty-handed.</summary>
        public static Inventory EquippedBackpack(Player player)
        {
            var method = BindBackpack();
            if (method == null || player == null) return null;

            try
            {
                var args = new object[] { player, null };
                var answer = method.Invoke(null, args);
                return answer is bool ok && ok ? args[1] as Inventory : null;
            }
            catch (Exception ex)
            {
                Log.Debug("Contract", "AdventureBackpacks equipped-pack probe failed: " + ex.Message);
                return null;
            }
        }

        private static Func<ItemDrop.ItemData, bool> BindSlots()
        {
            if (_slotsScanned) return _isItemInSlot;
            _slotsScanned = true;

            try
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(ExtraSlotsGuid, out info) || info == null || info.Instance == null)
                {
                    return null;
                }

                var slots = info.Instance.GetType().Assembly.GetType("ExtraSlots.Slots");
                if (slots == null)
                {
                    Log.Warn("[Contract] ExtraSlots is installed but ExtraSlots.Slots was not found; its rows are not a source.");
                    return null;
                }

                var isItem = slots.GetMethod("IsItemInSlot", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(ItemDrop.ItemData) }, null);
                var isCell = slots.GetMethod("IsGridPositionASlot", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Vector2i) }, null);

                if (isItem != null && isItem.ReturnType == typeof(bool))
                {
                    _isItemInSlot = (Func<ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, bool>), isItem);
                }

                if (isCell != null && isCell.ReturnType == typeof(bool))
                {
                    _isGridPositionASlot = (Func<Vector2i, bool>)Delegate.CreateDelegate(typeof(Func<Vector2i, bool>), isCell);
                }

                if (_isItemInSlot == null)
                {
                    Log.Warn("[Contract] ExtraSlots.Slots.IsItemInSlot was not found; its rows are counted as pockets.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("[Contract] ExtraSlots binding failed: " + ex.Message);
            }

            return _isItemInSlot;
        }

        private static MethodInfo BindBackpack()
        {
            if (_backpackScanned) return _tryGetEquippedBackpack;
            _backpackScanned = true;

            try
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(AdventureBackpacksGuid, out info) || info == null || info.Instance == null)
                {
                    return null;
                }

                var api = info.Instance.GetType().Assembly.GetType("AdventureBackpacks.API.ABAPI");
                var method = api == null
                    ? null
                    : api.GetMethod("TryGetEquippedBackpackInventory", BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(Player), typeof(Inventory).MakeByRefType() }, null);

                if (method == null || method.ReturnType != typeof(bool))
                {
                    Log.Warn("[Contract] AdventureBackpacks is installed but ABAPI.TryGetEquippedBackpackInventory was not found; " +
                             "the equipped pack is not a source.");
                    return null;
                }

                _tryGetEquippedBackpack = method;
            }
            catch (Exception ex)
            {
                Log.Warn("[Contract] AdventureBackpacks binding failed: " + ex.Message);
            }

            return _tryGetEquippedBackpack;
        }

        /// <summary>One line for `husnisse_status`: what the optional sources bound to.</summary>
        public static string Describe()
        {
            return "extraslots=" + (SlotsBound ? "bound" : "absent") +
                   " adventurebackpacks=" + (BackpackBound ? "bound" : "absent");
        }

        /// <summary>Marks the bindings stale so a reload can bind a mod that arrived later.</summary>
        public static void Reset()
        {
            _slotsScanned = false;
            _backpackScanned = false;
            _isItemInSlot = null;
            _isGridPositionASlot = null;
            _tryGetEquippedBackpack = null;
        }
    }
}
