using Husnisse.Claims;
using Husnisse.Localization;
using Husnisse.Orders;
using Husnisse.Teaching;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// Lets the player hand the nisse their pockets, and show it where things go.
    ///
    /// Interact is the Empty Pockets order; Alt-interact makes the nisse watch, and the next stack the
    /// player puts into a chest inside the claim teaches that chest. Alt is the mod's "second
    /// meaning" key everywhere else (Alt on a chest is Tidy, Alt on the bowl is feeding), so the
    /// teaching gesture stays one key.
    /// </summary>
    public class NisseHands : MonoBehaviour, Interactable, Hoverable
    {
        private NisseActor _actor;

        private void Awake()
        {
            _actor = GetComponent<NisseActor>();
        }

        public string GetHoverName()
        {
            return Loc.T(Loc.HandName);
        }

        public string GetHoverText()
        {
            if (Teach.Active) return Loc.T(Loc.TeachWatching);
            return Loc.T(Loc.HandHover) + "\n" + Loc.T(Loc.HandTeach);
        }

        public float GetHoverOffset()
        {
            return 0.5f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            var player = user as Player;
            if (player == null || _actor == null) return false;

            var claim = ClaimRegistry.Find(_actor.BowlId);
            if (claim == null)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNoClaim));
                return true;
            }

            if (alt) return Teach.Begin(player, claim);

            return OrderService.RequestEmptyPockets(player, claim);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }
}
