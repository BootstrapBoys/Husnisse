using Husnisse.Util;

namespace Husnisse.Memory
{
    /// <summary>
    /// The nisse's memory of where a kind of thing lives. It is stored in the nisse's own ZDO, one
    /// key per item name, because the chest does not need to remember anything.
    /// </summary>
    public class ItemMemory
    {
        private readonly ZDO _zdo;

        public ItemMemory(ZDO zdo)
        {
            _zdo = zdo;
        }

        public bool TryRecall(string itemName, out ZDOID container)
        {
            container = ZDOID.None;
            if (_zdo == null || string.IsNullOrEmpty(itemName)) return false;
            container = _zdo.GetZDOID(ZdoKeys.MemoryPrefix + itemName);
            return container != ZDOID.None;
        }

        public void Remember(string itemName, ZDOID container)
        {
            if (_zdo == null || string.IsNullOrEmpty(itemName) || container == ZDOID.None) return;
            _zdo.Set(ZdoKeys.MemoryPrefix + itemName, container);
        }

        public void Forget(string itemName)
        {
            if (_zdo == null || string.IsNullOrEmpty(itemName)) return;
            _zdo.Set(ZdoKeys.MemoryPrefix + itemName, ZDOID.None);
        }
    }
}
