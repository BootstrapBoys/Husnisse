using Husnisse.Core;

namespace Husnisse.Storage
{
    /// <summary>
    /// A container's contents are written by its ZDO's owner and nobody else. <c>Container.Save</c>
    /// runs from the inventory's change event and returns without a word when this machine is not the
    /// owner, so a server-side add into a container whose ZDO belongs to a peer — every chest a player
    /// has opened, and every chest a client configured — stays in the server's memory, is never
    /// serialised, and is overwritten by the owner's next write. The deposit looks successful on the
    /// server and the items are simply gone.
    ///
    /// Every server-side inventory move on the flow path goes through here: take the ZDO first (the
    /// game hands ownership back to the next player who opens the chest), let the game's own change
    /// event save it, then read the packed inventory back out of the ZDO and write it again if the
    /// event did not fire. The read-back is the point: it proves the items reached the save, which is
    /// the thing the in-memory inventory cannot prove.
    /// </summary>
    public static class ContainerWrite
    {
        /// <summary>Vanilla's own ZDO key for a container's packed inventory.</summary>
        private const string ItemsKey = "items";

        /// <summary>
        /// Makes this machine the ZDO's owner, so the write that follows is the copy peers keep and
        /// the next save carries it. On a client this is a no-op while it owns the chest (the normal
        /// case for an open container) and refuses otherwise: ownership is the server's to give.
        /// </summary>
        public static bool EnsureOwned(Container container, string why)
        {
            if (container == null) return false;

            var view = container.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return false;
            if (view.IsOwner()) return true;

            if (ZNet.instance != null && !ZNet.instance.IsServer())
            {
                Log.Warn("flow: " + why + " needs " + Name(container) +
                         " but this peer does not own its ZDO; the write cannot persist");
                return false;
            }

            var previous = view.GetZDO() != null ? view.GetZDO().GetOwner() : 0L;
            view.ClaimOwnership();
            var owned = view.IsOwner();

            Log.Debug("Flow", (owned ? "claimed " : "could not claim ") + Name(container) + " for " + why +
                              " (owner was " + previous + ")");
            if (!owned) Log.Warn("flow: could not take " + Name(container) + " for " + why);
            return owned;
        }

        /// <summary>
        /// Writes the container's packed inventory into its ZDO and reports whether the ZDO now
        /// carries exactly those bytes. Safe to call when the owner's change event already saved it:
        /// the bytes match and nothing is written.
        /// </summary>
        public static bool Persist(Container container, string why)
        {
            if (container == null) return false;

            var view = container.GetComponent<ZNetView>();
            var inventory = container.GetInventory();
            if (view == null || !view.IsValid() || inventory == null) return false;

            var zdo = view.GetZDO();
            if (zdo == null) return false;

            var package = new ZPackage();
            inventory.Save(package);
            var packed = package.GetArray();

            var stored = zdo.GetByteArray(ItemsKey);
            var wrote = false;

            if (!Same(stored, packed))
            {
                zdo.Set(ItemsKey, packed);
                stored = zdo.GetByteArray(ItemsKey);
                wrote = true;
            }

            var ok = Same(stored, packed);
            if (ok)
            {
                Log.Debug("Flow", "persist " + why + " " + Name(container) + " stacks=" + inventory.NrOfItems() +
                                  " bytes=" + packed.Length + (wrote ? " written" : " already saved") +
                                  " zid=" + zdo.m_uid + " rev=" + zdo.DataRevision +
                                  " owner=" + zdo.GetOwner());
            }
            else
            {
                Log.Error("flow: " + Name(container) + " did not take the inventory write for " + why +
                          " (" + (stored != null ? stored.Length : 0) + " of " + packed.Length + " bytes)");
            }

            return ok;
        }

        private static bool Same(byte[] left, byte[] right)
        {
            if (left == null || right == null) return left == right;
            if (left.Length != right.Length) return false;

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }

        private static string Name(Container container)
        {
            var prefab = Utils.GetPrefabName(container.gameObject);
            var at = container.transform.position;
            return prefab + " at=(" + at.x.ToString("0.#") + "," + at.z.ToString("0.#") + ")";
        }
    }
}
