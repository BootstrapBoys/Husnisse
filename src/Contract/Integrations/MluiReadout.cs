using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Husnisse.Compat;
using Husnisse.Core;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// MyLittleUI also appends an available-amount to each requirement row — its own number, counted
    /// from the player's pockets only. Two numbers on one row is the double-count this release exists
    /// to prevent, so Husnisse turns that one setting off and writes the row itself; every other
    /// MyLittleUI feature is untouched, and the user's config file is not rewritten (the change is in
    /// memory for this session only).
    ///
    /// This is the same shape AAA Crafting already uses against MyLittleUI's multicraft setting, so
    /// it is an established arrangement in this pack rather than a new kind of interference.
    /// </summary>
    public static class MluiReadout
    {
        private const string Section = "Item - Available resources amount";
        private const string Key = "Enabled";

        public static string State { get; private set; } = "absent";

        public static void Suppress()
        {
            State = "absent";

            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(PluginGuards.MluGuid, out info) || info == null || info.Instance == null)
            {
                return;
            }

            try
            {
                var config = info.Instance.Config;
                if (config == null)
                {
                    State = "no-config";
                    return;
                }

                ConfigEntry<bool> entry;
                if (!config.TryGetEntry(Section, Key, out entry) || entry == null)
                {
                    State = "entry-missing";
                    Log.Warn("[Contract] MyLittleUI's '" + Section + " / " + Key + "' entry was not found; " +
                             "its pocket-only number stays beside Husnisse's.");
                    return;
                }

                if (entry.Value)
                {
                    entry.Value = false;
                    State = "suppressed";
                    Log.Info("[Contract] MyLittleUI's available-amount number is off for this session so the " +
                             "requirement row has one count, not two.");
                }
                else
                {
                    State = "already-off";
                }
            }
            catch (Exception ex)
            {
                State = "error";
                Log.Warn("[Contract] Could not read MyLittleUI's config: " + ex.Message);
            }
        }
    }
}
