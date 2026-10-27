using Game.Attributes.Unity;
using Game.Input;
using Game.Presentation.UI;
using Game.Presentation.Cameras;
using Game.Items;
using Game.Presentation.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Network
{
    public sealed class NetworkPlayerItemsUIBinder : MonoBehaviour
    {
        [SerializeField] private InventoryHud inventoryHud;
        [SerializeField] private HotbarHud hotbarHud;
        [SerializeField] private PickupPromptView pickupPrompt;
        [SerializeField] private NpcDialogueView dialogueView;
        [SerializeField] private QuestWindowView questView;
        [SerializeField, Min(1)] private float aimRayDistance = 12f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        private NetworkInventory _inventory;
        private PlayerInputReader _input;
        private CharacterAttributes _attributes;
        private ThirdPersonCameraController _camera;
        private Component _target;
        private NetworkPlayerQuests _quests;
        private bool _menuOpen;
        public bool IsOtherUiOpen => DialogueOrQuestOpen || (inventoryHud != null && inventoryHud.IsOpen);
        public void SetMenuOpen(bool value) { _menuOpen = value; SetGameplayBlocked(false); }
        private readonly RaycastHit[] _hits = new RaycastHit[64];
        private void OnEnable()
        {
            if (inventoryHud == null) return;
            inventoryHud.OpenChanged += SetGameplayBlocked;
            inventoryHud.MoveRequested += Move;
            inventoryHud.DropRequested += Drop;
            inventoryHud.FoodUseRequested += UseFood;
            if (dialogueView != null) dialogueView.OpenChanged += HandleDialogueOpened;
            if (questView != null) questView.OpenChanged += HandleDialogueOpened;
        }
        private void Update()
        {
            var player = NetworkCharacter.LocalPlayer;
            var next = player != null && player.IsSpawned && player.IsOwner ? player.GetComponent<NetworkInventory>() : null;
            if (!ReferenceEquals(_inventory, next)) Bind(next);
            bool alive = _inventory != null && _inventory.IsReady && _attributes != null && _attributes.IsReady && !_attributes.Snapshot.IsDead;
            if (!alive)
            {
                if (inventoryHud != null) inventoryHud.SetOpen(false);
                if (dialogueView != null) dialogueView.Close();
                if (questView != null) { questView.Close(); questView.SetTracker(""); }
                _target = null;
                if (pickupPrompt != null) pickupPrompt.Hide();
                return;
            }
            UpdateTarget();
            RefreshQuests();
            if (pickupPrompt != null) pickupPrompt.ShowInteraction(_target != null ? ((IWorldInteractable)_target).InteractionPrompt : "",
                !_menuOpen && (inventoryHud == null || !inventoryHud.IsOpen) && !DialogueOrQuestOpen);
        }
        private void Bind(NetworkInventory next)
        {
            SetGameplayBlocked(false);
            if (_input != null) { _input.InventoryPressed -= ToggleInventory; _input.InteractPressed -= Interact; _input.HotbarPressed -= UseHotbar; }
            if (_inventory != null) { _inventory.PickupCompleted -= HandlePickup; _inventory.OperationCompleted -= HandleOperation; _inventory.UseCompleted -= HandleUse; }
            if (_quests != null) _quests.OperationCompleted -= HandleQuestOperation;
            _inventory = next;
            _input = next != null ? next.GetComponent<PlayerInputReader>() : null;
            _attributes = next != null ? next.GetComponent<CharacterAttributes>() : null;
            _quests = next != null ? next.GetComponent<NetworkPlayerQuests>() : null;
            if (_quests != null) _quests.OperationCompleted += HandleQuestOperation;
            if (_input != null) { _input.InventoryPressed += ToggleInventory; _input.InteractPressed += Interact; _input.HotbarPressed += UseHotbar; }
            if (_inventory != null) { _inventory.PickupCompleted += HandlePickup; _inventory.OperationCompleted += HandleOperation; _inventory.UseCompleted += HandleUse; }
            if (hotbarHud == null) hotbarHud = FindFirstObjectByType<HotbarHud>();
            if (inventoryHud != null) inventoryHud.AttachHotbar(hotbarHud);
            if (inventoryHud != null) inventoryHud.Bind(_inventory);
            SetGameplayBlocked(false);
        }
        private bool CanInteract => !_menuOpen && _inventory != null && _attributes != null && _attributes.IsReady && !_attributes.Snapshot.IsDead;
        private bool DialogueOrQuestOpen => (dialogueView != null && dialogueView.IsOpen) || (questView != null && questView.IsOpen);
        private void SetGameplayBlocked(bool value)
        {
            value |= _menuOpen || DialogueOrQuestOpen || (inventoryHud != null && inventoryHud.IsOpen);
            if (_input != null) _input.SetGameplayInputBlocked(value);
            if (_camera == null && Camera.main != null) _camera = Camera.main.GetComponent<ThirdPersonCameraController>();
            if (_camera != null) _camera.SetGameplayInputBlocked(value);
        }
        private void Move(int source, int target, int quantity, ItemStack from, ItemStack to) { if (CanInteract) _inventory.RequestMove(source, target, quantity, from, to); }
        private void Drop(int slot, int quantity, ItemStack expected) { if (CanInteract) _inventory.RequestDrop(slot, quantity, expected); }
        private void UseFood(int slot, ItemStack expected) { if (CanInteract) _inventory.RequestUseFood(slot, expected); }
        private void UseHotbar(int index)
        {
            if (CanInteract && (inventoryHud == null || !inventoryHud.IsBusy) && !DialogueOrQuestOpen) _inventory.RequestUseHotbar(index);
        }
        private void HandleUse(InventoryOperationResult result)
        {
            if (result != InventoryOperationResult.Success && inventoryHud != null)
                inventoryHud.ShowMessage(result == InventoryOperationResult.Stale ? "物品已更新，请重新选择" : "当前物品无法使用或正在执行动作");
        }
        private void ToggleInventory()
        {
            if (_menuOpen) return;
            if (questView != null && questView.IsOpen) { questView.Close(); return; }
            if (dialogueView != null && dialogueView.IsOpen) { dialogueView.Close(); return; }
            if (CanInteract && inventoryHud != null) inventoryHud.SetOpen(!inventoryHud.IsOpen);
        }
        private void HandleDialogueOpened(bool open)
        {
            if (open && inventoryHud != null) inventoryHud.SetOpen(false);
            SetGameplayBlocked(open || (inventoryHud != null && inventoryHud.IsOpen));
        }
        private void UpdateTarget()
        {
            _target = null;
            var camera = Camera.main;
            if (camera == null || !CanInteract || DialogueOrQuestOpen || (inventoryHud != null && inventoryHud.IsBusy)) return;
            Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            if (inventoryHud != null && inventoryHud.IsOpen && Mouse.current != null)
            {
                var point = Mouse.current.position.ReadValue();
                if (inventoryHud.InsidePanel(point)) return;
                ray = camera.ScreenPointToRay(point);
            }
            int count = Physics.RaycastNonAlloc(ray, _hits, aimRayDistance, interactionLayers, QueryTriggerInteraction.Collide);
            if (count == _hits.Length) return;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var collider = _hits[i].collider;
                if (collider.transform.IsChildOf(_inventory.transform)) continue;
                var candidate = collider.GetComponentInParent<IWorldInteractable>() as Component;
                if (collider.isTrigger && candidate == null) continue;
                if (_hits[i].distance >= nearest) continue;
                nearest = _hits[i].distance;
                _target = candidate != null && ((IWorldInteractable)candidate).CanInteract(_inventory.gameObject) ? candidate : null;
            }
            if (_target == null && (inventoryHud == null || !inventoryHud.IsOpen))
            {
                float closest = float.MaxValue;
                foreach (var npc in FindObjectsByType<QuestNpc>())
                {
                    float distance = (npc.transform.position - _inventory.transform.position).sqrMagnitude;
                    if (distance < closest && npc.CanInteract(_inventory.gameObject)) { closest = distance; _target = npc; }
                }
            }
        }
        private void Interact()
        {
            if (!CanInteract) return;
            if (questView != null && questView.IsOpen) return;
            if (dialogueView != null && dialogueView.IsOpen) { dialogueView.Advance(); return; }
            UpdateTarget();
            if (_target != null) ((IWorldInteractable)_target).Interact(_inventory.gameObject);
        }
        private void HandlePickup(PickupResult result)
        {
            if (pickupPrompt == null) return;
            pickupPrompt.ShowMessage(result == PickupResult.Success ? "已拾取" : result == PickupResult.InventoryFull ? "背包空间不足，剩余物品留在地上" : result == PickupResult.OutOfReach ? "距离太远或有障碍遮挡" : "物品已被拾取");
        }
        private void RefreshQuests()
        {
            if (questView == null || _quests == null || _quests.Catalog == null) return;
            if (dialogueView != null && dialogueView.IsOpen) { questView.SetTracker(""); return; }
            if (questView.CurrentQuest != null)
            {
                var state = _quests.GetState(questView.CurrentQuest);
                questView.Refresh(state.Status, state.Kills);
            }
            string text = "";
            for (int i = 0; i < _quests.Catalog.Count; i++)
            {
                var quest = _quests.Catalog.Get(i);
                if (quest == null) continue;
                var state = _quests.GetState(quest);
                if (state.Status == Game.Quests.QuestStatus.Active || state.Status == Game.Quests.QuestStatus.Ready)
                    text += quest.Title + "（队伍共享）\n击杀" + quest.TargetName + "  " + state.Kills + "/" + quest.RequiredKills +
                        (state.Status == Game.Quests.QuestStatus.Ready ? "  · 返回守卫领奖" : "") + "\n";
            }
            questView.SetTracker(text);
        }
        private void HandleQuestOperation(string message, bool success)
        {
            RefreshQuests();
            if (questView != null) questView.Complete(message, success);
            if (pickupPrompt != null) pickupPrompt.ShowMessage(message);
        }
        private void HandleOperation(InventoryOperationResult result)
        {
            if (inventoryHud != null) inventoryHud.CompleteOperation(result == InventoryOperationResult.Success, result == InventoryOperationResult.Stale);
            if (inventoryHud != null) inventoryHud.ShowMessage(result == InventoryOperationResult.Success ? "操作完成" :
                result == InventoryOperationResult.Stale ? "物品数量已更新，请重新操作" : result == InventoryOperationResult.Invalid ? "当前无法使用：检查物品和动作状态" : "暂时无法操作");
        }
        private void OnDisable()
        {
            Bind(null);
            if (inventoryHud == null) return;
            inventoryHud.OpenChanged -= SetGameplayBlocked;
            inventoryHud.MoveRequested -= Move;
            inventoryHud.DropRequested -= Drop;
            inventoryHud.FoodUseRequested -= UseFood;
            if (dialogueView != null) dialogueView.OpenChanged -= HandleDialogueOpened;
            if (questView != null) questView.OpenChanged -= HandleDialogueOpened;
        }
    }
}
