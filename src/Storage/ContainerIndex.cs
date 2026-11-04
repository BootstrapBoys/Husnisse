using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// Finds the containers a claim can use. A linear scan is deliberate for 0.1: it is one pass per
    /// round, bounded by the claim radius, and the 200-container benchmark decides whether it needs an
    /// index later.
    /// </summary>
    public static class ContainerIndex
    {
        public static List<Container> InClaim(Claim claim)
        {
            var result = new List<Container>();
            foreach (var container in Object.FindObjectsOfType<Container>())
            {
                if (container == null) continue;
                if (!claim.Contains(container.transform.position)) continue;
                result.Add(container);
            }
            return result;
        }

        public static Container ById(Claim claim, ZDOID id)
        {
            foreach (var container in Object.FindObjectsOfType<Container>())
            {
                if (container == null || !claim.Contains(container.transform.position)) continue;
                var view = container.GetComponent<ZNetView>();
                if (view != null && view.GetZDO() != null && view.GetZDO().m_uid == id) return container;
            }
            return null;
        }

        public static string Role(Container container)
        {
            var view = container != null ? container.GetComponent<ZNetView>() : null;
            var zdo = view != null ? view.GetZDO() : null;
            return zdo == null ? string.Empty : zdo.GetString(ZdoKeys.ContainerRole, string.Empty);
        }

        public static int FreeSlots(Container container)
        {
            var inventory = container != null ? container.GetInventory() : null;
            return inventory == null ? 0 : inventory.GetEmptySlots();
        }

        public static bool HasRoom(Container container)
        {
            return FreeSlots(container) > 0;
        }
    }
}
