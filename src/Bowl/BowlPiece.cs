using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Localization;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Bowl
{
    /// <summary>
    /// The home piece. It owns the claim, holds satiety and mood in its ZDO, and is the only way to
    /// feed the nisse. Everything the nisse does is gated on this bowl being fed.
    /// </summary>
    public class BowlPiece : MonoBehaviour, Interactable, Hoverable
    {
        private ZNetView _nview;
        private ZDO _zdo;
        private Claim _claim;

        public Claim Claim => _claim;

        /// <summary>
        /// Whether this particular nisse demands food. Server policy wins: Disabled turns the care loop
        /// off for everyone, Required turns the per-bowl choice off, Optional reads the bowl's own flag.
        /// </summary>
        public bool RequiresFeeding
        {
            get
            {
                if (WorldPolicy.FeedingRequired) return true;

                // Not forced means opt-in: a new bowl asks for nothing until its owner turns feeding on.
                return _zdo != null && _zdo.GetBool(ZdoKeys.BowlFeeding, false);
            }
        }

        public bool IsFed => !RequiresFeeding || Satiety > 0f;

        public float Satiety
        {
            get { return _zdo == null ? 0f : _zdo.GetFloat(ZdoKeys.BowlSatiety, 0f); }
            private set { if (_zdo != null) _zdo.Set(ZdoKeys.BowlSatiety, Mathf.Clamp(value, 0f, 100f)); }
        }

        public float Mood
        {
            get { return _zdo == null ? 100f : _zdo.GetFloat(ZdoKeys.BowlMood, 100f); }
            private set { if (_zdo != null) _zdo.Set(ZdoKeys.BowlMood, Mathf.Clamp(value, 0f, 100f)); }
        }

        public int SulkLevel
        {
            get { return _zdo == null ? 0 : _zdo.GetInt(ZdoKeys.BowlSulk, 0); }
        }

        /// <summary>True when enough time has passed since the last misfiling.</summary>
        public bool SulkDue(float minutes)
        {
            if (_zdo == null) return false;
            var last = _zdo.GetLong(ZdoKeys.BowlLastSulk, 0L);
            if (last == 0L) return true;
            var now = ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;
            return now - last >= System.TimeSpan.FromMinutes(minutes).Ticks;
        }

        public void RegisterSulk()
        {
            if (_zdo == null) return;
            _zdo.Set(ZdoKeys.BowlSulk, SulkLevel + 1);
            _zdo.Set(ZdoKeys.BowlLastSulk, ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L);
        }

        public float Radius
        {
            get { return _zdo == null ? HusnisseConfig.ClaimRadius.Value : _zdo.GetFloat(ZdoKeys.BowlRadius, HusnisseConfig.ClaimRadius.Value); }
        }

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || !_nview.IsValid()) return;

            _zdo = _nview.GetZDO();
            if (_zdo == null) return;

            if (_nview.IsOwner())
            {
                if (!_zdo.GetBool("husnisse_bowl_init", false))
                {
                    var piece = GetComponent<Piece>();
                    var creator = piece != null ? piece.GetCreator() : 0L;
                    _zdo.Set(ZdoKeys.BowlOwner, creator);
                    _zdo.Set(ZdoKeys.BowlRadius, HusnisseConfig.ClaimRadius.Value);
                    _zdo.Set(ZdoKeys.BowlSatiety, 25f);
                    _zdo.Set(ZdoKeys.BowlMood, 100f);
                    _zdo.Set("husnisse_bowl_init", true);
                }

                NisseFactory.EnsureNisse(this);
            }

            var owner = _zdo.GetLong(ZdoKeys.BowlOwner, 0L);

            var suppressed = false;
            if (ClaimRegistry.OverlapsAny(transform.position, Radius, _zdo.m_uid))
            {
                suppressed = true;
                Log.Warn("bowl at " + transform.position + " overlaps an existing claim; it refuses to settle");
            }
            if (HusnisseConfig.RespectWards.Value && !WardIsLoveCompat.IsPermitted(owner, transform.position))
            {
                suppressed = true;
                Log.Warn("bowl at " + transform.position + " is inside a ward its owner cannot use; it refuses to settle");
            }
            if (HusnisseConfig.RespectWards.Value && HusnisseConfig.RequireWardOwnership.Value)
            {
                long wardOwner;
                if (WardIsLoveCompat.TryGetWardOwner(transform.position, out wardOwner) && wardOwner != 0L && wardOwner != owner)
                {
                    suppressed = true;
                    Log.Warn("bowl at " + transform.position + " is inside another player's ward; it refuses to settle");
                }
            }

            _claim = new Claim(_zdo.m_uid, transform.position, Radius, owner, this, suppressed);
            ClaimRegistry.Register(_claim);
            Log.Debug("Bowl", "ready " + _zdo.m_uid + " owner=" + owner + " r=" + Radius.ToString("0") +
                               " satiety=" + Satiety.ToString("0") + " mood=" + Mood.ToString("0") +
                               " suppressed=" + suppressed);
        }

        private void OnDestroy()
        {
            if (_claim == null) return;
            ClaimRegistry.Unregister(_claim.BowlId);
            NisseVoice.Forget(_claim.BowlId);
            NisseFactory.Despawn(_claim.BowlId);
        }

        /// <summary>Feed from a player's inventory. One food item per interaction.</summary>
        public bool FeedFrom(Humanoid user)
        {
            var inventory = user != null ? user.GetInventory() : null;
            if (inventory == null) return false;

            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null || item.m_shared.m_food <= 0f) continue;

                inventory.RemoveItem(item, 1);
                Satiety += item.m_shared.m_food;
                Mood = Mood + 10f;
                Log.Debug("Bowl", "fed " + item.m_shared.m_name + " -> satiety=" + Satiety.ToString("0") +
                                   " mood=" + Mood.ToString("0"));

                if (SulkLevel > 0)
                {
                    if (_zdo != null) _zdo.Set(ZdoKeys.BowlSulk, 0);
                    if (_claim != null) NisseVoice.Say(_claim, Loc.T(Loc.VoiceForgive), true);
                }

                Log.Info($"bowl fed with {item.m_shared.m_name}: satiety={Satiety:0} mood={Mood:0}");
                return true;
            }

            return false;
        }

        /// <summary>Called by the round scheduler. Decay happens only while a player is present,
        /// and not at all when feeding is switched off.</summary>
        public void Decay(float hours)
        {
            if (hours <= 0f || !RequiresFeeding) return;
            Satiety = Satiety - hours * 8f;
            Mood = Mood - hours * HusnisseConfig.MoodDecayPerHour.Value;

            if (IsFed) Mood = Mood + hours * 10f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            if (alt) return ToggleFeeding(user);

            if (!RequiresFeeding)
            {
                Message(user, Loc.T(Loc.BowlNoFeed));
                return true;
            }

            if (!IsOwner())
            {
                Message(user, Loc.T(Loc.BowlNotYours));
                return true;
            }

            if (FeedFrom(user))
            {
                Message(user, Loc.T(Loc.BowlFed));
                return true;
            }

            Message(user, Satiety > 0f ? Loc.T(Loc.BowlContent) : Loc.T(Loc.BowlNeedsFood));
            return true;
        }

        /// <summary>The owner's own choice, available only when the server does not force feeding.</summary>
        private bool ToggleFeeding(Humanoid user)
        {
            var policy = WorldPolicy.FeedingRequired;

            if (policy)
            {
                Message(user, Loc.T(Loc.BowlServerForced));
                return true;
            }

            if (!IsOwner())
            {
                Message(user, Loc.T(Loc.BowlOnlyOwner));
                return true;
            }

            if (_zdo == null) return true;

            var next = !_zdo.GetBool(ZdoKeys.BowlFeeding, false);
            _zdo.Set(ZdoKeys.BowlFeeding, next);
            Message(user, Loc.T(next ? Loc.BowlOfferOn : Loc.BowlOfferOff));
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        public string GetHoverName()
        {
            return Loc.T(Loc.BowlName);
        }

        public string GetHoverText()
        {
            var name = Loc.T(Loc.BowlName);

            if (!RequiresFeeding)
            {
                return name + "\n" + Loc.T(Loc.BowlAltRequire);
            }

            var body = Satiety > 0f
                ? string.Format(Loc.T(Loc.BowlHover), Satiety, Mood)
                : Loc.T(Loc.BowlHoverEmpty);

            var text = name + "\n" + body;
            if (!WorldPolicy.FeedingRequired) text += Loc.T(Loc.BowlAltOff);
            return text;
        }

        public float GetHoverOffset()
        {
            return 0.3f;
        }

        private bool IsOwner()
        {
            var player = Player.m_localPlayer;
            if (player == null) return true; // headless: no player, allow
            var owner = _zdo != null ? _zdo.GetLong(ZdoKeys.BowlOwner, 0L) : 0L;
            return owner == 0L || player.GetPlayerID() == owner;
        }

        private static void Message(Humanoid user, string text)
        {
            var player = user as Player;
            if (player != null) player.Message(MessageHud.MessageType.Center, text);
        }
    }
}
