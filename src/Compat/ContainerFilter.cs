using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Compat
{
    /// <summary>
    /// Which containers a claim may use. Every rule here exists because the pack contains a mod that
    /// would otherwise make the nisse put items somewhere they should not go.
    /// </summary>
    public static class ContainerFilter
    {
        /// <summary>Mobile containers: sorting onto a cart or a ship lets the goods sail away.</summary>
        private static bool IsMobile(Container container)
        {
            if (container == null) return true;
            if (container.GetComponentInParent<Vagon>() != null) return true;
            if (container.GetComponentInParent<Ship>() != null) return true;
            return false;
        }

        /// <summary>
        /// Display piles from DynamicStoragePiles and the Muji DynamicStorage family. They look like
        /// containers but exist to show items, and filling them fights the mod that owns them.
        /// </summary>
        private static bool IsDisplayPile(Container container)
        {
            foreach (var component in container.GetComponents<Component>())
            {
                if (component == null) continue;
                var name = component.GetType().FullName ?? string.Empty;
                if (name.IndexOf("DynamicStorage", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            var prefab = Utils.GetPrefabName(container.gameObject);
            return prefab.StartsWith("DS_", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Wards are the owner's permission list; the nisse is not above it.</summary>
        private static bool IsWardPermitted(Claim claim, Container container)
        {
            return WardIsLoveCompat.IsPermitted(claim.OwnerId, container.transform.position);
        }

        public static bool CanUse(Claim claim, Container container)
        {
            if (claim == null || container == null) return false;
            if (claim.Suppressed) return false;

            var inventory = container.GetInventory();
            if (inventory == null) return false;

            if (HusnisseConfig.SkipMobileContainers.Value && IsMobile(container))
            {
                Log.Debug("skip mobile container " + container.name);
                return false;
            }

            if (HusnisseConfig.SkipDisplayPiles.Value && IsDisplayPile(container))
            {
                Log.Debug("skip display pile " + container.name);
                return false;
            }

            if (HusnisseConfig.RespectWards.Value && !IsWardPermitted(claim, container))
            {
                Log.Debug("skip ward-protected container " + container.name);
                return false;
            }

            return true;
        }
    }
}
