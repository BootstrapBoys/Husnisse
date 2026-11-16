using System.Reflection;
using HarmonyLib;
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
    /// Every server-side inventory move on the flow path goes through here:
    ///
    /// 1. <see cref="Acquire"/> asks the owning player for the ZDO (<see cref="ChestHandover"/>) and
    ///    reports <see cref="ChestAccess.Owned"/> only when this machine really owns it;
    /// 2. <see cref="Sync"/> re-reads the packed inventory out of the ZDO, so the move is applied to
    ///    the copy the world has rather than to a buffer that may be up to a tick stale;
    /// 3. <see cref="Persist"/> writes it back, reads it back again, and only then reports success.
    ///
    /// The read-back is the point: it proves the items reached the save, which is the thing the
    /// in-memory inventory cannot prove.
    /// </summary>
    public static class ContainerWrite
    {
        /// <summary>Vanilla's own ZDO key for a container's packed inventory.</summary>
        private const string ItemsKey = "items";

        private static readonly FieldInfo LoadingField = AccessTools.Field(typeof(Container), "m_loading");
        private static readonly FieldInfo LastRevisionField = AccessTools.Field(typeof(Container), "m_lastRevision");

        /// <summary>
        /// Asks for the container's ZDO if a player holds it. Callers must not move an item on
        /// anything but <see cref="ChestAccess.Owned"/>; a deferral means "ask again later".
        /// </summary>
        public static ChestAccess Acquire(Container container, string why)
        {
            return ChestHandover.Acquire(container, why);
        }

        /// <summary>Shorthand for callers whose only question is "may I write this now?".</summary>
        public static bool Own(Container container, string why)
        {
            return ChestHandover.Acquire(container, why) == ChestAccess.Owned;
        }

        /// <summary>
        /// Re-reads the container's inventory out of its ZDO. The ZDO is the copy every peer agrees
        /// on; the live <c>Inventory</c> is only current for the machine that wrote it last. A player
        /// can have moved a stack between the owner's save and this machine's claim, and vanilla's own
        /// one-second check is not a guarantee inside a single trip. Reports false when the container
        /// cannot be brought up to date — the caller must then leave the contents alone.
        /// </summary>
        public static bool Sync(Container container, string why)
        {
            if (container == null) return false;

            var view = container.GetComponent<ZNetView>();
            var inventory = container.GetInventory();
            if (view == null || !view.IsValid() || inventory == null) return false;

            var zdo = view.GetZDO();
            if (zdo == null) return false;

            var stored = zdo.GetByteArray(ItemsKey);
            var packed = Pack(inventory);

            if (stored == null || Same(stored, packed))
            {
                MarkCurrent(container, zdo);
                return true;
            }

            var loading = LoadingField;
            if (loading == null)
            {
                Log.Error("flow: cannot sync " + Name(container) + " (" + why + "): m_loading field moved");
                return false;
            }

            var was = (bool)loading.GetValue(container);
            try
            {
                loading.SetValue(container, true);
                inventory.Load(new ZPackage(stored));
            }
            catch (System.Exception ex)
            {
                Log.Error("flow: " + Name(container) + " has an unreadable inventory for " + why + ": " + ex.Message);
                return false;
            }
            finally
            {
                loading.SetValue(container, was);
            }

            MarkCurrent(container, zdo);

            var ok = Same(Pack(inventory), stored);
            Log.Debug("Flow", "sync " + why + " " + Name(container) + " reloaded from the ZDO stacks=" +
                              inventory.NrOfItems() + " (" + (ok ? "ok" : "mismatch") + ") drev=" + zdo.DataRevision);
            return ok;
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

            var packed = Pack(inventory);
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
                MarkCurrent(container, zdo);
                Log.Debug("Flow", "persist " + why + " " + Name(container) + " stacks=" + inventory.NrOfItems() +
                                  " bytes=" + packed.Length + (wrote ? " written" : " already saved") +
                                  " zid=" + zdo.m_uid + " drev=" + zdo.DataRevision + " orev=" + zdo.OwnerRevision +
                                  " owner=" + zdo.GetOwner());
            }
            else
            {
                Log.Error("flow: " + Name(container) + " did not take the inventory write for " + why +
                          " (" + (stored != null ? stored.Length : 0) + " of " + packed.Length + " bytes)");
            }

            return ok;
        }

        /// <summary>Reads a container's contents as the ZDO has them, without touching the live copy.</summary>
        public static Inventory Read(Container container)
        {
            var view = container != null ? container.GetComponent<ZNetView>() : null;
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null) return null;

            var stored = zdo.GetByteArray(ItemsKey);
            if (stored == null) return null;

            var inventory = new Inventory("husnisse-zdo", null, 8, 4);
            try
            {
                inventory.Load(new ZPackage(stored));
            }
            catch (System.Exception ex)
            {
                Log.Debug("Flow", "unreadable inventory on " + Name(container) + ": " + ex.Message);
                return null;
            }
            return inventory;
        }

        /// <summary>Keeps vanilla's own reload check from firing again on the copy we just aligned.</summary>
        private static void MarkCurrent(Container container, ZDO zdo)
        {
            if (LastRevisionField == null) return;
            LastRevisionField.SetValue(container, zdo.DataRevision);
        }

        private static byte[] Pack(Inventory inventory)
        {
            var package = new ZPackage();
            inventory.Save(package);
            return package.GetArray();
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
            if (container == null) return "container";
            var prefab = Utils.GetPrefabName(container.gameObject);
            var at = container.transform.position;
            return prefab + " at=(" + at.x.ToString("0.#") + "," + at.z.ToString("0.#") + ")";
        }
    }
}
