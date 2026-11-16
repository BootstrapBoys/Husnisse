using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// The ZNetView a container's inventory actually belongs to.
    ///
    /// A normal chest is one object: the Container and the ZNetView share it, and `GetComponent` is
    /// the answer. A ship's hold is a child Container whose `m_rootObjectOverride` points at the
    /// ship root's ZNetView — vanilla's own `Container.Awake` reads that field for exactly this
    /// reason, and the hold's packed items live in the ship's ZDO. Asking the child for a component
    /// finds nothing, so a server-side write to a ship hold would be refused as "no ZDO" even
    /// though the boat is loaded and the hold is full. Every container-to-ZDO lookup goes here.
    /// </summary>
    public static class ContainerNet
    {
        public static ZNetView View(Container container)
        {
            if (container == null) return null;
            if (container.m_rootObjectOverride != null) return container.m_rootObjectOverride;
            return container.GetComponent<ZNetView>();
        }

        /// <summary>The ZDO, or null when the container has no live view.</summary>
        public static ZDO Zdo(Container container)
        {
            var view = View(container);
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }
    }
}
