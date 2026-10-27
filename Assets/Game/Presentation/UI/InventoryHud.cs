using System;
using System.Text;
using Game.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class InventoryHud : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text capacityText, operationText;
        [SerializeField] private InventorySlotView[] slots;
        [SerializeField] private InventorySlotView mainHandSlot;
        [SerializeField] private InventoryQuantityPopup quantityPopup;
        [SerializeField] private RectTransform tooltip, dragGhost;
        [SerializeField] private Text tooltipText, dragText;
        [SerializeField] private Image dragIcon;
        [SerializeField, Min(0)] private float tooltipDelay = .5f;
        private Font _font;
        private int _hovered = -1, _carrySource = -1, _carryQuantity, _quantitySource = -1;
        private ItemStack _carryExpected, _quantityExpected;
        private float _hoverStarted, _messageUntil, _requestStarted;
        private bool _dragging, _pending;
        private HotbarHud _hotbar;
        public event Action<bool> OpenChanged;
        public event Action<int, int, int, ItemStack, ItemStack> MoveRequested;
        public event Action<int, int, ItemStack> DropRequested;
        public event Action<int, ItemStack> FoodUseRequested;
        public IInventoryReadModel BoundSource { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsCarrying => _carrySource != -1;
        public bool IsBusy => IsCarrying || _pending || _quantitySource >= 0;
        public int CarriedQuantity => _carryQuantity;
        public bool OperationPending => _pending;
        public void AttachHotbar(HotbarHud hotbar)
        {
            if (_hotbar != null && _hotbar != hotbar) _hotbar.Attach(null);
            _hotbar = hotbar;
            if (_hotbar != null) _hotbar.Attach(this);
            Refresh();
        }
        public int DisplayQuantity(int slot)
        {
            var stack = Slot(slot);
            bool carried = IsCarrying && (slot == _carrySource ||
                (_carrySource == -2 && BoundSource is IHotbarReadModel hotbar && hotbar.EquippedHotbarSlot == slot));
            return Mathf.Max(0, stack.Quantity - (carried && Matches(stack, _carryExpected) ? _carryQuantity : 0));
        }
        private void Awake()
        {
            if (slots != null) for (int i = 0; i < slots.Length; i++) if (slots[i] != null) slots[i].Bind(this, i);
            if (mainHandSlot != null) mainHandSlot.Bind(this, -2);
            if (quantityPopup != null) quantityPopup.Confirmed += ConfirmQuantity;
            HideTooltip(); ClearCarry(); CloseQuantity();
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.font == null || text.font.name == "LegacyRuntime")
                {
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                    text.font = _font;
                }
        }
        public void Bind(IInventoryReadModel source)
        {
            if (ReferenceEquals(BoundSource, source)) return;
            if (BoundSource != null) BoundSource.Changed -= Refresh;
            if (BoundSource is IEquipmentReadModel oldEquipment) oldEquipment.EquipmentChanged -= Refresh;
            BoundSource = source;
            if (BoundSource != null) BoundSource.Changed += Refresh;
            if (BoundSource is IEquipmentReadModel equipment) equipment.EquipmentChanged += Refresh;
            _pending = false;
            SetOpen(false);
        }
        public void SetOpen(bool value)
        {
            if (value && (BoundSource == null || !BoundSource.IsReady)) value = false;
            bool changed = IsOpen != value;
            IsOpen = value;
            if (!value) { HideTooltip(); ClearCarry(); CloseQuantity(); }
            Refresh();
            if (changed) OpenChanged?.Invoke(value);
        }
        public void ShowMessage(string message)
        {
            if (operationText != null) operationText.text = message;
            _messageUntil = Time.unscaledTime + 2;
        }
        public void CompleteOperation(bool success, bool stale)
        {
            if (!_pending) return;
            _pending = false;
            if (success || stale || !Matches(Slot(_carrySource), _carryExpected)) ClearCarry();
            Refresh();
        }
        private void Update()
        {
            if (operationText != null && Time.unscaledTime > _messageUntil) operationText.text = "";
            if (_pending && Time.unscaledTime - _requestStarted > 8)
            { _pending = false; ClearCarry(); Refresh(); ShowMessage("操作等待超时，请重新选择物品"); }
            if (!IsOpen || Mouse.current == null) return;
            var point = Mouse.current.position.ReadValue();
            if (IsCarrying) Drag(point);
            if (_hovered != -1 && !IsBusy && Time.unscaledTime - _hoverStarted >= tooltipDelay)
            { RefreshTooltip(); Position(tooltip, TooltipPosition(point)); }
        }
        private ItemStack Slot(int index) => index == -2 && BoundSource is IEquipmentReadModel equipment ? equipment.MainHand :
            BoundSource != null && BoundSource.IsReady && ValidSlot(index) ? BoundSource.GetSlot(index) : new ItemStack(-1, 0);
        private bool ValidSlot(int slot) => BoundSource != null && ((slot >= 0 && slot < BoundSource.SlotCount) ||
            (InventorySlotAddress.IsHotbar(slot) && BoundSource is IHotbarReadModel hotbar && slot - InventorySlotAddress.HotbarStart < hotbar.HotbarSlotCount));
        private static bool Matches(ItemStack a, ItemStack b) => a.ItemIndex == b.ItemIndex && a.Quantity == b.Quantity;
        public void SelectSlot(int index)
        {
            bool shift = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            SelectSlot(index, Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero, shift);
        }
        public void SelectSlot(int index, Vector2 position, bool shift)
        {
            if (!IsOpen || _pending) return;
            if (IsCarrying) { DropOn(index); return; }
            CloseQuantity(); HideTooltip();
            var stack = Slot(index);
            if (stack.IsEmpty) return;
            if (shift && quantityPopup != null && index >= 0)
            {
                _quantitySource = index; _quantityExpected = stack;
                quantityPopup.Show(stack.Quantity);
                var view = InventorySlotAddress.IsHotbar(index) ? _hotbar != null ? _hotbar.GetSlotView(index - InventorySlotAddress.HotbarStart) : null : slots[index];
                var rect = view != null ? view.transform as RectTransform : null;
                if (rect == null) { CloseQuantity(); return; }
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var canvas = rect.GetComponentInParent<Canvas>();
                Position(quantityPopup.Window, RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, corners[0]) + new Vector2(0, -4));
            }
            else PickUp(index, stack.Quantity, stack, position);
        }
        private void ConfirmQuantity(int quantity)
        {
            int source = _quantitySource; var expected = _quantityExpected;
            CloseQuantity();
            if (!IsOpen || _pending || !Matches(Slot(source), expected)) { ShowMessage("物品数量已更新，请重新选择"); return; }
            PickUp(source, Mathf.Clamp(quantity, 1, expected.Quantity), expected, Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
        }
        private void CloseQuantity() { _quantitySource = -1; if (quantityPopup != null) quantityPopup.Hide(); }
        private void PickUp(int source, int quantity, ItemStack expected, Vector2 position)
        {
            _carrySource = source; _carryQuantity = quantity; _carryExpected = expected;
            var item = BoundSource.Catalog.Get(expected.ItemIndex);
            if (item == null) { ClearCarry(); return; }
            if (dragText != null) dragText.text = item.DisplayName + " × " + quantity;
            if (dragIcon != null) { dragIcon.sprite = item.Icon; dragIcon.enabled = item.Icon != null; }
            if (dragGhost != null) { dragGhost.gameObject.SetActive(true); dragGhost.SetAsLastSibling(); }
            HideTooltip(); Drag(position); Refresh();
        }
        public void HoverSlot(int index, Vector2 position)
        {
            if (!IsOpen || IsBusy) return;
            HideTooltip(); _hovered = index; _hoverStarted = Time.unscaledTime;
        }
        private Vector2 TooltipPosition(Vector2 point)
        {
            var canvas = tooltip != null ? tooltip.GetComponentInParent<Canvas>() : null;
            return point + new Vector2(-(tooltip != null ? tooltip.rect.width * (canvas != null ? canvas.scaleFactor : 1) : 320) - 16, -16);
        }
        private void RefreshTooltip()
        {
            var stack = Slot(_hovered); var item = !stack.IsEmpty ? BoundSource.Catalog.Get(stack.ItemIndex) : null;
            bool show = IsOpen && !IsBusy && item != null && Time.unscaledTime - _hoverStarted >= tooltipDelay;
            if (tooltip != null) tooltip.gameObject.SetActive(show);
            if (!show || tooltipText == null) return;
            string category = item.Category == ItemCategory.Material ? "材料" : item.Category == ItemCategory.Weapon ? "武器" : item.Category == ItemCategory.Armor ? "护甲" : "食物";
            var details = new StringBuilder(item.DisplayName).Append('\n').Append(category).Append("  ·  数量 ").Append(stack.Quantity).Append(" / ").Append(item.StackLimit);
            if (item.Weapon != null)
            {
                var weapon = item.Weapon;
                details.Append("\n").Append(weapon.AttackPattern != null ? weapon.AttackPattern.DisplayName : "近战武器")
                    .Append("  ·  ").Append(weapon.Attacks.Length).Append(" 段连击")
                    .Append("\n基础攻击力：").Append(weapon.BaseDamage.ToString("0.#"))
                    .Append("\n攻击力属性系数：").Append(weapon.AttackPowerScale.ToString("0.##"))
                    .Append("\n耐力消耗：").Append(weapon.StaminaCost.ToString("0.#"))
                    .Append("\n攻击范围：").Append(weapon.Range.ToString("0.#")).Append(" 米");
                if (weapon.AttackPattern != null)
                {
                    details.Append("\n连击基础伤害：");
                    for (int i = 0; i < weapon.Attacks.Length; i++)
                    { if (i > 0) details.Append(" / "); details.Append((weapon.BaseDamage * weapon.Attacks[i].damageMultiplier).ToString("0.#")); }
                    details.Append("\n连击耐力消耗：");
                    for (int i = 0; i < weapon.Attacks.Length; i++)
                    { if (i > 0) details.Append(" / "); details.Append((weapon.StaminaCost * weapon.Attacks[i].staminaMultiplier).ToString("0.#")); }
                }
            }
            if (item.Food != null)
            {
                details.Append("\n持续时间：").Append(item.Food.Duration.ToString("0")).Append(" 秒")
                    .Append("\n生命恢复：").Append(item.Food.HealthRecoveryPerSecond.ToString("0.#")).Append(" / 秒")
                    .Append("\n额外耐力恢复：").Append(item.Food.StaminaRecoveryPerSecond.ToString("0.#")).Append(" / 秒");
                foreach (var modifier in item.Food.CreateModifiers("tooltip"))
                {
                    string label = modifier.Attribute == Game.Attributes.AttributeType.MaxHealth ? "生命上限" :
                        modifier.Attribute == Game.Attributes.AttributeType.MaxStamina ? "耐力上限" :
                        modifier.Attribute == Game.Attributes.AttributeType.AttackPower ? "攻击力" :
                        modifier.Attribute == Game.Attributes.AttributeType.Defense ? "防御力" : "移动速度";
                    details.Append('\n').Append(label).Append("：");
                    if (modifier.Operation == Game.Attributes.AttributeModifierOperation.AdditivePercent)
                        details.Append((modifier.Value * 100f).ToString("+0.#;-0.#;0")).Append('%');
                    else if (modifier.Operation == Game.Attributes.AttributeModifierOperation.Multiply)
                        details.Append('×').Append(modifier.Value.ToString("0.##"));
                    else if (modifier.Operation == Game.Attributes.AttributeModifierOperation.Override)
                        details.Append("设为 ").Append(modifier.Value.ToString("0.#"));
                    else details.Append(modifier.Value.ToString("+0.#;-0.#;0"));
                }
                details.Append("\n右键食用 · 同种食物刷新时间");
            }
            if (item.DisplayProperties != null) foreach (var property in item.DisplayProperties)
                if (!string.IsNullOrWhiteSpace(property.name)) details.Append('\n').Append(property.name).Append("：").Append(property.value);
            if (!string.IsNullOrWhiteSpace(item.Description)) details.Append("\n\n").Append(item.Description);
            tooltipText.text = details.ToString();
            if (tooltip != null) { tooltip.sizeDelta = new Vector2(tooltip.sizeDelta.x, tooltipText.preferredHeight + 32); tooltip.SetAsLastSibling(); }
        }
        public void HideTooltip() { _hovered = -1; if (tooltip != null) tooltip.gameObject.SetActive(false); }
        public void UseFood(int slot)
        {
            if (!IsOpen || IsCarrying || _pending || _quantitySource >= 0 || !ValidSlot(slot) || FoodUseRequested == null) return;
            var stack = Slot(slot);
            var item = !stack.IsEmpty ? BoundSource.Catalog.Get(stack.ItemIndex) : null;
            if (item == null || item.Category != ItemCategory.Food || item.Food == null) return;
            HideTooltip(); FoodUseRequested.Invoke(slot, stack);
        }
        public void BeginDrag(int source, Vector2 position)
        {
            if (!IsOpen || _pending) return;
            CloseQuantity();
            if (!IsCarrying)
            {
                var stack = Slot(source); if (stack.IsEmpty) return;
                PickUp(source, stack.Quantity, stack, position);
            }
            _dragging = true;
        }
        public void Drag(Vector2 position) => Position(dragGhost, position + new Vector2(16, -16));
        public void EndDrag(Vector2 position)
        {
            if (_dragging && !_pending && IsCarrying && !InsidePanel(position)) DropOutside();
            _dragging = false;
        }
        private void ClearCarry() { _carrySource = -1; _carryQuantity = 0; _dragging = false; if (dragGhost != null) dragGhost.gameObject.SetActive(false); }
        public bool InsidePanel(Vector2 point)
        {
            var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
            return (panel != null && RectTransformUtility.RectangleContainsScreenPoint(panel, point, canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null)) ||
                (_hotbar != null && _hotbar.Contains(point));
        }
        public void OutsideClick()
        {
            if (_quantitySource >= 0) { CloseQuantity(); return; }
            DropOutside();
        }
        public void DropOutside()
        {
            if (!IsOpen || !IsCarrying || _pending || DropRequested == null) return;
            BeginRequest(); DropRequested.Invoke(_carrySource, _carryQuantity, _carryExpected);
        }
        public void DropOn(int target)
        {
            if (!IsOpen || !IsCarrying || _pending) return;
            if (target == _carrySource) { ClearCarry(); Refresh(); return; }
            if ((target != -2 && !ValidSlot(target)) || MoveRequested == null) return;
            BeginRequest(); MoveRequested.Invoke(_carrySource, target, _carryQuantity, _carryExpected, Slot(target));
        }
        private void BeginRequest() { _pending = true; _requestStarted = Time.unscaledTime; _dragging = false; }
        private void Position(RectTransform rect, Vector2 screen)
        {
            if (rect == null || !(rect.parent is RectTransform parent)) return;
            var canvas = rect.GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, out var local);
            float minX = parent.rect.xMin + rect.rect.width * rect.pivot.x, maxX = parent.rect.xMax - rect.rect.width * (1 - rect.pivot.x);
            float minY = parent.rect.yMin + rect.rect.height * rect.pivot.y, maxY = parent.rect.yMax - rect.rect.height * (1 - rect.pivot.y);
            rect.localPosition = new Vector3(Mathf.Clamp(local.x, minX, Mathf.Max(minX, maxX)), Mathf.Clamp(local.y, minY, Mathf.Max(minY, maxY)), 0);
        }
        private void Refresh()
        {
            bool ready = BoundSource != null && BoundSource.IsReady;
            if (IsCarrying && !_pending && !Matches(Slot(_carrySource), _carryExpected))
            { ClearCarry(); ShowMessage("物品数量已更新，请重新选择"); }
            if (_quantitySource >= 0 && !Matches(Slot(_quantitySource), _quantityExpected)) CloseQuantity();
            if (visibility != null) { visibility.alpha = ready && IsOpen ? 1f : 0f; visibility.interactable = visibility.blocksRaycasts = ready && IsOpen; }
            int count = ready ? BoundSource.SlotCount : 0, used = 0;
            for (int i = 0; i < count; i++) if (!Slot(i).IsEmpty) used++;
            if (capacityText != null) capacityText.text = used + " / " + count + " 格";
            if (slots != null) for (int i = 0; i < slots.Length; i++)
            {
                var stack = Slot(i);
                int displayed = stack.Quantity - (IsCarrying && i == _carrySource && Matches(stack, _carryExpected) ? _carryQuantity : 0);
                if (slots[i] != null)
                { slots[i].Refresh(displayed > 0 ? BoundSource.Catalog.Get(stack.ItemIndex) : null, displayed); slots[i].SetSelected(i == _carrySource); }
            }
            if (mainHandSlot != null)
            {
                var worn = Slot(-2);
                int displayed = worn.Quantity - (IsCarrying && _carrySource == -2 ? _carryQuantity : 0);
                mainHandSlot.Refresh(ready && displayed > 0 ? BoundSource.Catalog.Get(worn.ItemIndex) : null, displayed);
                mainHandSlot.SetSelected(_carrySource == -2);
            }
            RefreshTooltip();
            if (_hotbar != null) _hotbar.Refresh();
        }
        private void OnDisable() => Bind(null);
        private void OnDestroy() { if (quantityPopup != null) quantityPopup.Confirmed -= ConfirmQuantity; if (_font != null) Destroy(_font); }
    }
}
