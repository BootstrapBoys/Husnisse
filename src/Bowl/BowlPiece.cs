using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Creature;
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
        public bool IsFed => Satiety > 0f;

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

            _claim = new Claim(_zdo.m_uid, transform.position, Radius, owner, this, suppressed);
            ClaimRegistry.Register(_claim);
        }

        private void OnDestroy()
        {
            if (_claim != null) ClaimRegistry.Unregister(_claim.BowlId);
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
                Log.Info($"bowl fed with {item.m_shared.m_name}: satiety={Satiety:0} mood={Mood:0}");
                return true;
            }

            return false;
        }

        /// <summary>Called by the round scheduler. Decay happens only while a player is present.</summary>
        public void Decay(float hours)
        {
            if (hours <= 0f) return;
            Satiety = Satiety - hours * 8f;
            Mood = Mood - hours * HusnisseConfig.MoodDecayPerHour.Value;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            if (!IsOwner())
            {
                Message(user, "The nisse's bowl is not yours to fill.");
                return true;
            }

            if (FeedFrom(user))
            {
                Message(user, "The nisse takes the offering.");
                return true;
            }

            Message(user, Satiety > 0f ? "The bowl is full enough." : "The nisse needs food.");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        public string GetHoverName()
        {
            return "Porridge Bowl";
        }

        public string GetHoverText()
        {
            return string.Format("Porridge Bowl\nSatiety {0:0}/100   Mood {1:0}/100\n[E] offer food", Satiety, Mood);
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
