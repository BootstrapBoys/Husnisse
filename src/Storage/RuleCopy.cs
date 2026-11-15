using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// Copying one container's rule to its neighbours (improvement 5). "Nearby" means the same
    /// prefab within a short reach inside the same claim — a row of wooden chests, not the barrel two
    /// rooms away — and only to containers that are already on the network, because a rule on a
    /// container nobody opted in would be a decision the player did not make.
    ///
    /// The UI asks first (the improvement is behind a confirm) and then sends one rule request per
    /// target through the ordinary server path, so each write is validated where it lands. The
    /// console path here does the same work in one call for a headless proof.
    /// </summary>
    public static class RuleCopy
    {
        /// <summary>Reach from the source; a chest wall is usually within one room.</summary>
        public const float DefaultRadius = 10f;

        public static List<Container> Targets(Claim claim, Container source, float radius)
        {
            var targets = new List<Container>();
            if (claim == null || source == null) return targets;
            if (radius <= 0f) radius = DefaultRadius;

            var prefab = Utils.GetPrefabName(source.gameObject);
            var origin = source.transform.position;

            foreach (var container in ContainerIndex.InClaim(claim))
            {
                if (container == null || container == source) continue;
                if (Utils.GetPrefabName(container.gameObject) != prefab) continue;
                if (Vector3.Distance(container.transform.position, origin) > radius) continue;
                if (!ContainerNetwork.IsOnNetwork(container)) continue;
                if (ContainerNetwork.IsBlacklisted(container)) continue;
                targets.Add(container);
            }

            return targets;
        }

        /// <summary>
        /// Writes the source's rule to every target that does not already carry it. Returns how many
        /// containers changed, so a caller can say "3 chests" or report that there was nothing to do.
        /// </summary>
        public static int Copy(Claim claim, Container source, float radius, string who)
        {
            if (claim == null || source == null) return 0;

            var rule = ContainerNetwork.RuleOf(source);
            if (rule.IsNone)
            {
                Log.Debug("Rules", "copy refused: the source has no rule");
                return 0;
            }

            var changed = 0;
            foreach (var target in Targets(claim, source, radius))
            {
                if (ContainerNetwork.RuleOf(target).SameAs(rule)) continue;
                if (ContainerNetwork.WriteRule(target, rule.Clone(), who + " (copied)")) changed++;
            }

            Log.Debug("Rules", "copied " + rule.Describe() + " from " + Utils.GetPrefabName(source.gameObject) +
                               " to " + changed + " container(s)");
            return changed;
        }
    }
}
