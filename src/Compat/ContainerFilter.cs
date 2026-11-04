using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
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
        private static MethodInfo _isBackpackProxy;
        private static bool _proxyScanned;

        /// <summary>
        /// AdventureBackpacks turns a backpack into a proxy Container. Filling one puts loot in a
        /// player's pack, so those containers are never storage. The extension method is found by
        /// shape rather than by type name, because its type has moved between versions.
        /// </summary>
        private static bool IsBackpackProxy(Container container)
        {
            if (!_proxyScanned)
            {
                _proxyScanned = true;
                try
                {
                    PluginInfo info;
                    if (Chainloader.PluginInfos.TryGetValue("vapok.mods.adventurebackpacks", out info) && info != null && info.Instance != null)
                    {
                        var assembly = info.Instance.GetType().Assembly;
                        foreach (var type in assembly.GetTypes())
                        {
                            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                            {
                                if (method.Name != "IsBackpackProxy" || method.ReturnType != typeof(bool)) continue;
                                var parameters = method.GetParameters();
                                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Container)) continue;
                                _isBackpackProxy = method;
                                break;
                            }
                            if (_isBackpackProxy != null) break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("AdventureBackpacks scan failed: " + ex.Message);
                }
            }

            if (_isBackpackProxy == null) return false;

            try
            {
                return (bool)_isBackpackProxy.Invoke(null, new object[] { container });
            }
            catch
            {
                return false;
            }
        }

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

        /// <summary>Optional stricter rule: only wards the claim owner placed themselves.</summary>
        private static bool IsWardOwnedByClaim(Claim claim, Container container)
        {
            long wardOwner;
            if (!WardIsLoveCompat.TryGetWardOwner(container.transform.position, out wardOwner)) return true;
            return wardOwner == 0L || wardOwner == claim.OwnerId;
        }

        public static bool CanUse(Claim claim, Container container)
        {
            if (claim == null || container == null) return false;
            if (claim.Suppressed) return false;

            var inventory = container.GetInventory();
            if (inventory == null) return false;

            if (HusnisseConfig.SkipMobileContainers.Value && IsMobile(container))
            {
                Log.Debug("Compat", "skip mobile container " + container.name);
                return false;
            }

            if (IsBackpackProxy(container))
            {
                Log.Debug("Compat", "skip AdventureBackpacks proxy " + container.name);
                return false;
            }

            if (HusnisseConfig.SkipDisplayPiles.Value && IsDisplayPile(container))
            {
                Log.Debug("Compat", "skip display pile " + container.name);
                return false;
            }

            if (HusnisseConfig.RespectWards.Value && !IsWardPermitted(claim, container))
            {
                Log.Debug("Compat", "skip ward-protected container " + container.name);
                return false;
            }

            if (HusnisseConfig.RespectWards.Value && HusnisseConfig.RequireWardOwnership.Value && !IsWardOwnedByClaim(claim, container))
            {
                Log.Debug("Compat", "skip container in a ward the claim owner did not place " + container.name);
                return false;
            }

            return true;
        }
    }
}
