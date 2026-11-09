using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Flow;
using Husnisse.Localization;
using Husnisse.Net;
using Husnisse.Orders;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Bowl
{
    /// <summary>
    /// The home piece. It holds satiety and mood in its ZDO, and its Container is a four-slot pantry
    /// that accepts food only. The nisse eats one item every BowlDrainMinutes while satiety is low.
    /// </summary>
    public class BowlPiece : MonoBehaviour
    {
        private ZNetView _nview;
        private ZDO _zdo;
        private Claim _claim;
        private Container _container;
        private float _drainTimer;
        private float _tickAccum;

        public Claim Claim => _claim;

        /// <summary>
        /// Whether this particular nisse demands food. Server policy wins: when feeding is required
        /// every bowl is on; otherwise the bowl's own flag decides.
        /// </summary>
        public bool RequiresFeeding
        {
            get
            {
                if (WorldPolicy.FeedingRequired) return true;
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

            _container = GetComponent<Container>();

            if (_nview.IsOwner())
            {
                if (!_zdo.GetBool(ZdoKeys.BowlInit, false))
                {
                    var piece = GetComponent<Piece>();
                    var creator = piece != null ? piece.GetCreator() : 0L;
                    _zdo.Set(ZdoKeys.BowlOwner, creator);
                    _zdo.Set(ZdoKeys.BowlRadius, HusnisseConfig.ClaimRadius.Value);
                    _zdo.Set(ZdoKeys.BowlSatiety, 25f);
                    _zdo.Set(ZdoKeys.BowlMood, 100f);
                    _zdo.Set(ZdoKeys.BowlInit, true);
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

        private void Start()
        {
            if (_container == null)
            {
                Log.Error("bowl: no Container component; the pantry UI will not open");
                return;
            }

            var inventory = _container.GetInventory();
            if (inventory == null)
            {
                Log.Error("bowl: container has no inventory (width=" + _container.m_width + " height=" + _container.m_height + ")");
                return;
            }

            if (inventory.GetWidth() != 4 || inventory.GetHeight() != 1)
            {
                Log.Warn("bowl: inventory is " + inventory.GetWidth() + "x" + inventory.GetHeight() + ", expected 4x1");
            }

            BowlFoodGuard.Register(_container);
        }

        private void OnDestroy()
        {
            if (_container != null) BowlFoodGuard.Unregister(_container);
            if (_claim == null) return;

            FlowQueue.Forget(_claim.BowlId);
            ClaimRegistry.Unregister(_claim.BowlId);
            NisseVoice.Forget(_claim.BowlId);
            NisseFactory.Despawn(_claim.BowlId);
        }

        /// <summary>
        /// Server-side tick: decay satiety and mood, and eat one item from the bowl at the configured
        /// interval while satiety is below full. An empty bowl decays into hunger.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || !RequiresFeeding) return;

            // ZDO writes are network traffic; batch the slow simulation into ten-second steps.
            _tickAccum += dt;
            if (_tickAccum < 10f) return;

            var step = _tickAccum;
            _tickAccum = 0f;

            Satiety = Satiety - step / 3600f * 8f;
            Mood = Mood - step / 3600f * HusnisseConfig.MoodDecayPerHour.Value;
            if (IsFed) Mood = Mood + step / 3600f * 10f;

            if (Satiety >= 99.5f)
            {
                _drainTimer = 0f;
                return;
            }

            if (Satiety <= 0f && TryEatOne()) return;

            var interval = Mathf.Max(60f, HusnisseConfig.BowlDrainMinutes.Value * 60f);
            _drainTimer += step;
            while (_drainTimer >= interval)
            {
                _drainTimer -= interval;
                if (!TryEatOne()) break;
            }
        }

        private bool TryEatOne()
        {
            var inventory = _container != null ? _container.GetInventory() : null;
            if (inventory == null) return false;

            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null || item.m_shared.m_food <= 0f) continue;

                if (!inventory.RemoveItem(item, 1)) continue;

                Satiety = Satiety + item.m_shared.m_food;
                Mood = Mood + 10f;
                Log.Debug("Bowl", "ate " + item.m_shared.m_name + " -> satiety=" + Satiety.ToString("0") +
                                   " mood=" + Mood.ToString("0"));

                if (SulkLevel > 0)
                {
                    if (_zdo != null) _zdo.Set(ZdoKeys.BowlSulk, 0);
                    if (_claim != null) NisseVoice.Say(_claim, Loc.T(Loc.VoiceForgive), true);
                }

                return true;
            }

            return false;
        }

        public string DisplayName()
        {
            return Loc.T(Loc.BowlName);
        }

        public string HoverText()
        {
            var name = DisplayName();

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

        /// <summary>Alt-interact requests the toggle from the server; the server owns the ZDO write.</summary>
        public void ToggleFeeding(Humanoid user)
        {
            var player = user as Player;
            if (player == null) return;

            if (WorldPolicy.FeedingRequired)
            {
                Message(user, Loc.T(Loc.BowlServerForced));
                return;
            }

            if (!IsOwner(player.GetPlayerID()))
            {
                Message(user, Loc.T(Loc.BowlOnlyOwner));
                return;
            }

            if (ZNet.instance == null || _claim == null)
            {
                ApplyFeedingToggle(player.GetPlayerID());
                return;
            }

            OrderRpc.Send(OrderKind.ToggleFeeding, _claim.BowlId, ZDOID.None, player.GetPlayerID());
        }

        /// <summary>Server-side write for a feeding toggle request.</summary>
        public void ServerToggleFeeding(long playerId)
        {
            if (WorldPolicy.FeedingRequired) return;
            if (!IsOwner(playerId)) return;

            var player = Player.GetPlayer(playerId);
            ApplyFeedingToggle(playerId);
            if (player != null)
            {
                Message(player, Loc.T(_zdo.GetBool(ZdoKeys.BowlFeeding, false) ? Loc.BowlOfferOn : Loc.BowlOfferOff));
            }
        }

        private void ApplyFeedingToggle(long playerId)
        {
            if (_zdo == null) return;

            var next = !_zdo.GetBool(ZdoKeys.BowlFeeding, false);
            _zdo.Set(ZdoKeys.BowlFeeding, next);
            Log.Debug("Bowl", "feeding=" + next + " by " + playerId);
        }

        private bool IsOwner(long playerId)
        {
            var owner = _zdo != null ? _zdo.GetLong(ZdoKeys.BowlOwner, 0L) : 0L;
            return owner == 0L || playerId == owner;
        }

        private static void Message(Humanoid user, string text)
        {
            var player = user as Player;
            if (player != null) player.Message(MessageHud.MessageType.Center, text);
        }
    }
}
