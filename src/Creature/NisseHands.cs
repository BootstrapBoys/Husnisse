using Husnisse.Claims;
using Husnisse.Localization;
using Husnisse.Orders;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// Lets the player hand the nisse their pockets. Interact on the creature, and it queues what it
    /// can carry; the server does the moving.
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
            return Loc.T(Loc.HandHover);
        }

        public float GetHoverOffset()
        {
            return 0.5f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || alt) return false;

            var player = user as Player;
            if (player == null || _actor == null) return false;

            var claim = ClaimRegistry.Find(_actor.BowlId);
            if (claim == null)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNoClaim));
                return true;
            }

            return OrderService.RequestEmptyPockets(player, claim);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }
}
