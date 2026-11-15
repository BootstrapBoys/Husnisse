using System;
using System.Collections.Generic;
using Husnisse.Contract;
using Husnisse.Core;
using Husnisse.Orders;
using Husnisse.Patches;
using UnityEngine;

namespace Husnisse.Net
{
    /// <summary>How a supply request ended. Wire values: a client and a server of the same build agree.</summary>
    public enum FetchResult : byte
    {
        Accepted = 0,
        Busy = 1,
        NoClaim = 2,
        NotYours = 3,
        TooFar = 4,
        NotWorking = 5,
        Nothing = 6,
        Missing = 7,
        Disabled = 8,
    }

    /// <summary>
    /// The supply channel: a client asks the server to have the nisse bring materials, the server
    /// validates and answers, and the finished load travels back to the client that will hold it.
    ///
    /// The delivery leg is the same shape Empty Pockets uses in reverse: a player's inventory lives
    /// on the player's machine, so the server cannot put an item in it. It sends item data to the
    /// peer that asked, the peer adds it to its own inventory, and answers with what it took. A peer
    /// that cannot fit the load answers with what did fit, and the server sets the rest down where
    /// the player stood rather than deleting it.
    /// </summary>
    public static class FetchRpc
    {
        private const string RequestMethod = "Husnisse_Fetch";
        private const string AckMethod = "Husnisse_FetchAck";
        private const string DeliverMethod = "Husnisse_FetchDeliver";
        private const string TakenMethod = "Husnisse_FetchTaken";

        /// <summary>More item types than any recipe has, and more stacks than a trip carries.</summary>
        private const int MaxItems = 12;
        private const int MaxStacks = 40;

        private static bool _registered;

        public static void Register()
        {
            if (_registered || ZRoutedRpc.instance == null) return;

            ZRoutedRpc.instance.Register<ZPackage>(RequestMethod, HandleRequest);
            ZRoutedRpc.instance.Register<ZPackage>(AckMethod, HandleAck);
            ZRoutedRpc.instance.Register<ZPackage>(DeliverMethod, HandleDeliver);
            ZRoutedRpc.instance.Register<ZPackage>(TakenMethod, HandleTaken);

            _registered = true;
            Log.Info("supply RPC registered");
        }

        /// <summary>
        /// Client: ask for a load. Returns null when the request went over the wire (the answer
        /// arrives as an acknowledgment) and the verdict when this machine is the server.
        /// </summary>
        public static FetchResult? Request(ZDOID bowlId, long playerId, long token, Vector3 bench,
                                           List<KeyValuePair<string, int>> items)
        {
            if (ZNet.instance == null) return FetchResult.NoClaim;

            Register();

            var package = new ZPackage();
            package.Write(token);
            package.Write(bowlId);
            package.Write(playerId);
            package.Write(bench);
            package.Write((byte)Mathf.Min(MaxItems, items != null ? items.Count : 0));

            var written = 0;
            if (items != null)
            {
                foreach (var pair in items)
                {
                    if (written >= MaxItems) break;
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0) continue;
                    package.Write(pair.Key);
                    package.Write(pair.Value);
                    written++;
                }
            }

            if (ZNet.instance.IsServer())
            {
                package.Flush();
                package.SetPos(0);
                return Execute(0L, package);
            }

            if (ZRoutedRpc.instance == null) return FetchResult.NoClaim;
            ZRoutedRpc.instance.InvokeRoutedRPC(RequestMethod, package);
            return null;
        }

        /// <summary>Server: hand the finished load to the peer that asked for it.</summary>
        public static void Deliver(long peerUid, long token, List<byte[]> blobs)
        {
            if (peerUid == 0L || blobs == null || blobs.Count == 0 || ZRoutedRpc.instance == null) return;

            var package = new ZPackage();
            package.Write(token);
            package.Write((byte)Mathf.Min(MaxStacks, blobs.Count));

            var count = 0;
            foreach (var blob in blobs)
            {
                if (count >= MaxStacks) break;
                if (blob == null || blob.Length == 0) continue;
                package.Write(blob);
                count++;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(peerUid, DeliverMethod, package);
        }

        /// <summary>Client: tell the server what actually reached the pockets.</summary>
        public static void Taken(long token, int stacks, int left)
        {
            if (ZRoutedRpc.instance == null || ZNet.instance == null) return;

            var package = new ZPackage();
            package.Write(token);
            package.Write((short)Mathf.Clamp(stacks, 0, MaxStacks));
            package.Write((short)Mathf.Clamp(left, 0, MaxStacks));

            if (ZNet.instance.IsServer())
            {
                package.Flush();
                package.SetPos(0);
                HandleTaken(0L, package);
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(TakenMethod, package);
        }

        // ------------------------------------------------------------------ server

        private static void HandleRequest(long sender, ZPackage package)
        {
            Execute(sender, package);
        }

        private static FetchResult Execute(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || package == null)
            {
                return FetchResult.NoClaim;
            }

            long token;
            ZDOID bowlId;
            long claimed;
            Vector3 bench;
            var items = new List<KeyValuePair<string, int>>();

            try
            {
                token = package.ReadLong();
                bowlId = package.ReadZDOID();
                claimed = package.ReadLong();
                bench = package.ReadVector3();
                var count = package.ReadByte();

                for (var i = 0; i < count; i++)
                {
                    var name = package.ReadString();
                    var amount = package.ReadInt();
                    if (string.IsNullOrEmpty(name) || amount <= 0) continue;
                    items.Add(new KeyValuePair<string, int>(name, amount));
                }
            }
            catch (Exception ex)
            {
                Log.Warn("supply RPC unreadable: " + ex.Message);
                return FetchResult.Nothing;
            }

            var caller = OrderRpc.ResolveCaller(sender, claimed);
            if (!caller.Valid)
            {
                Log.Warn("supply RPC dropped: sender=" + sender + " claimed=" + claimed +
                         " (the connection has no readable player identity)");
                return FetchResult.NotYours;
            }

            var verdict = SupplyService.Begin(caller, bowlId, token, bench, items);
            if (sender != 0L) Ack(sender, token, verdict, SupplyService.LastNote);

            return verdict;
        }

        private static void Ack(long uid, long token, FetchResult verdict, string note)
        {
            if (uid == 0L || ZRoutedRpc.instance == null) return;

            var package = new ZPackage();
            package.Write(token);
            package.Write((byte)verdict);
            package.Write(note ?? string.Empty);
            ZRoutedRpc.instance.InvokeRoutedRPC(uid, AckMethod, package);
        }

        private static void HandleAck(long sender, ZPackage package)
        {
            try
            {
                var token = package.ReadLong();
                var result = (FetchResult)package.ReadByte();
                var note = package.ReadString();
                CraftSupply.OnSupplyAnswer(token, result, note);
            }
            catch (Exception ex)
            {
                Log.Warn("supply ack unreadable: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ client

        private static void HandleDeliver(long sender, ZPackage package)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            long token;
            var items = new List<ItemDrop.ItemData>();

            try
            {
                token = package.ReadLong();
                var count = package.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    var blob = package.ReadByteArray();
                    var item = OrderService.DeserialiseItem(blob);
                    if (item != null) items.Add(item);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("supply delivery unreadable: " + ex.Message);
                return;
            }

            var leftovers = new List<ItemDrop.ItemData>();
            var stacks = AddToInventory(player.GetInventory(), items, leftovers);

            // A load that does not fit is set down at the player's feet: it is already out of the
            // chest, and a visible drop is recoverable where a deleted stack is not.
            for (var i = 0; i < leftovers.Count; i++)
            {
                var item = leftovers[i];
                if (item == null || item.m_shared == null) continue;
                var angle = i * 1.1f;
                var offset = new Vector3(Mathf.Cos(angle) * 0.5f, 0.2f, Mathf.Sin(angle) * 0.5f);
                ItemDrop.DropItem(item, item.m_stack, player.transform.position + offset, Quaternion.identity);
            }

            Taken(token, stacks, leftovers.Count);
            CraftSupply.OnSupplied(token, stacks);
        }

        private static void HandleTaken(long sender, ZPackage package)
        {
            try
            {
                var token = package.ReadLong();
                var stacks = package.ReadShort();
                var left = package.ReadShort();
                var fetch = Flow.FetchRunner.ByToken(token);
                if (fetch != null) fetch.NoteClientTook(stacks);

                if (left > 0)
                {
                    Log.Warn("supply: the client could not hold " + left + " stack(s) of a delivered load; " +
                             "they were set down beside the player");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("supply delivery answer unreadable: " + ex.Message);
            }
        }

        /// <summary>
        /// Puts item data into an inventory and reports how many stacks landed whole. A stack that
        /// only partly fits is trimmed by the inventory; the remainder comes back in
        /// <paramref name="leftovers"/> so the caller can set it down instead of losing it.
        /// </summary>
        public static int AddToInventory(Inventory inventory, List<ItemDrop.ItemData> items,
                                         List<ItemDrop.ItemData> leftovers = null)
        {
            if (inventory == null || items == null) return 0;

            var moved = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.m_shared == null) continue;

                var wanted = item.m_stack;
                var added = inventory.AddItem(item);
                if (added && item.m_stack > 0) { moved++; continue; }

                if (item.m_stack > 0 && item.m_stack < wanted)
                {
                    // Part of the stack landed; what is left is the remainder.
                    moved++;
                    if (leftovers != null) leftovers.Add(item);
                    continue;
                }

                if (leftovers != null && item.m_stack > 0) leftovers.Add(item);
            }

            return moved;
        }
    }
}
