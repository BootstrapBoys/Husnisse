using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Husnisse.Core;
using Jotunn.Managers;
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

        public const string PlaceOverlap = "$husnisse_place_overlap";
        public const string PlaceWard = "$husnisse_place_ward";
        public const string PlaceOtherWard = "$husnisse_place_other_ward";

        private const string FolderName = "localization";

        private static readonly Dictionary<string, Dictionary<string, string>> ByLanguage =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> _english;

        public static void Initialize()
        {
            LoadEmbedded();
            LoadFromDisk();
        }

        /// <summary>Registers every loaded language with Jotunn; call once the managers are ready.</summary>
        public static void Apply()
        {
            try
            {
                foreach (var pair in ByLanguage)
                {
                    LocalizationManager.Instance.AddLocalization(pair.Key, pair.Value);
                }
                Log.Info("localisation registered: " + string.Join(", ", new List<string>(ByLanguage.Keys).ToArray()));
            }
            catch (Exception ex)
            {
                Log.Warn("localisation registration failed: " + ex.Message);
            }
        }

        /// <summary>Looks up a token, falling back to the shipped English string.</summary>
        public static string T(string key, params object[] args)
        {
            var value = null as string;

            try
            {
                if (global::Localization.instance != null)
                {
                    value = global::Localization.instance.Localize(key);
                }
            }
            catch
            {
                // fall through to the English table
            }

            if (string.IsNullOrEmpty(value) || value == key)
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
            public string Language { get; set; }
            public Dictionary<string, string> Strings { get; set; }
        }

        private const string DefaultEnglish = @"
language: English
strings:
  $husnisse_bowl_name: Porridge Bowl
  $husnisse_bowl_desc: A nisse's home. Keep it fed and it keeps your base.
  $husnisse_bowl_hover: ""Satiety {0:0}/100   Mood {1:0}/100\n[E] offer food""
  $husnisse_bowl_hover_nofeed: ""This nisse asks for nothing.\n[Alt] require offerings""
  $husnisse_bowl_hover_empty: ""The bowl is empty.\n[E] offer food""
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
  $husnisse_place_overlap: Another nisse already claims this ground.
  $husnisse_place_ward: You are not permitted to build here.
  $husnisse_place_other_ward: Another player's ward holds this ground.
";
    }
}
