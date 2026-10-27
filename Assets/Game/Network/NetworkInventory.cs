using System;
using System.Collections.Generic;
using Game.Attributes.Unity;
using Game.Items;
using Game.Combat.Unity;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    public struct NetworkInventorySlot : INetworkSerializable, IEquatable<NetworkInventorySlot>
    {
        public int ItemIndex, Quantity;
        public NetworkInventorySlot(ItemStack stack) { ItemIndex = stack.ItemIndex; Quantity = stack.Quantity; }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref ItemIndex); serializer.SerializeValue(ref Quantity); }
        public bool Equals(NetworkInventorySlot other) => ItemIndex == other.ItemIndex && Quantity == other.Quantity;
    }

    public enum PickupResult : byte { Success, InventoryFull, Unavailable, OutOfReach }
    public enum InventoryOperationResult : byte { Success, Invalid, Stale, Unavailable }
    [Serializable]
    public struct StartingItemGrant
    {
        public ItemDefinition item;
        [Min(1)] public int quantity;
    }
    public struct NetworkEquipmentState : INetworkSerializable, IEquatable<NetworkEquipmentState>
    {
        public int ItemIndex;
        public int SourceSlot;
        public uint Revision;
        public NetworkEquipmentState(int item, uint revision, int sourceSlot = -1) { ItemIndex = item; Revision = revision; SourceSlot = sourceSlot; }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref ItemIndex); serializer.SerializeValue(ref Revision); serializer.SerializeValue(ref SourceSlot); }
        public bool Equals(NetworkEquipmentState other) => ItemIndex == other.ItemIndex && Revision == other.Revision && SourceSlot == other.SourceSlot;
    }

    [RequireComponent(typeof(NetworkObject), typeof(CharacterAttributes))]
    public sealed class NetworkInventory : NetworkBehaviour, IInventoryReadModel, IEquipmentReadModel, IHotbarReadModel
    {
        [SerializeField] private ItemCatalog catalog;
        [SerializeField, Range(4, 64)] private int capacity = 24;
        [SerializeField, Min(.5f)] private float pickupRange = 2.2f;
        [SerializeField, Min(.1f)] private float autoPickupInterval = .2f;
        [SerializeField] private GameObject droppedItemPrefab;
        [SerializeField, Min(0f)] private float droppedItemPickupDelay = 1200f;
        [SerializeField] private ItemDefinition[] startingItems;
        [SerializeField] private StartingItemGrant[] startingSupplies = Array.Empty<StartingItemGrant>();
        private readonly NetworkVariable<NetworkEquipmentState> _equipment = new NetworkVariable<NetworkEquipmentState>(new NetworkEquipmentState(-1, 0));
        private NetworkList<NetworkInventorySlot> _slots;
        private InventoryStorage _storage;
        private float _nextPickup;
        private float _nextOperation, _nextFullMessage, _nextManualPickup;
        private CharacterAttributes _attributes;
        private readonly List<NetworkWorldPickup> _nearby = new List<NetworkWorldPickup>();
        public bool IsReady => IsSpawned && (IsOwner || IsServer) && _slots != null && _slots.Count > 0;
        public int SlotCount => IsReady ? _slots.Count - InventorySlotAddress.HotbarCount : 0;
        public int HotbarSlotCount => IsReady ? InventorySlotAddress.HotbarCount : 0;
        public int EquippedHotbarSlot => _equipment.Value.SourceSlot;
        public ItemCatalog Catalog => catalog;
        public float PickupRange => Mathf.Clamp(pickupRange, .5f, 50f);
        public ItemStack MainHand => new ItemStack(_equipment.Value.ItemIndex, _equipment.Value.ItemIndex >= 0 ? 1 : 0);
        public uint EquipmentRevision => _equipment.Value.Revision;
        public event Action EquipmentChanged;
        public event Action Changed;
        public event Action<PickupResult> PickupCompleted;
        public event Action<InventoryOperationResult> OperationCompleted;
        public event Action<InventoryOperationResult> UseCompleted;

        private void Awake()
        {
            _attributes = GetComponent<CharacterAttributes>();
            _slots = new NetworkList<NetworkInventorySlot>(null, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        }
        public override void OnNetworkSpawn()
        {
            _slots.OnListChanged += HandleChanged;
            _equipment.OnValueChanged += HandleEquipment;
            _nextPickup = 0f;
            _nextOperation = _nextFullMessage = _nextManualPickup = 0f;
            if (IsServer)
            {
                _storage = new InventoryStorage(Mathf.Clamp(capacity, 4, 64) + InventorySlotAddress.HotbarCount);
                _slots.Clear();
                for (int i = 0; i < _storage.SlotCount; i++) _slots.Add(new NetworkInventorySlot(_storage.GetSlot(i)));
                if (startingItems != null) foreach (var item in startingItems) if (item != null) TryGiveItem(item, 1);
                if (startingSupplies != null) foreach (var grant in startingSupplies)
                    if (grant.item != null && grant.quantity > 0) TryGiveItem(grant.item, grant.quantity);
            }
            ApplyEquipment();
            Changed?.Invoke();
        }
        public override void OnNetworkDespawn()
        {
            _slots.OnListChanged -= HandleChanged;
            _equipment.OnValueChanged -= HandleEquipment;
            _storage = null;
            Changed?.Invoke();
        }
        private void HandleChanged(NetworkListEvent<NetworkInventorySlot> change) => Changed?.Invoke();
        private void HandleEquipment(NetworkEquipmentState previous, NetworkEquipmentState next) => ApplyEquipment();
        private void ApplyEquipment()
        {
            var item = catalog != null ? catalog.Get(_equipment.Value.ItemIndex) : null;
            GetComponent<CharacterCombatController>()?.ApplyWeapon(item != null ? item.Weapon : null);
            GetComponent<NetworkCharacter>()?.RefreshEquipmentSimulation();
            EquipmentChanged?.Invoke();
        }
        public ItemStack GetSlot(int index)
        {
            int raw = StorageSlot(index);
            if (!IsReady || raw < 0) return new ItemStack(-1, 0);
            var slot = _slots[raw];
            return new ItemStack(slot.ItemIndex, slot.Quantity);
        }
        private int StorageSlot(int slot) => InventorySlotAddress.IsHotbar(slot)
            ? Mathf.Clamp(capacity, 4, 64) + slot - InventorySlotAddress.HotbarStart
            : slot >= 0 && slot < Mathf.Clamp(capacity, 4, 64) ? slot : -1;
        private void SetEquipment(ItemStack stack, int source = -1) => _equipment.Value =
            new NetworkEquipmentState(stack.IsEmpty ? -1 : stack.ItemIndex, unchecked(EquipmentRevision + 1), stack.IsEmpty ? -1 : source);
        private bool IsEquippedSource(int slot) => InventorySlotAddress.IsHotbar(slot) && slot == EquippedHotbarSlot;
        public bool TryGiveItem(ItemDefinition item, int quantity)
        {
            if (!IsSpawned || !IsServer || _storage == null || catalog == null || item == null) return false;
            int index = catalog.IndexOf(item);
            if (!_storage.TryAdd(index, quantity, item.StackLimit, SlotCount, InventorySlotAddress.HotbarCount)) return false;
            Publish();
            return true;
        }
        public bool TryGiveRewards(Game.Quests.QuestReward[] rewards)
        {
            if (!IsSpawned || !IsServer || _storage == null || catalog == null || rewards == null || rewards.Length == 0) return false;
            // Stage the complete reward on a copy so a full bag cannot receive a partial reward.
            var staged = new InventoryStorage(_storage.SlotCount);
            for (int i = 0; i < _storage.SlotCount; i++)
                staged.TryExchange(i, staged.GetSlot(i), _storage.GetSlot(i));
            foreach (var reward in rewards)
            {
                if (reward.item == null || reward.quantity < 1 ||
                    !staged.TryAdd(catalog.IndexOf(reward.item), reward.quantity, reward.item.StackLimit, SlotCount, InventorySlotAddress.HotbarCount)) return false;
            }
            _storage = staged;
            Publish();
            return true;
        }
        private void Publish()
        {
            for (int i = 0; i < _storage.SlotCount; i++)
            {
                var next = new NetworkInventorySlot(_storage.GetSlot(i));
                if (!_slots[i].Equals(next)) _slots[i] = next;
            }
        }
        public int TryGiveAvailable(ItemDefinition item, int quantity)
        {
            if (!IsSpawned || !IsServer || _storage == null || catalog == null || item == null || quantity < 1) return 0;
            int index = catalog.IndexOf(item);
            int amount = Mathf.Min(quantity, _storage.AvailableSpace(index, item.StackLimit));
            return amount > 0 && TryGiveItem(item, amount) ? amount : 0;
        }
        private bool IsAlive => IsSpawned && _attributes != null && _attributes.IsReady && !_attributes.Snapshot.IsDead;
        private void Update()
        {
            if (!IsServer || !IsAlive || Time.unscaledTime < _nextPickup) return;
            _nextPickup = Time.unscaledTime + Mathf.Max(.1f, autoPickupInterval);
            NetworkWorldPickup.FindNearby(transform.position, PickupRange, NetworkManager, _nearby);
            _nearby.Sort((a, b) => a.NetworkObjectId.CompareTo(b.NetworkObjectId));
            bool collected = false, full = false;
            foreach (var pickup in _nearby)
            {
                if (pickup == null || !pickup.IsInReach(transform, PickupRange)) continue;
                var result = pickup.TryCollect(this);
                collected |= result == PickupResult.Success;
                full |= result == PickupResult.InventoryFull;
            }
            if (collected) PickupResultRpc(PickupResult.Success);
            else if (full && Time.unscaledTime >= _nextFullMessage)
            { _nextFullMessage = Time.unscaledTime + 2f; PickupResultRpc(PickupResult.InventoryFull); }
        }
        public void RequestMove(int source, int target, int quantity, ItemStack expectedSource, ItemStack expectedTarget)
        {
            if (IsOwner && IsReady) MoveRpc(source, target, quantity, expectedSource.ItemIndex, expectedSource.Quantity, expectedTarget.ItemIndex, expectedTarget.Quantity);
        }
        public void RequestDrop(int slot, int quantity, ItemStack expected)
        {
            if (IsOwner && IsReady) DropRpc(slot, quantity, expected.ItemIndex, expected.Quantity);
        }
        public void RequestUseFood(int slot, ItemStack expected)
        {
            if (IsOwner && IsReady) UseFoodRpc(slot, expected.ItemIndex, expected.Quantity);
        }
        public bool TryUseFood(int slot, ItemStack expected)
        {
            int raw = StorageSlot(slot);
            if (!IsServer || !IsAlive || _storage == null || !_storage.Matches(raw, expected) || expected.IsEmpty) return false;
            var foods = GetComponent<NetworkFoods>();
            if (foods == null || !foods.TryEat(expected.ItemIndex)) return false;
            _storage.TryTake(raw, 1);
            Publish();
            return true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UseFoodRpc(int slot, int itemIndex, int quantity, RpcParams rpc = default)
        {
            if (!CanUse(rpc.Receive.SenderClientId)) { UseResultRpc(InventoryOperationResult.Unavailable); return; }
            var expected = new ItemStack(itemIndex, quantity);
            if (!_storage.Matches(StorageSlot(slot), expected)) { UseResultRpc(InventoryOperationResult.Stale); return; }
            UseResultRpc(TryUseFood(slot, expected) ? InventoryOperationResult.Success : InventoryOperationResult.Invalid);
        }
        private bool CanUse(ulong sender) => IsServer && IsAlive && _storage != null && sender == OwnerClientId;
        public void RequestUseHotbar(int index)
        {
            if (!IsOwner || !IsReady || index < 0 || index >= HotbarSlotCount) return;
            int slot = InventorySlotAddress.HotbarStart + index;
            var expected = GetSlot(slot);
            if (!expected.IsEmpty) UseHotbarRpc(index, expected.ItemIndex, expected.Quantity);
        }
        public bool TryUseHotbar(int index, ItemStack expected)
        {
            if (!IsServer || !IsAlive || _storage == null || index < 0 || index >= InventorySlotAddress.HotbarCount) return false;
            int slot = InventorySlotAddress.HotbarStart + index;
            if (!_storage.Matches(StorageSlot(slot), expected) || expected.IsEmpty) return false;
            var item = catalog.Get(expected.ItemIndex);
            if (item == null) return false;
            if (item.Category == ItemCategory.Food) return TryUseFood(slot, expected);
            if (item.Category != ItemCategory.Weapon || !CanEquip(expected) || !CanChangeEquipment) return false;
            if (IsEquippedSource(slot)) { SetEquipment(new ItemStack(-1, 0)); return true; }
            return MoveEquipment(slot, -2, 1);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UseHotbarRpc(int index, int itemIndex, int quantity, RpcParams rpc = default)
        {
            if (!CanUse(rpc.Receive.SenderClientId)) { UseResultRpc(InventoryOperationResult.Unavailable); return; }
            var expected = new ItemStack(itemIndex, quantity);
            if (!Matches(InventorySlotAddress.HotbarStart + index, expected)) { UseResultRpc(InventoryOperationResult.Stale); return; }
            bool success = TryUseHotbar(index, expected);
            if (success) Publish();
            UseResultRpc(success ? InventoryOperationResult.Success : InventoryOperationResult.Invalid);
        }
        private bool AcceptOperation(ulong sender)
        {
            if (!IsServer || !IsAlive || _storage == null || sender != OwnerClientId || Time.unscaledTime < _nextOperation) return false;
            _nextOperation = Time.unscaledTime + .05f;
            return true;
        }
        private bool Matches(int slot, ItemStack expected) => slot == -2
            ? MainHand.ItemIndex == expected.ItemIndex && MainHand.Quantity == expected.Quantity : _storage.Matches(StorageSlot(slot), expected);
        private bool CanEquip(ItemStack stack)
        {
            if (stack.IsEmpty) return true;
            var item = catalog.Get(stack.ItemIndex);
            if (stack.Quantity != 1 || item == null || item.Category != ItemCategory.Weapon || item.StackLimit != 1 || item.Weapon == null) return false;
            try { item.Weapon.CreateSettings(); return true; }
            catch (Exception) { return false; }
        }
        private bool CanChangeEquipment => GetComponent<CharacterCombatController>() != null &&
            !GetComponent<CharacterCombatController>().State.Attacking && !GetComponent<CharacterCombatController>().State.Dodging;
        private bool MoveEquipment(int source, int target, int quantity)
        {
            if (!CanChangeEquipment || quantity != 1 || source == target) return false;
            int slot = source == -2 ? target : source;
            int raw = StorageSlot(slot);
            if (raw < 0) return false;
            var from = _storage.GetSlot(raw); var worn = MainHand;
            if ((source != -2 && from.IsEmpty) || (source == -2 && worn.IsEmpty) || !CanEquip(from)) return false;
            bool linked = InventorySlotAddress.IsHotbar(EquippedHotbarSlot);
            if (source == -2 && linked)
            {
                if (target != EquippedHotbarSlot && !_storage.TryMove(StorageSlot(EquippedHotbarSlot), raw, 1, catalog)) return false;
                SetEquipment(new ItemStack(-1, 0));
                return true;
            }
            if (source != -2 && InventorySlotAddress.IsHotbar(source))
            {
                if (!linked && !worn.IsEmpty)
                {
                    var oldItem = catalog.Get(worn.ItemIndex);
                    if (oldItem == null || !_storage.TryAdd(worn.ItemIndex, 1, oldItem.StackLimit, SlotCount, InventorySlotAddress.HotbarCount)) return false;
                }
                SetEquipment(from, source);
                return true;
            }
            if (!_storage.TryExchange(raw, from, linked ? new ItemStack(-1, 0) : worn)) return false;
            SetEquipment(from);
            return true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void MoveRpc(int source, int target, int quantity, int fromItem, int fromQuantity, int toItem, int toQuantity, RpcParams rpc = default)
        {
            if (!AcceptOperation(rpc.Receive.SenderClientId)) { OperationResultRpc(InventoryOperationResult.Unavailable); return; }
            if (!Matches(source, new ItemStack(fromItem, fromQuantity)) || !Matches(target, new ItemStack(toItem, toQuantity)))
            { OperationResultRpc(InventoryOperationResult.Stale); return; }
            if (source == -2 || target == -2)
            { if (!MoveEquipment(source, target, quantity)) { OperationResultRpc(InventoryOperationResult.Invalid); return; } }
            else
            {
                bool changesEquipment = IsEquippedSource(source) || IsEquippedSource(target);
                if (changesEquipment && !CanChangeEquipment) { OperationResultRpc(InventoryOperationResult.Invalid); return; }
                if (!_storage.TryMove(StorageSlot(source), StorageSlot(target), quantity, catalog)) { OperationResultRpc(InventoryOperationResult.Invalid); return; }
                if (changesEquipment) SetEquipment(new ItemStack(-1, 0));
            }
            Publish(); OperationResultRpc(InventoryOperationResult.Success);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void DropRpc(int slot, int quantity, int itemIndex, int expectedQuantity, RpcParams rpc = default)
        {
            if (!AcceptOperation(rpc.Receive.SenderClientId)) { OperationResultRpc(InventoryOperationResult.Unavailable); return; }
            if (!Matches(slot, new ItemStack(itemIndex, expectedQuantity))) { OperationResultRpc(InventoryOperationResult.Stale); return; }
            var item = catalog.Get(itemIndex);
            if (item == null || quantity < 1 || quantity > expectedQuantity || ((slot == -2 || IsEquippedSource(slot)) && (!CanChangeEquipment || quantity != 1))) { OperationResultRpc(InventoryOperationResult.Invalid); return; }
            Vector3 position = transform.position + transform.forward * .9f + Vector3.up * .15f;
            if (Physics.Linecast(transform.position + Vector3.up, position, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<CharacterAttributes>() == null) position = transform.position + Vector3.up * .15f;
            position = NetworkWorldPickup.GroundPosition(position);
            if (!NetworkWorldPickup.SpawnOrMerge(NetworkManager, droppedItemPrefab, item, quantity, position, gameObject.scene, droppedItemPickupDelay))
            { OperationResultRpc(InventoryOperationResult.Unavailable); return; }
            if (slot == -2)
            {
                if (InventorySlotAddress.IsHotbar(EquippedHotbarSlot)) _storage.TryTake(StorageSlot(EquippedHotbarSlot), quantity);
                SetEquipment(new ItemStack(-1, 0));
            }
            else
            {
                _storage.TryTake(StorageSlot(slot), quantity);
                if (IsEquippedSource(slot)) SetEquipment(new ItemStack(-1, 0));
            }
            Publish(); OperationResultRpc(InventoryOperationResult.Success);
        }
        [Rpc(SendTo.Owner)]
        private void OperationResultRpc(InventoryOperationResult result) => OperationCompleted?.Invoke(result);
        [Rpc(SendTo.Owner)]
        private void UseResultRpc(InventoryOperationResult result) => UseCompleted?.Invoke(result);
        public void RequestPickup(NetworkWorldPickup pickup)
        {
            if (IsOwner && IsSpawned && pickup != null && pickup.IsSpawned) RequestPickupRpc(pickup.NetworkObjectId);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestPickupRpc(ulong pickupId, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || rpcParams.Receive.SenderClientId != OwnerClientId || Time.unscaledTime < _nextManualPickup) return;
            _nextManualPickup = Time.unscaledTime + .15f;
            var attributes = GetComponent<CharacterAttributes>();
            PickupResult result = PickupResult.Unavailable;
            if (attributes.IsReady && !attributes.Snapshot.IsDead &&
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickupId, out var obj) && obj.TryGetComponent(out NetworkWorldPickup pickup))
                result = pickup.TryCollect(this, true);
            PickupResultRpc(result);
        }
        [Rpc(SendTo.Owner)]
        private void PickupResultRpc(PickupResult result) => PickupCompleted?.Invoke(result);
    }
}
