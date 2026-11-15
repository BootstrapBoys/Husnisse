using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Husnisse.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Husnisse.Localization
{
    /// <summary>
    /// YAML-backed localisation. Every language lives in its own file under
    /// BepInEx/config/Husnisse/localization, is registered with Jotunn so the game's language
    /// setting selects it, and falls back to the shipped English strings if a key is missing.
    /// </summary>
    public static class Loc
    {
        public const string BowlName = "$husnisse_bowl_name";
        public const string BowlDesc = "$husnisse_bowl_desc";
        public const string BowlHover = "$husnisse_bowl_hover";
        public const string BowlHoverNoFeed = "$husnisse_bowl_hover_nofeed";
        public const string BowlHoverEmpty = "$husnisse_bowl_hover_empty";
        public const string BowlHoverClaim = "$husnisse_bowl_hover_claim";
        public const string BowlHoverClaimRefused = "$husnisse_bowl_hover_claim_refused";
        public const string BowlFed = "$husnisse_bowl_fed";
        public const string BowlNotYours = "$husnisse_bowl_not_yours";
        public const string BowlNeedsFood = "$husnisse_bowl_needs_food";
        public const string BowlContent = "$husnisse_bowl_content";
        public const string BowlNoFeed = "$husnisse_bowl_no_feed";
        public const string BowlAltOff = "$husnisse_bowl_alt_off";
        public const string BowlAltRequire = "$husnisse_bowl_alt_require";
        public const string BowlServerForced = "$husnisse_bowl_server_forced";
        public const string BowlOnlyOwner = "$husnisse_bowl_only_owner";
        public const string BowlOfferOn = "$husnisse_bowl_offer_on";
        public const string BowlOfferOff = "$husnisse_bowl_offer_off";

        public const string OrderNoClaim = "$husnisse_order_no_claim";
        public const string OrderRefuses = "$husnisse_order_refuses";
        public const string OrderTooFar = "$husnisse_order_too_far";
        public const string OrderNotWorking = "$husnisse_order_not_working";
        public const string OrderNotHere = "$husnisse_order_not_here";
        public const string OrderQueued = "$husnisse_order_queued";
        public const string OrderTakes = "$husnisse_order_takes";
        public const string OrderNowhere = "$husnisse_order_nowhere";
        public const string OrderFiles = "$husnisse_order_files";
        public const string OrderLeaves = "$husnisse_order_leaves";

        public const string HandHover = "$husnisse_hand_hover";
        public const string HandName = "$husnisse_hand_name";

        public const string VoiceHungry1 = "$husnisse_voice_hungry1";
        public const string VoiceHungry2 = "$husnisse_voice_hungry2";
        public const string VoiceHungry3 = "$husnisse_voice_hungry3";
        public const string VoiceSulk = "$husnisse_voice_sulk";
        public const string VoiceForgive = "$husnisse_voice_forgive";
        public const string VoiceUnreachable = "$husnisse_voice_unreachable";

        public const string PlaceOverlap = "$husnisse_place_overlap";
        public const string PlaceWard = "$husnisse_place_ward";
        public const string PlaceOtherWard = "$husnisse_place_other_ward";

        public const string ClaimSourceNative = "$husnisse_claim_source_native";
        public const string ClaimSourceWard = "$husnisse_claim_source_ward";
        public const string ClaimRefusedWard = "$husnisse_claim_refused_ward";
        public const string ClaimRefusedOwner = "$husnisse_claim_refused_owner";
        public const string ClaimRefusedHeld = "$husnisse_claim_refused_held";

        // Container network (0.3)
        public const string NetOn = "$husnisse_net_on";
        public const string NetOff = "$husnisse_net_off";
        public const string RuleButton = "$husnisse_rule_button";
        public const string RuleTitle = "$husnisse_rule_title";
        public const string RuleNone = "$husnisse_rule_none";
        public const string RuleOverflow = "$husnisse_rule_overflow";
        public const string RuleDeposit = "$husnisse_rule_deposit";
        public const string RuleClose = "$husnisse_rule_close";
        public const string RuleReadOnly = "$husnisse_rule_read_only";
        public const string RuleRefused = "$husnisse_rule_refused";
        public const string CatMetals = "$husnisse_cat_metals";
        public const string CatWood = "$husnisse_cat_wood";
        public const string CatFood = "$husnisse_cat_food";
        public const string CatTrophies = "$husnisse_cat_trophies";
        public const string CatValuables = "$husnisse_cat_valuables";
        public const string CatBuilding = "$husnisse_cat_building";
        public const string CatConsumables = "$husnisse_cat_consumables";
        public const string CatTools = "$husnisse_cat_tools";

        private const string FolderName = "localization";

        private static readonly Dictionary<string, Dictionary<string, string>> ByLanguage =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> _english;

        public static void Initialize()
        {
            LoadEmbedded();
            LoadFromDisk();

            Log.Info("localisation loaded: " + string.Join(", ", new List<string>(ByLanguage.Keys).ToArray()) +
                     " (check: " + T(BowlName) + ")");
        }

        /// <summary>Looks up a token in the selected language, falling back to the shipped English string.</summary>
        public static string T(string key, params object[] args)
        {
            var value = null as string;
            var language = "English";
            try
            {
                var localization = global::Localization.instance;
                if (localization != null) language = localization.GetSelectedLanguage();
            }
            catch (Exception)
            {
                // Steamworks is not initialized during the client's plugin Awake. The game is
                // still in English at that point; use the embedded table and resolve the real
                // language on the next lookup once Steamworks is ready.
            }

            Dictionary<string, string> table;
            if (!string.IsNullOrEmpty(language) && ByLanguage.TryGetValue(language, out table))
            {
                table.TryGetValue(key, out value);
            }

            if (string.IsNullOrEmpty(value))
            {
                _english.TryGetValue(key, out value);
            }

            if (string.IsNullOrEmpty(value)) value = key;

            return args != null && args.Length > 0 ? string.Format(value, args) : value;
        }

        private static void LoadFromDisk()
        {
            try
            {
                var directory = Path.Combine(Paths.ConfigPath, "Husnisse", FolderName);
                Directory.CreateDirectory(directory);

                var english = Path.Combine(directory, "en.yaml");
                if (!File.Exists(english)) File.WriteAllText(english, DefaultEnglish);

                foreach (var file in Directory.GetFiles(directory, "*.yaml"))
                {
                    LoadFile(file);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("localisation files unavailable: " + ex.Message);
            }
        }

        private static void LoadFile(string path)
        {
            try
            {
                var yaml = File.ReadAllText(path);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var file = deserializer.Deserialize<LocalizationDocument>(yaml);
                if (file == null || string.IsNullOrEmpty(file.Language) || file.Strings == null)
                {
                    Log.Warn("localisation file skipped (missing language or strings): " + path);
                    return;
                }

                if (string.Equals(file.Language, "English", StringComparison.OrdinalIgnoreCase) && file.Version < 5)
                {
                    Log.Warn("outdated English localisation ignored; embedded strings are used: " + path);
                    return;
                }

                Dictionary<string, string> target;
                if (!ByLanguage.TryGetValue(file.Language, out target))
                {
                    target = new Dictionary<string, string>();
                    ByLanguage[file.Language] = target;
                }

                foreach (var pair in file.Strings)
                {
                    target[pair.Key] = pair.Value;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("localisation file failed to parse (" + Path.GetFileName(path) + "): " + ex.Message);
            }
        }

        private static void LoadEmbedded()
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var file = deserializer.Deserialize<LocalizationDocument>(DefaultEnglish);
            _english = file.Strings ?? new Dictionary<string, string>();
            ByLanguage["English"] = new Dictionary<string, string>(_english);
        }

        private class LocalizationDocument
        {
            public int Version { get; set; }
            public string Language { get; set; }
            public Dictionary<string, string> Strings { get; set; }
        }

        private const string DefaultEnglish = @"
version: 5
language: English
strings:
  $husnisse_bowl_name: Porridge Bowl
  $husnisse_bowl_desc: A nisse's home. Keep it fed and it keeps your base.
  $husnisse_bowl_hover: ""Satiety {0:0}/100   Mood {1:0}/100\n[E] open bowl""
  $husnisse_bowl_hover_nofeed: ""This nisse asks for nothing.\n[Alt] require offerings""
  $husnisse_bowl_hover_empty: ""The bowl is empty.\n[E] open bowl""
  $husnisse_bowl_hover_claim: Claim {0:0} m ({1})
  $husnisse_bowl_hover_claim_refused: Claim refused — {0}
  $husnisse_bowl_fed: The nisse takes the offering.
  $husnisse_bowl_not_yours: The nisse's bowl is not yours to fill.
  $husnisse_bowl_needs_food: The nisse needs food.
  $husnisse_bowl_content: The bowl is full enough.
  $husnisse_bowl_no_feed: This nisse is content without offerings.
  $husnisse_bowl_alt_off: ""\n[Alt] no offerings needed""
  $husnisse_bowl_alt_require: ""This nisse asks for nothing.\n[Alt] require offerings""
  $husnisse_bowl_server_forced: This server requires every nisse to be fed.
  $husnisse_bowl_only_owner: Only the player who placed this bowl may change it.
  $husnisse_bowl_offer_on: The nisse will expect offerings.
  $husnisse_bowl_offer_off: The nisse will work without offerings.
  $husnisse_order_no_claim: No nisse claims this place.
  $husnisse_order_refuses: This nisse refuses to settle here.
  $husnisse_order_too_far: The nisse is not nearby.
  $husnisse_order_not_working: The nisse is not working. The bowl is empty.
  $husnisse_order_not_here: The nisse is not here.
  $husnisse_order_queued: The nisse will see to it.
  $husnisse_order_takes: The nisse takes {0} stack(s).
  $husnisse_order_nowhere: The nisse has nowhere to put that.
  $husnisse_order_files: The nisse files {0} stack(s).
  $husnisse_order_leaves: The nisse leaves it as it is.
  $husnisse_hand_hover: ""[E] take my things""
  $husnisse_hand_name: Nisse
  $husnisse_voice_hungry1: The bowl is empty.
  $husnisse_voice_hungry2: It has been a while since anyone left porridge.
  $husnisse_voice_hungry3: The nisse is watching the bowl, not the chests.
  $husnisse_voice_sulk: Something clatters into the wrong chest.
  $husnisse_voice_forgive: The nisse accepts the offering.
  $husnisse_voice_unreachable: The way is shut. The nisse steps through.
  $husnisse_place_overlap: Another nisse already claims this ground.
  $husnisse_place_ward: You are not permitted to build here.
  $husnisse_place_other_ward: Another player's ward holds this ground.
  $husnisse_claim_source_native: native
  $husnisse_claim_source_ward: ward
  $husnisse_claim_refused_ward: the ward does not permit you
  $husnisse_claim_refused_owner: another player's ward
  $husnisse_claim_refused_held: another bowl is already here
  $husnisse_net_on: On the network
  $husnisse_net_off: Off the network
  $husnisse_rule_button: Rules
  $husnisse_rule_title: Storage rule
  $husnisse_rule_none: No rule
  $husnisse_rule_overflow: Overflow
  $husnisse_rule_deposit: Deposit
  $husnisse_rule_close: Close
  $husnisse_rule_read_only: read only
  $husnisse_rule_refused: Only the claim's owner may change this container's rule.
  $husnisse_cat_metals: Metals
  $husnisse_cat_wood: Wood
  $husnisse_cat_food: Food
  $husnisse_cat_trophies: Trophies
  $husnisse_cat_valuables: Valuables
  $husnisse_cat_building: Building
  $husnisse_cat_consumables: Consumables
  $husnisse_cat_tools: Tools
";
    }
}
