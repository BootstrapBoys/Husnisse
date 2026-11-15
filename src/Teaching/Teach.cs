using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Localization;
using Husnisse.Net;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Teaching
{
    /// <summary>
    /// Teaching by demonstration: the nisse watches where a sample goes, then keeps that kind of
    /// thing there.
    ///
    /// The gesture is the deposit itself. Alt-interact the nisse to make it watch, then move one
    /// stack into the chest it belongs in; the move is observed where the game performs it
    /// (<see cref="Inventory.MoveItemToThis"/>, the only path a player's drag or shift-click takes),
    /// and the chest's rule becomes a custom list with that item in it. Nothing is written until the
    /// player has actually shown the nisse the place, and nothing is inferred from a move made while
    /// the nisse is not watching — a base full of chests must never rewrite its own rules.
    ///
    /// Why not "hand the nisse the item"? Valheim lets a player hold weapons, tools and shields, not
    /// a material stack, so there is no held item to hand over. The sample in the chest is also the
    /// model Quartermaster players already know: putting a sample back teaches the chest again.
    ///
    /// The writes go through <see cref="RuleRpc"/> like every other rule: one writer, server-validated.
    /// </summary>
    public static class Teach
    {
        /// <summary>How long the nisse watches after Alt-interact. Long enough to walk to a chest,
        /// short enough that an old window cannot silently catch an unrelated move.</summary>
        private const float WindowSeconds = 120f;

        /// <summary>
        /// The player the nisse is watching for. A reference, not a player id: the id is 0 on a
        /// synthetic player (the harness, a probe) and on this build's own client before its
        /// character ZDO is authored, and a window keyed on 0 would never open. The local player
        /// object is stable for the life of a character, and a respawn is a different gesture anyway.
        /// </summary>
        private static Player _watcher;
        private static float _until;

        public static bool Active
        {
            get
            {
                var player = Player.m_localPlayer;
                if (_watcher == null || player == null) return false;
                if (!ReferenceEquals(player, _watcher)) return false;
                return Time.time < _until;
            }
        }

        public static float Remaining
        {
            get { return Active ? Mathf.Max(0f, _until - Time.time) : 0f; }
        }

        /// <summary>Alt-interact the nisse: watch, or stop watching.</summary>
        public static bool Begin(Player player, Claim claim)
        {
            if (player == null) return false;

            if (Active)
            {
                Cancel();
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.TeachStop));
                return true;
            }

            if (claim == null || claim.Bowl == null)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNoClaim));
                return true;
            }

            if (!claim.Allows(player.GetPlayerID()))
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.BowlNotYours));
                return true;
            }

            _watcher = player;
            _until = Time.time + WindowSeconds;
            player.Message(MessageHud.MessageType.Center, Loc.T(Loc.TeachBegin));
            NisseVoice.Say(claim, Loc.T(Loc.VoiceWatch), NisseVoice.Kind.Event);
            Note("watching player=" + player.GetPlayerID() + " name=" + player.GetPlayerName() +
                 " claim=" + claim.BowlId + " for=" + WindowSeconds + "s");
            return true;
        }

        public static void Cancel()
        {
            if (_watcher != null) Note("stopped watching player=" + _watcher.GetPlayerID());
            _watcher = null;
            _until = 0f;
        }

        /// <summary>
        /// Called after a player-driven move: the sample landed in a container the claim owns, so
        /// the nisse learns it. Returns whether anything was written.
        /// </summary>
        public static bool Observe(Inventory to, Inventory from, ItemDrop.ItemData item)
        {
            if (!Active || to == null || from == null || item == null || item.m_shared == null) return false;

            var player = Player.m_localPlayer;
            if (player == null) return false;
            if (!ReferenceEquals(from, player.GetInventory())) return false;

            var container = ContainerIndex.ByInventory(to);
            if (container == null) return false;
            if (container.GetComponent<Bowl.BowlPiece>() != null) return false;

            var name = item.m_shared.m_name;
            if (string.IsNullOrEmpty(name)) return false;

            var claim = ClaimRegistry.FindFor(container.transform.position);
            if (claim == null) return false;

            if (!claim.Allows(player.GetPlayerID()))
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.BowlNotYours));
                return false;
            }

            if (!ContainerNetwork.OffersUi(claim, container))
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.TeachNotHere));
                return false;
            }

            var rule = ContainerNetwork.RuleOf(container);
            switch (rule.Kind)
            {
                case RuleKind.None:
                {
                    var learned = new ContainerRule { Kind = RuleKind.Custom };
                    learned.Items.Add(name);
                    RuleRpc.SendRuleAndNetwork(container.transform.position, player.GetPlayerID(), learned);
                    Report(player, Loc.TeachLearned, name, container, learned);
                    return true;
                }

                case RuleKind.Custom:
                {
                    if (rule.Items != null && rule.Items.Contains(name))
                    {
                        player.Message(MessageHud.MessageType.Center,
                                       Loc.T(Loc.TeachKnown, DisplayName(name)));
                        Note("already knows name=" + name + " chest=" + Where(container));
                        return false;
                    }

                    var learned = rule.Clone();
                    learned.Items.Add(name);
                    learned.Sanitise();
                    RuleRpc.SendRuleAndNetwork(container.transform.position, player.GetPlayerID(), learned);
                    Report(player, Loc.TeachLearned, name, container, learned);
                    return true;
                }

                case RuleKind.Category:
                {
                    if (CategoryMap.Accepts(rule.Category, item))
                    {
                        player.Message(MessageHud.MessageType.Center,
                                       Loc.T(Loc.TeachKnown, DisplayName(name)));
                        Note("already knows name=" + name + " chest=" + Where(container) + " rule=" + rule.Describe());
                        return false;
                    }

                    Refuse(player, name, rule, container);
                    return false;
                }

                default:
                    Refuse(player, name, rule, container);
                    return false;
            }
        }

        private static void Refuse(Player player, string name, ContainerRule rule, Container container)
        {
            player.Message(MessageHud.MessageType.Center,
                           Loc.T(Loc.TeachRefused, DisplayName(name), RuleText.Describe(rule)));
            Note("refused name=" + name + " chest=" + Where(container) + " rule=" + rule.Describe());
        }

        private static void Report(Player player, string token, string name, Container container, ContainerRule rule)
        {
            player.Message(MessageHud.MessageType.Center, Loc.T(token, DisplayName(name)));
            Note("learned name=" + name + " chest=" + Where(container) + " rule=" + rule.Describe());
        }

        private static string DisplayName(string sharedName)
        {
            try
            {
                var text = global::Localization.instance.Localize(sharedName);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            catch (System.Exception)
            {
                // Steamworks is not up yet on a joining client; the token is still readable.
            }
            return sharedName;
        }

        private static string Where(Container container)
        {
            var point = container.transform.position;
            return Utils.GetPrefabName(container.gameObject) + "@" +
                   point.x.ToString("0.#") + "," + point.z.ToString("0.#");
        }

        private static void Note(string line)
        {
            Log.Debug("Teach", line);
            DiagnosticsFile.Line("[Teach] " + line);
        }
    }
}
