using System;
using System.Collections.Generic;
using Game.Attributes;
using Game.Attributes.Unity;
using Game.Combat.Unity;
using Game.Items;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    public struct NetworkFoodState : INetworkSerializable, IEquatable<NetworkFoodState>
    {
        public int ItemIndex;
        public double EndsAt;
        public NetworkFoodState(int item, double endsAt) { ItemIndex = item; EndsAt = endsAt; }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref ItemIndex); serializer.SerializeValue(ref EndsAt); }
        public bool Equals(NetworkFoodState other) => ItemIndex == other.ItemIndex && EndsAt.Equals(other.EndsAt);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkInventory), typeof(CharacterAttributes))]
    public sealed class NetworkFoods : NetworkBehaviour, IActiveFoodReadModel
    {
        [SerializeField, Range(1, 6)] private int foodSlotCount = 3;
        [SerializeField] private bool replaceSoonestWhenFull = true;
        [SerializeField] private bool clearOnDeath = true;
        private NetworkList<NetworkFoodState> _foods;
        private CharacterAttributes _attributes;
        private NetworkInventory _inventory;
        private double _lastTime;
        private string _source;
        public bool IsReady => IsSpawned && (IsOwner || IsServer) && _foods != null && _foods.Count > 0;
        public int FoodSlotCount => IsReady ? _foods.Count : 0;
        public ItemCatalog Catalog => _inventory != null ? _inventory.Catalog : null;
        public double CurrentTime => IsSpawned ? NetworkManager.ServerTime.Time : 0;
        public event Action FoodsChanged;

        private void Awake()
        {
            _attributes = GetComponent<CharacterAttributes>();
            _inventory = GetComponent<NetworkInventory>();
            _foods = new NetworkList<NetworkFoodState>(null, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        }
        public override void OnNetworkSpawn()
        {
            _source = "foods:" + NetworkObjectId;
            _foods.OnListChanged += HandleChanged;
            if (IsServer)
            {
                _foods.Clear();
                for (int i = 0; i < Mathf.Clamp(foodSlotCount, 1, 6); i++) _foods.Add(new NetworkFoodState(-1, 0));
                _attributes.Died += HandleDeath;
                _lastTime = CurrentTime;
            }
            FoodsChanged?.Invoke();
        }
        public override void OnNetworkDespawn()
        {
            _foods.OnListChanged -= HandleChanged;
            _attributes.Died -= HandleDeath;
            if (IsServer && _attributes.TryGetAuthority(out var service)) service.RemoveModifiersFromSource(_source);
            FoodsChanged?.Invoke();
        }
        private void HandleChanged(NetworkListEvent<NetworkFoodState> change) => FoodsChanged?.Invoke();
        public ActiveFood GetFood(int slot) => IsReady && slot >= 0 && slot < _foods.Count
            ? new ActiveFood(_foods[slot].ItemIndex, _foods[slot].EndsAt) : new ActiveFood(-1, 0);
        private void Update()
        {
            if (IsSpawned && IsServer && !NetworkManager.ShutdownInProgress) TickAuthority(CurrentTime);
        }
        private NetworkFoodState[] CopyFoods()
        {
            var result = new NetworkFoodState[_foods.Count];
            for (int i = 0; i < result.Length; i++) result[i] = _foods[i];
            return result;
        }
        private List<AttributeModifier> BuildModifiers(NetworkFoodState[] foods)
        {
            var result = new List<AttributeModifier>();
            foreach (var entry in foods)
            {
                var item = Catalog.Get(entry.ItemIndex);
                if (item != null && item.Food != null) result.AddRange(item.Food.CreateModifiers(_source));
            }
            return result;
        }
        private void Commit(NetworkFoodState[] foods, IAttributeService service)
        {
            var modifiers = BuildModifiers(foods);
            service.ReplaceModifiersFromSource(_source, modifiers);
            for (int i = 0; i < foods.Length; i++) if (!_foods[i].Equals(foods[i])) _foods[i] = foods[i];
            GetComponent<NetworkAttributes>()?.PublishAuthorityState();
        }
        private void TickAuthority(double now)
        {
            if (!_attributes.TryGetAuthority(out var service) || now < _lastTime) return;
            NetworkFoodState[] changed = null;
            double heal = 0, stamina = 0;
            for (int i = 0; i < _foods.Count; i++)
            {
                var entry = _foods[i];
                if (entry.ItemIndex < 0) continue;
                var item = Catalog.Get(entry.ItemIndex);
                if (item != null && item.Food != null)
                {
                    double elapsed = Math.Max(0, Math.Min(now, entry.EndsAt) - _lastTime);
                    heal += item.Food.HealthRecoveryPerSecond * elapsed;
                    stamina += item.Food.StaminaRecoveryPerSecond * elapsed;
                }
                if (entry.EndsAt <= now)
                {
                    if (changed == null) changed = CopyFoods();
                    changed[i] = new NetworkFoodState(-1, 0);
                }
            }
            _lastTime = now;
            if (heal > 0 && !service.Snapshot.IsDead) service.Heal((float)Math.Min(float.MaxValue, heal));
            if (stamina > 0 && !service.Snapshot.IsDead) service.RestoreStamina((float)Math.Min(float.MaxValue, stamina));
            if (changed != null) Commit(changed, service);
        }
        public bool TryEat(int itemIndex)
        {
            if (!IsSpawned || !IsServer || !_attributes.TryGetAuthority(out var service) || service.Snapshot.IsDead || Catalog == null) return false;
            double now = CurrentTime;
            var combat = GetComponent<CharacterCombatController>();
            if (combat != null && (combat.State.Attacking || combat.State.Dodging || combat.State.HitStopRemaining > 0f)) return false;
            var item = Catalog.Get(itemIndex);
            if (item == null || item.Category != ItemCategory.Food || item.Food == null) return false;
            TickAuthority(now);
            var next = CopyFoods();
            int target = -1;
            for (int i = 0; i < next.Length; i++) if (next[i].ItemIndex == itemIndex) { target = i; break; }
            if (target < 0) for (int i = 0; i < next.Length; i++) if (next[i].ItemIndex < 0) { target = i; break; }
            if (target < 0)
            {
                if (!replaceSoonestWhenFull) return false;
                target = 0;
                for (int i = 1; i < next.Length; i++) if (next[i].EndsAt < next[target].EndsAt) target = i;
            }
            next[target] = new NetworkFoodState(itemIndex, now + item.Food.Duration);
            try { BuildModifiers(next); }
            catch (Exception error) { Debug.LogError("食物加成配置无效：" + error.Message, item); return false; }
            Commit(next, service);
            return true;
        }
        private void HandleDeath()
        {
            if (!IsServer || !clearOnDeath || !_attributes.TryGetAuthority(out var service)) return;
            var empty = CopyFoods();
            for (int i = 0; i < empty.Length; i++) empty[i] = new NetworkFoodState(-1, 0);
            Commit(empty, service);
            _lastTime = CurrentTime;
        }
    }
}
