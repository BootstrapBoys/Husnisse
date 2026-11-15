using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Ledger;
using Husnisse.Util;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Husnisse.Storage
{
    /// <summary>
    /// The rules file: every container rule in a world as editable YAML, plus the named half of the
    /// presets. Export is a server tool (a client has only the zones it has loaded, so its file would
    /// be a partial base); import is all-or-nothing — every entry is resolved against a live
    /// container and every rule is parsed before the first ZDO is written, so a file that does not
    /// fit the world changes nothing at all.
    ///
    /// Container identity is the prefab name plus its position: a copied world keeps both, and a
    /// rebuilt base keeps the prefab but not the position, which is exactly the case the mapping
    /// step exists for (<c>husnisse_rules map &lt;n&gt; &lt;prefab&gt; [index]</c> re-points an entry
    /// before <c>apply</c>).
    /// </summary>
    public static class RulesFile
    {
        public const int Version = 1;

        public static string Path
        {
            get { return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "Husnisse", "rules.yaml"); }
        }

        // ------------------------------------------------------------------ document

        public sealed class Vec3
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }

            public Vec3() { }

            public Vec3(Vector3 point)
            {
                X = point.x;
                Y = point.y;
                Z = point.z;
            }

            public Vector3 ToVector3()
            {
                return new Vector3(X, Y, Z);
            }

            public override string ToString()
            {
                return X.ToString("0.##", CultureInfo.InvariantCulture) + "," +
                       Z.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        public sealed class RuleEntry
        {
            public string Prefab { get; set; }
            public Vec3 Position { get; set; }
            public bool Net { get; set; }
            public bool Blacklist { get; set; }

            /// <summary>The console shorthand (`cat:metals@1`, `custom:$item_coal`, `none`).</summary>
            public string Rule { get; set; }
        }

        public sealed class ClaimEntry
        {
            public Vec3 Bowl { get; set; }

            /// <summary>Allowed pickup categories; null or empty means every preset.</summary>
            public List<string> Pickup { get; set; }

            public List<RuleEntry> Containers { get; set; }
        }

        public sealed class Document
        {
            public int Version { get; set; }
            public string Exported { get; set; }
            public List<ClaimEntry> Claims { get; set; }
            public Dictionary<string, List<string>> Presets { get; set; }
        }

        // ------------------------------------------------------------------ export

        public sealed class ExportResult
        {
            public string Path;
            public int Claims;
            public int Containers;
            public string Error;
        }

        /// <summary>
        /// Writes every claim's rules to <see cref="Path"/>. Read-only: nothing in the world changes.
        /// </summary>
        public static ExportResult Export()
        {
            var result = new ExportResult { Path = Path };

            try
            {
                var document = new Document
                {
                    Version = Version,
                    Exported = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC",
                    Claims = new List<ClaimEntry>(),
                    Presets = EffectivePresets(),
                };

                foreach (var claim in ClaimRegistry.All())
                {
                    if (claim == null || claim.Bowl == null) continue;

                    var entry = new ClaimEntry
                    {
                        Bowl = new Vec3(claim.BowlPosition),
                        Pickup = PickupTokens(PickupFilter.Of(claim)),
                        Containers = new List<RuleEntry>(),
                    };

                    var containers = ContainerIndex.InClaim(claim);
                    containers.Sort(delegate(Container a, Container b)
                    {
                        var first = a.transform.position;
                        var second = b.transform.position;
                        if (Math.Abs(first.x - second.x) > 0.01f) return first.x < second.x ? -1 : 1;
                        if (Math.Abs(first.z - second.z) > 0.01f) return first.z < second.z ? -1 : 1;
                        return first.y.CompareTo(second.y);
                    });

                    foreach (var container in containers)
                    {
                        if (container == null) continue;

                        var rule = ContainerNetwork.RuleOf(container);
                        var net = ContainerNetwork.IsOnNetwork(container);
                        var blacklisted = ContainerNetwork.Zdo(container) != null &&
                                          ContainerNetwork.Zdo(container).GetBool(ZdoKeys.ContainerBlacklist, false);
                        if (rule.IsNone && !net && !blacklisted) continue;

                        entry.Containers.Add(new RuleEntry
                        {
                            Prefab = Utils.GetPrefabName(container.gameObject),
                            Position = new Vec3(container.transform.position),
                            Net = net,
                            Blacklist = blacklisted,
                            Rule = rule.ToShorthand(),
                        });
                    }

                    document.Claims.Add(entry);
                    result.Claims++;
                    result.Containers += entry.Containers.Count;
                }

                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var header = new StringBuilder();
                header.AppendLine("# Husnisse rule file (v" + Version + ").");
                header.AppendLine("# Export: husnisse_rules export   Import: husnisse_rules plan then husnisse_rules apply");
                header.AppendLine("# Rule shorthand: none | overflow | deposit | cat:<category> | custom:<shared name>,...");
                header.AppendLine("# Each entry is one container, addressed by prefab and position; `map` re-points one.");
                header.AppendLine("# presets: replaces the named half of a category (omit a category to keep the built-in set).");

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path, header + serializer.Serialize(document));
                Log.Info("rules exported: " + result.Containers + " container(s) in " + result.Claims + " claim(s) -> " + Path);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Log.Warn("rules export failed: " + ex.Message);
            }

            return result;
        }

        private static List<string> PickupTokens(PickupFilter filter)
        {
            var tokens = new List<string>();
            if (filter == null) return tokens;
            foreach (var category in ContainerRule.Presets)
            {
                if (filter.Allows(category)) tokens.Add(ContainerRule.CategoryToken(category));
            }
            return tokens;
        }

        /// <summary>The preset table the world is running: the file's overrides over the built-in sets.</summary>
        private static Dictionary<string, List<string>> EffectivePresets()
        {
            var table = new Dictionary<string, List<string>>();
            foreach (var category in ContainerRule.Presets)
            {
                var token = ContainerRule.CategoryToken(category);
                var names = new List<string>(CategoryMap.NamesOf(category));
                names.Sort(StringComparer.OrdinalIgnoreCase);
                if (names.Count > 0) table[token] = names;
            }
            return table;
        }

        // ------------------------------------------------------------------ read

        /// <summary>The file's <c>presets:</c> section, or null when the file is absent or has none.</summary>
        public static Dictionary<ItemCategory, List<string>> ReadPresets()
        {
            if (!File.Exists(Path)) return null;

            var document = Parse(File.ReadAllText(Path));
            if (document == null || document.Presets == null || document.Presets.Count == 0) return null;

            var presets = new Dictionary<ItemCategory, List<string>>();
            foreach (var pair in document.Presets)
            {
                var category = ContainerRule.CategoryFromToken(pair.Key);
                if (category == ItemCategory.None)
                {
                    Log.Warn("preset category not understood, skipped: " + pair.Key);
                    continue;
                }
                if (pair.Value == null || pair.Value.Count == 0) continue;
                presets[category] = new List<string>(pair.Value);
            }
            return presets.Count > 0 ? presets : null;
        }

        private static Document Parse(string yaml)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                return deserializer.Deserialize<Document>(yaml);
            }
            catch (Exception ex)
            {
                Log.Warn("rules file failed to parse: " + ex.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------ the plan

        public sealed class PlanRow
        {
            public int Index;
            public int ClaimIndex;
            public RuleEntry Entry;
            public Claim Claim;
            public Container Container;
            public ContainerRule Rule;
            public PickupFilter Pickup;

            /// <summary>Set by `map`: an entry the file could not resolve, pointed at a live container.</summary>
            public bool Mapped;

            public string Describe()
            {
                var target = Container != null
                    ? Utils.GetPrefabName(Container.gameObject) + " " + new Vec3(Container.transform.position)
                    : "UNMAPPED";
                return "yaml row " + Index +
                       " claim=" + ClaimIndex +
                       " file=" + (Entry != null ? Entry.Prefab + " " + Entry.Position : "-") +
                       " target=" + target +
                       " net=" + (Entry != null && Entry.Net) +
                       " rule=" + (Entry != null ? Entry.Rule : "-") +
                       " blacklist=" + (Entry != null && Entry.Blacklist) +
                       (Mapped ? " mapped=yes" : string.Empty);
            }
        }

        public sealed class Plan
        {
            public Document Document;
            public readonly List<PlanRow> Rows = new List<PlanRow>();
            public readonly List<ClaimEntry> Claims = new List<ClaimEntry>();
            public readonly List<PickupFilter> Pickups = new List<PickupFilter>();
            public readonly List<string> Errors = new List<string>();
            public string Source;

            public bool Ok
            {
                get { return Errors.Count == 0; }
            }

            public int Unmapped
            {
                get
                {
                    var count = 0;
                    foreach (var row in Rows) if (row.Container == null) count++;
                    return count;
                }
            }
        }

        private static Plan _pending;

        /// <summary>The plan `husnisse_rules map` edits and `apply` runs.</summary>
        public static Plan Pending
        {
            get { return _pending; }
        }

        public static void ClearPending()
        {
            _pending = null;
        }

        public static Plan Load()
        {
            if (!File.Exists(Path))
            {
                _pending = new Plan { Source = Path };
                _pending.Errors.Add("no rules file at " + Path + " (run husnisse_rules export first)");
                return _pending;
            }

            _pending = Build(File.ReadAllText(Path), Path);
            return _pending;
        }

        public static Plan Build(string yaml, string source)
        {
            var plan = new Plan { Source = source };

            var document = Parse(yaml);
            if (document == null)
            {
                plan.Errors.Add("file is not readable YAML");
                return plan;
            }
            plan.Document = document;

            if (document.Version != Version)
            {
                plan.Errors.Add("version " + document.Version + " is not this build's version " + Version);
                return plan;
            }

            if (document.Claims == null)
            {
                plan.Errors.Add("file has no claims");
                return plan;
            }

            var claimIndex = 0;
            foreach (var entry in document.Claims)
            {
                claimIndex++;
                if (entry == null || entry.Bowl == null)
                {
                    plan.Errors.Add("claim " + claimIndex + " has no bowl position");
                    continue;
                }

                var point = entry.Bowl.ToVector3();
                var claim = ClaimRegistry.FindFor(point);
                if (claim == null || claim.Bowl == null ||
                    Vector3.Distance(claim.BowlPosition, point) > 3f)
                {
                    plan.Errors.Add("claim " + claimIndex + " at " + entry.Bowl + " matches no live bowl");
                    continue;
                }

                var pickup = PickupFilter.Decode(PickupEncode(entry.Pickup, plan, claimIndex));
                plan.Claims.Add(entry);
                plan.Pickups.Add(pickup);

                if (entry.Containers == null) continue;

                foreach (var row in entry.Containers)
                {
                    if (row == null || row.Position == null)
                    {
                        plan.Errors.Add("claim " + claimIndex + " has a container entry with no position");
                        continue;
                    }

                    ContainerRule rule;
                    if (row.Rule == null)
                    {
                        plan.Errors.Add("claim " + claimIndex + " " + row.Prefab + " " + row.Position +
                                       " has no rule (write `none` for an empty rule)");
                        continue;
                    }

                    rule = ContainerRule.ParseShorthand(row.Rule);
                    if (rule == null)
                    {
                        plan.Errors.Add("claim " + claimIndex + " " + row.Prefab + " " + row.Position +
                                       " has a rule this build does not understand: " + row.Rule);
                        continue;
                    }

                    var container = Resolve(claim, row);
                    plan.Rows.Add(new PlanRow
                    {
                        Index = plan.Rows.Count + 1,
                        ClaimIndex = claimIndex,
                        Entry = row,
                        Claim = claim,
                        Container = container,
                        Rule = rule,
                        Pickup = pickup,
                    });
                }
            }

            return plan;
        }

        private static string PickupEncode(List<string> tokens, Plan plan, int claimIndex)
        {
            if (tokens == null || tokens.Count == 0) return string.Empty;

            var allowed = PickupFilter.None();
            foreach (var token in tokens)
            {
                var category = ContainerRule.CategoryFromToken(token == null ? string.Empty : token.Trim());
                if (category == ItemCategory.None)
                {
                    plan.Errors.Add("claim " + claimIndex + " has an unknown pickup category: " + token);
                    continue;
                }
                allowed = allowed.With(category, true);
            }
            return allowed.Encode();
        }

        private static Container Resolve(Claim claim, RuleEntry row)
        {
            Container best = null;
            var bestDistance = 2.0f;

            foreach (var container in ContainerIndex.InClaim(claim))
            {
                if (container == null) continue;
                if (row.Prefab != null && Utils.GetPrefabName(container.gameObject) != row.Prefab) continue;

                var distance = Vector3.Distance(container.transform.position, row.Position.ToVector3());
                if (distance > bestDistance) continue;

                bestDistance = distance;
                best = container;
            }

            return best;
        }

        /// <summary>
        /// Re-points one entry at a live container: the mapping step for a rebuilt base. Refused when
        /// the container is outside every claim or is the bowl.
        /// </summary>
        public static bool Map(Plan plan, int index, string prefab, int containerIndex, out string message)
        {
            message = null;
            if (plan == null) { message = "no plan loaded"; return false; }

            PlanRow row = null;
            foreach (var candidate in plan.Rows)
            {
                if (candidate.Index == index) { row = candidate; break; }
            }
            if (row == null) { message = "no yaml row " + index; return false; }

            var matches = LedgerModel.Containers(prefab);
            if (matches.Count == 0) { message = "no live container " + prefab; return false; }
            if (containerIndex < 0 || containerIndex >= matches.Count)
            {
                message = "no " + prefab + "[" + containerIndex + "]";
                return false;
            }

            var container = matches[containerIndex];
            var claim = ClaimRegistry.FindFor(container.transform.position);
            if (claim == null)
            {
                message = Utils.GetPrefabName(container.gameObject) + "[" + containerIndex + "] is outside every claim";
                return false;
            }

            row.Container = container;
            row.Claim = claim;
            row.Mapped = true;
            message = row.Describe();
            return true;
        }

        /// <summary>
        /// Applies a validated plan. Nothing is written unless the whole plan validated, and the
        /// writes are the same server path the UI's requests land on.
        /// </summary>
        public static bool Apply(Plan plan, out int applied, out string error)
        {
            applied = 0;
            error = null;

            if (plan == null) { error = "no plan loaded (run husnisse_rules plan)"; return false; }
            if (!plan.Ok) { error = "plan refused: " + string.Join("; ", plan.Errors.ToArray()); return false; }
            if (plan.Unmapped > 0) { error = "plan has " + plan.Unmapped + " unmapped entr(y/ies); map them first"; return false; }

            foreach (var row in plan.Rows)
            {
                if (row.Container == null || row.Claim == null)
                {
                    error = "yaml row " + row.Index + " has no container";
                    return false;
                }

                ContainerNetwork.WriteNetwork(row.Container, row.Entry.Net, "yaml import");
                ContainerNetwork.WriteRule(row.Container, row.Rule, "yaml import");
                ContainerNetwork.WriteBlacklist(row.Container, row.Entry.Blacklist, "yaml import");
                applied++;
            }

            for (var i = 0; i < plan.Claims.Count && i < plan.Pickups.Count; i++)
            {
                PickupFilter.Write(FindClaimByBowl(plan.Claims[i]), plan.Pickups[i], "yaml import");
            }

            if (plan.Document != null && plan.Document.Presets != null && plan.Document.Presets.Count > 0)
            {
                var presets = new Dictionary<ItemCategory, List<string>>();
                foreach (var pair in plan.Document.Presets)
                {
                    var category = ContainerRule.CategoryFromToken(pair.Key);
                    if (category == ItemCategory.None) continue;
                    if (pair.Value != null && pair.Value.Count > 0) presets[category] = pair.Value;
                }
                if (presets.Count > 0) PresetStore.Apply(presets);
            }

            Log.Info("rules import applied " + applied + " container(s) from " + plan.Source);
            return true;
        }

        private static Claim FindClaimByBowl(ClaimEntry entry)
        {
            if (entry == null || entry.Bowl == null) return null;
            var point = entry.Bowl.ToVector3();
            var claim = ClaimRegistry.FindFor(point);
            if (claim != null && Vector3.Distance(claim.BowlPosition, point) <= 3f) return claim;
            return null;
        }

        public static string Describe(Plan plan)
        {
            if (plan == null) return "no plan loaded";

            var text = new StringBuilder();
            text.Append("yaml plan rows=").Append(plan.Rows.Count)
                .Append(" claims=").Append(plan.Claims.Count)
                .Append(" unmapped=").Append(plan.Unmapped)
                .Append(" errors=").Append(plan.Errors.Count)
                .Append(" source=").Append(plan.Source);
            return text.ToString();
        }
    }
}
