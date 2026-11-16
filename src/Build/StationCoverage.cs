using System;
using Husnisse.Claims;
using Husnisse.Claims.Authority;
using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Build
{
    /// <summary>
    /// Building coverage — Quartermaster's "a station anywhere in the deposit chest's area supports
    /// building throughout that area", with the claim as the area. The station must still be the
    /// right kind and it must stand on the same claim as the player asking: vanilla's own nearby
    /// range keeps working, and this only answers a question vanilla answered with "nothing".
    ///
    /// One static gate is enough because vanilla routes every relevant action through it:
    /// <c>Player.HaveRequirements</c> (placing a piece), the placement-cost check, and
    /// <c>Player.CheckCanRemovePiece</c> (repair and dismantle) all ask
    /// <c>CraftingStation.HaveBuildStationInRange</c> from the player's position. Crafting, item
    /// upgrades and item repair never ask it — those still require walking up to the station.
    /// </summary>
    public static class StationCoverage
    {
        private static int _extensions;

        /// <summary>How many times this session a vanilla "not in range" was answered by the claim.</summary>
        public static int Extensions
        {
            get { return _extensions; }
        }

        /// <summary>
        /// The station of this name standing anywhere inside the claim that contains
        /// <paramref name="point"/>, or null. Called only from the postfix when vanilla found none.
        /// </summary>
        public static CraftingStation Find(string name, Vector3 point)
        {
            if (!HusnisseConfig.ExtendStationCoverage.Value) return null;
            if (string.IsNullOrEmpty(name)) return null;

            var claim = ClaimRegistry.FindFor(point);
            if (claim == null || claim.Suppressed || claim.Bowl == null) return null;

            // A ward is the only source of an ACL here: inside one, only the owner and the players
            // the ward permits may borrow its stations, matching PrivateArea.CheckAccess. A native
            // claim has no access list — vanilla lets anyone build in the open — so it must not
            // invent one. The server (no local player) always answers.
            var player = Player.m_localPlayer;
            if (claim.Source == ClaimSource.Ward && player != null && !claim.Allows(player.GetPlayerID()))
            {
                return null;
            }

            foreach (var updater in CraftingStation.Instances)
            {
                var station = updater as CraftingStation;
                if (station == null) continue;
                if (!string.Equals(station.m_name, name, StringComparison.Ordinal)) continue;
                if (!claim.Contains(station.transform.position)) continue;

                _extensions++;
                Log.Debug("Build", "coverage extended " + name + " claim=" + claim.BowlId +
                                   " station=" + Where(station.transform.position) +
                                   " at=" + Where(point));
                return station;
            }

            return null;
        }

        /// <summary>One line for `husnisse_status`: the switch, the stations in claims and the use count.</summary>
        public static string Describe()
        {
            var stations = 0;
            foreach (var claim in ClaimRegistry.All())
            {
                foreach (var updater in CraftingStation.Instances)
                {
                    var station = updater as CraftingStation;
                    if (station != null && claim.Contains(station.transform.position)) stations++;
                }
            }

            return "coverage=" + (HusnisseConfig.ExtendStationCoverage.Value ? "on" : "off") +
                   " stations=" + stations +
                   " extensions=" + _extensions;
        }

        private static string Where(Vector3 point)
        {
            return "(" + point.x.ToString("0.#") + "," + point.z.ToString("0.#") + ")";
        }
    }
}
