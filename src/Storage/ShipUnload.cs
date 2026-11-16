using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Flow;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// Ship unloading (Quartermaster's Helmsman surface, done natively): a ship's hold inside a claim
    /// is a source, never a destination. The nisse carries what is aboard to the chests whose rules
    /// claim it at the queue's rate, one visible trip at a time — and a stack nothing accepts stays
    /// aboard, exactly like an unmatched stack in a deposit chest.
    ///
    /// A ship hold is exempt from the "Skip Carts and Ships" class blacklist only while this feature
    /// is on: that setting is about filing goods *into* something that moves, and unloading is the
    /// opposite. A per-container blacklist (`husnisse_rule blacklist`) still refuses one boat.
    ///
    /// The hold is not on the container network, so it can never be chosen as a destination. If the
    /// ship leaves the claim while a stack is queued or being carried, the ticket becomes invalid and
    /// the stack stays where it is; the next reconcile queues it again only if the ship comes back.
    /// </summary>
    public static class ShipUnload
    {
        private static int _holds;
        private static int _queued;
        private static string _last = "none";

        /// <summary>Resets the per-reconcile counters; called once before the claims are refilled.</summary>
        public static void BeginPass()
        {
            _holds = 0;
        }

        /// <summary>
        /// Queues every stack aboard a ship inside this claim. The claim's gathered container list is
        /// reused for routing so an unloading pass does not rescan the base.
        /// </summary>
        public static void Enqueue(Claim claim, List<Container> containers)
        {
            if (!HusnisseConfig.UnloadShips.Value) return;
            if (claim == null || claim.Suppressed || claim.Bowl == null) return;

            var ships = Object.FindObjectsByType<Ship>(FindObjectsSortMode.None);
            if (ships == null || ships.Length == 0) return;

            List<Container> network = null;

            foreach (var ship in ships)
            {
                if (ship == null) continue;
                if (!claim.Contains(ship.transform.position)) continue;

                foreach (var container in ship.GetComponentsInChildren<Container>(true))
                {
                    if (container == null) continue;
                    if (container.GetComponent<Bowl.BowlPiece>() != null) continue;
                    if (ContainerNetwork.HasFlagBlacklist(container)) continue;

                    var inventory = container.GetInventory();
                    if (inventory == null || inventory.NrOfItems() == 0) continue;

                    _holds++;
                    _last = Utils.GetPrefabName(container.gameObject);

                    for (var i = 0; i < inventory.NrOfItems(); i++)
                    {
                        var item = inventory.GetItem(i);
                        if (item == null || item.m_shared == null) continue;

                        if (network == null)
                        {
                            network = StorageRouter.NetworkCandidates(claim, containers);
                        }

                        // Nothing on the base claims this stack: leave it aboard rather than queue
                        // work that would be dropped at the first trip. The reason is logged where
                        // every other unmatched stack is, so a hold that will not empty is legible.
                        var decision = StorageRouter.Decide(claim, network, item, null, container);
                        if (!decision.Found)
                        {
                            Log.Debug("Flow", "ship unload leaves " + item.m_shared.m_name + " aboard (" +
                                              decision.Why + ") claim=" + claim.BowlId);
                            continue;
                        }

                        if (FlowQueue.EnqueueContainer(claim, container, item)) _queued++;
                    }
                }
            }
        }

        /// <summary>One line for `husnisse_status`: the switch, last pass and what it has queued.</summary>
        public static string Describe()
        {
            return "unload=" + (HusnisseConfig.UnloadShips.Value ? "on" : "off") +
                   " holds=" + _holds +
                   " queued=" + _queued +
                   " last=" + _last;
        }
    }
}
