using Game.Attributes.Unity;
using Game.Items;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Presentation.World;

namespace Game.Network
{
    public sealed class NetworkWorldPickup : NetworkBehaviour, IWorldInteractable
    {
        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private Transform appearanceAnchor;
        [SerializeField, Range(.1f, 10f)] private float mergeRange = 1.25f;
        [SerializeField, Min(1)] private int maximumWorldStack = 10000;
        [SerializeField] private Text stackText;
        private const float CellSize = 4f;
        private static readonly Dictionary<Vector3Int, List<NetworkWorldPickup>> Cells = new Dictionary<Vector3Int, List<NetworkWorldPickup>>();
        private readonly List<NetworkWorldPickup> _neighbors = new List<NetworkWorldPickup>();
        private Vector3Int _cell;
        private float _nextMerge;
        private double _pendingPickupAfter;
        private readonly NetworkVariable<double> _automaticPickupAfter = new NetworkVariable<double>(0);
        private Font _font;
        private readonly NetworkVariable<int> _item = new NetworkVariable<int>(-1);
        private readonly NetworkVariable<int> _quantity = new NetworkVariable<int>(0);
        private int _pendingItem = -1, _pendingQuantity;
        private bool _claimed;
        private GameObject _appearance;
        public ItemDefinition Item => catalog != null ? catalog.Get(_item.Value) : null;
        public int Quantity => _quantity.Value;
        public ItemCatalog Catalog => catalog;
        public double AutomaticPickupRemaining => IsSpawned ? System.Math.Max(0, _automaticPickupAfter.Value - NetworkManager.ServerTime.Time) : 0;
        public bool AutomaticPickupAllowed => AutomaticPickupRemaining <= 0;
        public string InteractionPrompt => Item != null ? "[E] 拾取 " + Item.DisplayName + " × " + Quantity : "";
        public bool CanInteract(GameObject actor)
        {
            var inventory = actor != null ? actor.GetComponent<NetworkInventory>() : null;
            var attributes = actor != null ? actor.GetComponent<CharacterAttributes>() : null;
            return inventory != null && inventory.IsReady && attributes != null && attributes.IsReady && !attributes.Snapshot.IsDead &&
                actor.scene == gameObject.scene && IsInReach(actor.transform, inventory.PickupRange);
        }
        public void Interact(GameObject actor)
        { if (CanInteract(actor)) actor.GetComponent<NetworkInventory>().RequestPickup(this); }
        public void Configure(ItemDefinition item, int quantity)
        {
            if (IsSpawned || catalog == null || catalog.IndexOf(item) < 0 || quantity < 1 || quantity > 999)
                throw new System.InvalidOperationException("Configure an unspawned pickup with a catalog item and valid quantity.");
            _pendingItem = catalog.IndexOf(item);
            _pendingQuantity = quantity;
        }
        public override void OnNetworkSpawn()
        {
            _claimed = false;
            if (IsServer)
            {
                _item.Value = _pendingItem; _quantity.Value = _pendingQuantity;
                _automaticPickupAfter.Value = _pendingPickupAfter;
                Register(); _nextMerge = Time.unscaledTime + .1f;
            }
            _item.OnValueChanged += HandleItem;
            _quantity.OnValueChanged += HandleQuantity;
            if (stackText != null && (stackText.font == null || stackText.font.name == "LegacyRuntime"))
            { _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24); stackText.font = _font; }
            RefreshAppearance();
            RefreshLabel();
        }
        public override void OnNetworkDespawn()
        {
            _item.OnValueChanged -= HandleItem;
            _quantity.OnValueChanged -= HandleQuantity;
            Unregister();
            if (_appearance != null) Destroy(_appearance);
            if (_font != null) Destroy(_font);
        }
        private void HandleItem(int previous, int next) { RefreshAppearance(); RefreshLabel(); }
        public override void OnDestroy() { Unregister(); base.OnDestroy(); }
        private void HandleQuantity(int previous, int next) => RefreshLabel();
        private void RefreshLabel() { if (stackText != null) stackText.text = Item != null ? Item.DisplayName + " × " + Quantity : ""; }
        private void LateUpdate()
        {
            if (stackText != null && Camera.main != null) stackText.transform.parent.rotation = Camera.main.transform.rotation;
        }
        private static Vector3Int Cell(Vector3 position) => new Vector3Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.y / CellSize), Mathf.FloorToInt(position.z / CellSize));
        private void Register()
        {
            _cell = Cell(transform.position);
            if (!Cells.TryGetValue(_cell, out var list)) Cells.Add(_cell, list = new List<NetworkWorldPickup>());
            if (!list.Contains(this)) list.Add(this);
        }
        private void Unregister()
        {
            if (!Cells.TryGetValue(_cell, out var list)) return;
            list.Remove(this); if (list.Count == 0) Cells.Remove(_cell);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Cells.Clear();
        public static void FindNearby(Vector3 position, float range, NetworkManager manager, List<NetworkWorldPickup> results)
        {
            results.Clear(); var center = Cell(position); int reach = Mathf.CeilToInt(Mathf.Clamp(range, 0, 50) / CellSize);
            float square = range * range;
            for (int x = -reach; x <= reach; x++) for (int y = -reach; y <= reach; y++) for (int z = -reach; z <= reach; z++)
                if (Cells.TryGetValue(center + new Vector3Int(x, y, z), out var list))
                    foreach (var pickup in list)
                        if (pickup != null && pickup.IsSpawned && !pickup._claimed && pickup.NetworkManager == manager &&
                            (pickup.transform.position - position).sqrMagnitude <= square) results.Add(pickup);
        }
        private void Update()
        {
            if (!IsServer || !IsSpawned || _claimed || Time.unscaledTime < _nextMerge) return;
            _nextMerge = Time.unscaledTime + .5f;
            if (_cell != Cell(transform.position)) { Unregister(); Register(); }
            FindNearby(transform.position, Mathf.Clamp(mergeRange, .1f, 10f), NetworkManager, _neighbors);
            foreach (var other in _neighbors)
            {
                if (other == this || !other.IsSpawned || other.NetworkObjectId <= NetworkObjectId || other.gameObject.scene != gameObject.scene ||
                    other.catalog != catalog || other.Item != Item || other.AutomaticPickupAllowed != AutomaticPickupAllowed ||
                    !ClearPath(transform.position + Vector3.up * .15f, other.transform.position + Vector3.up * .15f)) continue;
                int amount = Mathf.Min(other.Quantity, Mathf.Max(0, maximumWorldStack - Quantity));
                if (amount == 0) continue;
                _quantity.Value += amount;
                _automaticPickupAfter.Value = System.Math.Max(_automaticPickupAfter.Value, other._automaticPickupAfter.Value);
                other._quantity.Value -= amount;
                if (other.Quantity == 0) { other._claimed = true; other.NetworkObject.Despawn(); }
            }
        }
        private static bool ClearPath(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < .0001f) return true;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<CharacterAttributes>() == null && hit.collider.GetComponentInParent<NetworkWorldPickup>() == null) return false;
            return true;
        }
        public static Vector3 GroundPosition(Vector3 position)
        {
            float nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * 2, Vector3.down, 8, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<CharacterAttributes>() == null && hit.collider.GetComponentInParent<NetworkWorldPickup>() == null && hit.distance < nearest)
                { nearest = hit.distance; position.y = hit.point.y + .12f; }
            return position;
        }
        public static bool SpawnOrMerge(NetworkManager manager, GameObject prefab, ItemDefinition item, int quantity, Vector3 position, Scene scene, float pickupDelay = 0)
        {
            if (manager == null || !manager.IsServer || manager.ShutdownInProgress || prefab == null || item == null || quantity < 1 || quantity > 999 ||
                !manager.NetworkConfig.Prefabs.Contains(prefab) || !prefab.TryGetComponent(out NetworkWorldPickup template) ||
                template.catalog == null || template.catalog.IndexOf(item) < 0) return false;
            var nearby = new List<NetworkWorldPickup>();
            FindNearby(position, Mathf.Clamp(template.mergeRange, .1f, 10f), manager, nearby);
            nearby.Sort((a, b) => a.NetworkObjectId.CompareTo(b.NetworkObjectId));
            foreach (var existing in nearby)
                if (existing.gameObject.scene == scene && existing.catalog == template.catalog && existing.Item == item &&
                    existing.AutomaticPickupAllowed == (pickupDelay <= 0) &&
                    (long)existing.Quantity + quantity <= existing.maximumWorldStack && ClearPath(position + Vector3.up * .15f, existing.transform.position + Vector3.up * .15f))
                {
                    existing._quantity.Value += quantity;
                    existing._automaticPickupAfter.Value = System.Math.Max(existing._automaticPickupAfter.Value, manager.ServerTime.Time + Mathf.Max(0, pickupDelay));
                    return true;
                }
            GameObject obj = null;
            try
            {
                obj = Instantiate(prefab, position, Quaternion.identity); SceneManager.MoveGameObjectToScene(obj, scene);
                var pickup = obj.GetComponent<NetworkWorldPickup>(); pickup.Configure(item, quantity);
                pickup._pendingPickupAfter = manager.ServerTime.Time + Mathf.Max(0, pickupDelay);
                obj.GetComponent<NetworkObject>().Spawn(true); return true;
            }
            catch (System.Exception exception)
            { Debug.LogException(exception); if (obj != null) Destroy(obj); return false; }
        }
        private void RefreshAppearance()
        {
            if (_appearance != null) Destroy(_appearance);
            if (Item == null || Item.WorldAppearance == null) return;
            _appearance = Instantiate(Item.WorldAppearance, appearanceAnchor != null ? appearanceAnchor : transform);
            foreach (var collider in _appearance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
        public bool IsInReach(Transform player, float range)
        {
            if (!IsSpawned || _claimed || Quantity <= 0 || Item == null || player == null ||
                Vector3.Distance(player.position, transform.position) > range) return false;
            Vector3 start = player.position + Vector3.up;
            Vector3 delta = transform.position + Vector3.up * .1f - start;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<CharacterAttributes>() == null && hit.collider.GetComponentInParent<NetworkWorldPickup>() != this) return false;
            return true;
        }
        public PickupResult TryCollect(NetworkInventory inventory, bool manual = false)
        {
            if (!IsSpawned || !IsServer || _claimed || (!manual && !AutomaticPickupAllowed) || inventory == null || !inventory.IsSpawned || !inventory.IsServer || Item == null || inventory.gameObject.scene != gameObject.scene)
                return PickupResult.Unavailable;
            var attributes = inventory.GetComponent<CharacterAttributes>();
            if (!attributes.IsReady || attributes.Snapshot.IsDead) return PickupResult.Unavailable;
            if (!IsInReach(inventory.transform, inventory.PickupRange)) return PickupResult.OutOfReach;
            int collected = inventory.TryGiveAvailable(Item, Quantity);
            if (collected == 0) return PickupResult.InventoryFull;
            _quantity.Value -= collected;
            if (Quantity == 0) { _claimed = true; NetworkObject.Despawn(); }
            return PickupResult.Success;
        }
    }
}
