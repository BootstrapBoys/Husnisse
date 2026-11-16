using System;
using System.Collections.Generic;
using System.Reflection;
using Husnisse.Storage;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Migration
{
    /// <summary>A container ZDO that still carries Quartermaster's record.</summary>
    public sealed class QmContainer
    {
        public ZDO Zdo;
        public string Prefab;
        public Vector3 Position;
        public QmRecord Record;
        public string Existing;

        /// <summary>True when a live piece exists for this ZDO in the loaded scene.</summary>
        public bool Live;

        /// <summary>The container's packed inventory, if the save has one.</summary>
        public byte[] Items;

        /// <summary>The rule Husnisse already has here, decoded; None when there is none.</summary>
        public ContainerRule ExistingRule
        {
            get
            {
                if (string.IsNullOrEmpty(Existing)) return ContainerRule.None();
                var rule = ContainerRule.Decode(Existing);
                return rule ?? ContainerRule.None();
            }
        }
    }

    /// <summary>
    /// Finds Quartermaster's per-container records in a world Husnisse has loaded.
    ///
    /// Quartermaster stores them as a JSON string in each container's ZDO, so they survive its
    /// removal — which is what makes a migration possible at all: the operator boots Husnisse on a
    /// copy of the world and the records are still there to be read. Nothing here needs Quartermaster
    /// installed, its assembly, or any of its key names: a ZDO's string values are enumerated and a
    /// value is recognised by its own shape (<see cref="QmRecord.Parse"/>).
    ///
    /// It works over <b>ZDOs</b>, not over live containers. A dedicated server loads no zones, so on
    /// the rig — and on any server whose base is not being visited right now — most chests have no
    /// GameObject at all; the save still knows every one of them. A migration that could only see
    /// what happened to be loaded would be a migration that quietly skipped most of a base.
    /// </summary>
    public static class QmScan
    {
        private static readonly FieldInfo ObjectsById =
            typeof(ZDOMan).GetField("m_objectsByID", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Every container ZDO carrying a readable Quartermaster record.</summary>
        public static List<QmContainer> Containers()
        {
            var found = new List<QmContainer>();
            var map = Objects();
            if (map == null)
            {
                Core.Log.Debug("Migrate", "no ZDO map: ZDOMan.m_objectsByID not readable");
                return found;
            }

            foreach (var pair in map)
            {
                var zdo = pair.Value;
                if (zdo == null) continue;

                var record = QmRecord.Parse(Payload(zdo));
                if (record == null) continue;

                found.Add(new QmContainer
                {
                    Zdo = zdo,
                    Prefab = PrefabName(zdo),
                    Position = zdo.GetPosition(),
                    Record = record,
                    Existing = zdo.GetString(ZdoKeys.ContainerRule, string.Empty),
                    Live = ZNetScene.instance != null && ZNetScene.instance.FindInstance(zdo) != null,
                    Items = zdo.GetByteArray("items"),
                });
            }

            found.Sort(delegate(QmContainer a, QmContainer b)
            {
                if (Math.Abs(a.Position.x - b.Position.x) > 0.01f) return a.Position.x < b.Position.x ? -1 : 1;
                if (Math.Abs(a.Position.z - b.Position.z) > 0.01f) return a.Position.z < b.Position.z ? -1 : 1;
                return a.Position.y.CompareTo(b.Position.y);
            });
            return found;
        }

        /// <summary>
        /// Count of records whose prefab this world does not have — Quartermaster's own deposit chests
        /// when it is removed, or a container from another mod that is not installed. Their contents
        /// stay in the save untouched; a migration must say so rather than skip them silently.
        /// </summary>
        public static int Orphans(out int withItems)
        {
            withItems = 0;
            var orphans = 0;

            var found = Containers();
            foreach (var record in found)
            {
                if (record.Live) continue;
                if (ZNetScene.instance != null && ZNetScene.instance.GetPrefab(record.Zdo.GetPrefab()) != null) continue;

                orphans++;
                if (record.Items != null) withItems++;
            }
            return orphans;
        }

        /// <summary>
        /// The ZDO's Quartermaster record, if it has one. <c>ZDOExtraData</c> holds a ZDO's string
        /// values by key hash, so this reads them all and lets the payload identify itself — which is
        /// also why the migration needs no key name from Quartermaster at all.
        /// </summary>
        public static string Payload(ZDO zdo)
        {
            if (zdo == null) return null;

            try
            {
                List<KeyValuePair<int, float>> floats;
                List<KeyValuePair<int, Vector3>> vec3s;
                List<KeyValuePair<int, Quaternion>> quats;
                List<KeyValuePair<int, int>> ints;
                List<KeyValuePair<int, long>> longs;
                List<KeyValuePair<int, string>> strings;
                List<KeyValuePair<int, byte[]>> bytes;
                ZDOConnection connection;

                ZDOExtraData.GetData(zdo.m_uid, out floats, out vec3s, out quats, out ints, out longs,
                                     out strings, out bytes, out connection);
                if (strings == null) return null;

                foreach (var pair in strings)
                {
                    var value = pair.Value;
                    if (QmRecord.Parse(value) == null) continue;
                    return value;
                }
            }
            catch (Exception ex)
            {
                Core.Log.Debug("Migrate", "could not read a ZDO's strings: " + ex.Message);
            }

            return null;
        }

        private static Dictionary<ZDOID, ZDO> Objects()
        {
            if (ObjectsById == null || ZDOMan.instance == null) return null;
            return ObjectsById.GetValue(ZDOMan.instance) as Dictionary<ZDOID, ZDO>;
        }

        /// <summary>A prefab name from the ZDO's stored hash, or the hash when the prefab is gone.</summary>
        public static string PrefabName(ZDO zdo)
        {
            if (zdo == null) return "-";
            var hash = zdo.GetPrefab();
            if (ZNetScene.instance != null)
            {
                var prefab = ZNetScene.instance.GetPrefab(hash);
                if (prefab != null) return prefab.name;
            }
            return "hash:" + hash;
        }
    }
}
