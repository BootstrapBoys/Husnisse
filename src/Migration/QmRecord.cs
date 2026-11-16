using Newtonsoft.Json;

namespace Husnisse.Migration
{
    /// <summary>
    /// Quartermaster's per-container record, as Quartermaster writes it into a container's ZDO: one
    /// JSON string carrying the deposit/overflow role, the four supply permissions, the item list it
    /// remembers and the flags it learns with.
    ///
    /// The names here are Quartermaster's <b>data format</b>, not ours, and they appear in this
    /// directory only. Migration reads them; nothing in the storage layer, the rule string or the
    /// diagnostics ever uses one. That is the clean-room line: a save format we read is not a shape we
    /// copy.
    /// </summary>
    public sealed class QmRecord
    {
        public int Version;

        /// <summary>This container is a place players drop things off.</summary>
        public bool Deposit;

        public bool AcceptStorage;

        // The four supply permissions, one flag each. Quartermaster splits "may be raided" into these
        // and so do we, which is why this is the one part of its record that maps one-to-one.
        public bool CraftingSupply;
        public bool FuelSupply;
        public bool ProcessingSupply;
        public bool LivestockFeed;

        public bool FeedUntamed;
        public float FeedingRange;
        public float PickupRange;

        /// <summary>First choice among containers that accept the same item.</summary>
        public bool Preferred;

        /// <summary>This container takes what nothing else does.</summary>
        public bool Overflow;

        public bool AutoSort;

        // Its learning model: it watches what players put where. Ours is teaching by demonstration
        // plus the ledger, so these become notes rather than state.
        public bool Learn;
        public bool AutoAssign;
        public string AutoCategories;

        // Base grouping. Quartermaster groups chests into named bases; a claim is our unit, so this
        // is a note.
        public string BaseName;
        public string Group;

        /// <summary>Item prefab names it files here.</summary>
        public string[] Remembered;

        /// <summary>Item prefab names it refuses here.</summary>
        public string[] Forgotten;

        /// <summary>
        /// Reads one of Quartermaster's records, or returns null when the text is not one. The check
        /// is the shape, not a key name: any ZDO string that parses as this record is one.
        /// </summary>
        public static QmRecord Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (json[0] != '{') return null;
            if (json.IndexOf("\"Remembered\"", System.StringComparison.Ordinal) < 0) return null;
            if (json.IndexOf("\"Version\"", System.StringComparison.Ordinal) < 0) return null;

            try
            {
                var record = JsonConvert.DeserializeObject<QmRecord>(json);
                if (record == null) return null;
                if (record.Remembered == null) record.Remembered = new string[0];
                if (record.Forgotten == null) record.Forgotten = new string[0];
                return record;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
