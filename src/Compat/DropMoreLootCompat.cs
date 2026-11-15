using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Compat
{
    /// <summary>
    /// DropMoreLoot multiplies `SharedData.m_maxStackSize` in `ItemDrop.Awake`. A storage mod that
    /// also writes stack sizes fights it: whichever runs last wins, and the loss is silent.
    ///
    /// Husnisse never writes a stack size. It has no `ItemStacks`-shaped override to disable and no
    /// maximum-stack setting of its own — stack sizes are scheduled for 1.0, and until then the
    /// game's value (or whatever another mod made of it) is the value every count, merge and
    /// requirement uses. That is the whole integration: defer by not competing, and say so in the
    /// diagnostics so a support log shows whose number is in play.
    ///
    /// The merge rule is the other half of the same question. Vanilla's `FindFreeStackItem` compares
    /// shared data, quality and world level — it does **not** care who crafted a stack, and it does
    /// not look at custom-data keys. Husnisse's own pocket merge adds durability and variant to that
    /// list and never touches quality, world level or durability to make a merge fit. So a stack
    /// crafted at a bench merges with one picked up (the relaxation Quartermaster_Integrations asks
    /// for), and EpicLoot's material stamps do not split a stack, while gear, levelled items and
    /// damaged items keep their own stacks.
    /// </summary>
    public static class DropMoreLootCompat
    {
        public const string Guid = "TastyChickenLegs.DropMoreLoot";

        private static bool _read;
        private static bool _installed;
        private static bool _stacking;
        private static int _multiplier = 1;

        public static bool Installed
        {
            get
            {
                Read();
                return _installed;
            }
        }

        /// <summary>One line for `husnisse_status`: whether another mod owns stack sizes.</summary>
        public static string Describe()
        {
            Read();

            if (!_installed) return "stacks=native";

            return "stacks=dropmoreloot" +
                   (Safe(() => _stacking) ? " on" : " off") +
                   " x" + SafeInt(() => _multiplier);
        }

        public static void LogOnce()
        {
            Read();
            if (!_installed) return;

            try
            {
                Log.Info("[Compat] DropMoreLoot owns stack sizes (" +
                         (Safe(() => _stacking) ? "stacking on, x" + SafeInt(() => _multiplier) : "stacking off") +
                         "); Husnisse never writes a stack size, so its counts and merges use whatever " +
                         "DropMoreLoot applied.");
            }
            catch (Exception ex)
            {
                Log.Debug("Compat", "DropMoreLoot probe failed: " + ex.Message);
            }
        }

        private static void Read()
        {
            if (_read) return;
            _read = true;

            try
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(Guid, out info) || info == null || info.Instance == null)
                {
                    _installed = false;
                    return;
                }

                _installed = true;

                var main = info.Instance.GetType().Assembly.GetType("DropMoreLoot.DropMoreLootMain");
                var enabled = main == null ? null : AccessTools.Field(main, "enableStacking");
                var multiplier = main == null ? null : AccessTools.Field(main, "itemStackMultiplier");

                _stacking = ReadConfig(enabled, false);
                _multiplier = ReadConfig(multiplier, 1);
            }
            catch (Exception ex)
            {
                Log.Debug("Compat", "DropMoreLoot binding failed: " + ex.Message);
                _installed = false;
            }
        }

        private static int ReadConfig(FieldInfo field, int fallback)
        {
            var entry = field?.GetValue(null);
            if (entry == null) return fallback;

            var value = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            var boxed = value != null ? value.GetValue(entry, null) : null;
            return boxed is int number ? number : fallback;
        }

        private static bool ReadConfig(FieldInfo field, bool fallback)
        {
            var entry = field?.GetValue(null);
            if (entry == null) return fallback;

            var value = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            var boxed = value != null ? value.GetValue(entry, null) : null;
            return boxed is bool flag ? flag : fallback;
        }

        private static bool Safe(Func<bool> read)
        {
            try { return read(); }
            catch { return false; }
        }

        private static int SafeInt(Func<int> read)
        {
            try { return read(); }
            catch { return 1; }
        }
    }
}
