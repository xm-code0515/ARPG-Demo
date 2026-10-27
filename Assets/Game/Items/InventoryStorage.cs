using System;

namespace Game.Items
{
    public readonly struct ItemStack
    {
        public readonly int ItemIndex;
        public readonly int Quantity;
        public bool IsEmpty => Quantity <= 0 || ItemIndex < 0;
        public ItemStack(int itemIndex, int quantity) { ItemIndex = itemIndex; Quantity = quantity; }
    }

    public interface IInventoryReadModel
    {
        bool IsReady { get; }
        int SlotCount { get; }
        ItemCatalog Catalog { get; }
        ItemStack GetSlot(int index);
        event Action Changed;
    }
    public interface IEquipmentReadModel
    {
        ItemStack MainHand { get; }
        event Action EquipmentChanged;
    }

    public interface IHotbarReadModel
    {
        int HotbarSlotCount { get; }
        int EquippedHotbarSlot { get; }
    }

    public static class InventorySlotAddress
    {
        public const int HotbarStart = 100;
        public const int HotbarCount = 8;
        public static bool IsHotbar(int slot) => slot >= HotbarStart && slot < HotbarStart + HotbarCount;
    }

    public sealed class InventoryStorage
    {
        private readonly ItemStack[] _slots;
        public int SlotCount => _slots.Length;
        public InventoryStorage(int capacity)
        {
            if (capacity < 1 || capacity > 72) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new ItemStack[capacity];
            for (int i = 0; i < capacity; i++) _slots[i] = new ItemStack(-1, 0);
        }
        public ItemStack GetSlot(int index) => _slots[index];
        public int AvailableSpace(int itemIndex, int stackLimit)
        {
            if (itemIndex < 0 || stackLimit < 1) return 0;
            long space = 0;
            foreach (var slot in _slots)
                if (slot.IsEmpty) space += stackLimit;
                else if (slot.ItemIndex == itemIndex) space += Math.Max(0, stackLimit - slot.Quantity);
            return (int)Math.Min(int.MaxValue, space);
        }
        public bool Matches(int index, ItemStack expected) => index >= 0 && index < SlotCount &&
            _slots[index].ItemIndex == expected.ItemIndex && _slots[index].Quantity == expected.Quantity;
        public bool TryExchange(int index, ItemStack expected, ItemStack replacement)
        {
            if (!Matches(index, expected) || replacement.Quantity < 0 || (!replacement.IsEmpty && replacement.ItemIndex < 0)) return false;
            _slots[index] = replacement.IsEmpty ? new ItemStack(-1, 0) : replacement;
            return true;
        }
        public bool TryMove(int source, int target, int quantity, ItemCatalog catalog)
        {
            if (catalog == null || source < 0 || target < 0 || source >= SlotCount || target >= SlotCount || source == target) return false;
            var from = _slots[source]; var to = _slots[target];
            var item = catalog.Get(from.ItemIndex);
            if (from.IsEmpty || item == null || quantity < 1 || quantity > from.Quantity) return false;
            if (!to.IsEmpty && to.ItemIndex != from.ItemIndex)
            {
                if (quantity != from.Quantity) return false;
                _slots[source] = to; _slots[target] = from;
                return true;
            }
            int amount = Math.Min(quantity, item.StackLimit - (to.IsEmpty ? 0 : to.Quantity));
            if (amount <= 0) return false;
            _slots[target] = new ItemStack(from.ItemIndex, (to.IsEmpty ? 0 : to.Quantity) + amount);
            _slots[source] = from.Quantity == amount ? new ItemStack(-1, 0) : new ItemStack(from.ItemIndex, from.Quantity - amount);
            return true;
        }
        public bool TryTake(int index, int quantity)
        {
            if (index < 0 || index >= SlotCount || quantity < 1 || _slots[index].IsEmpty || quantity > _slots[index].Quantity) return false;
            var slot = _slots[index];
            _slots[index] = quantity == slot.Quantity ? new ItemStack(-1, 0) : new ItemStack(slot.ItemIndex, slot.Quantity - quantity);
            return true;
        }
        public bool TryAdd(int itemIndex, int quantity, int stackLimit, int priorityStart = 0, int priorityCount = 0)
        {
            if (itemIndex < 0 || quantity <= 0 || stackLimit < 1) return false;
            if (AvailableSpace(itemIndex, stackLimit) < quantity) return false;
            int left = quantity;
            priorityStart = Math.Max(0, Math.Min(_slots.Length, priorityStart));
            priorityCount = Math.Max(0, Math.Min(_slots.Length - priorityStart, priorityCount));
            for (int region = 0; region < 2 && left > 0; region++)
              for (int pass = 0; pass < 2 && left > 0; pass++)
                for (int i = 0; i < _slots.Length && left > 0; i++)
                {
                    bool priority = i >= priorityStart && i < priorityStart + priorityCount;
                    if (priority != (region == 0)) continue;
                    var slot = _slots[i];
                    bool matches = pass == 0 ? !slot.IsEmpty && slot.ItemIndex == itemIndex : slot.IsEmpty;
                    if (!matches) continue;
                    int add = Math.Min(left, Math.Max(0, stackLimit - slot.Quantity));
                    _slots[i] = new ItemStack(itemIndex, slot.Quantity + add);
                    left -= add;
                }
            return true;
        }
    }
}
