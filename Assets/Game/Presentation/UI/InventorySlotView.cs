using Game.Items;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Presentation.UI
{
    public sealed class InventorySlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text quantityText;
        [SerializeField] private Image selection;
        private InventoryHud _hud;
        private int _index;
        public void Bind(InventoryHud hud, int index) { _hud = hud; _index = index; }
        public void SetSelected(bool value) { if (selection != null) selection.enabled = value; }
        public void OnPointerClick(PointerEventData e)
        {
            bool shift = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            if (e.button == PointerEventData.InputButton.Left) _hud?.SelectSlot(_index, e.position, shift);
            else if (e.button == PointerEventData.InputButton.Right) _hud?.UseFood(_index);
        }
        public void OnPointerEnter(PointerEventData e) => _hud?.HoverSlot(_index, e.position);
        public void OnPointerExit(PointerEventData e) => _hud?.HideTooltip();
        public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) _hud?.BeginDrag(_index, e.position); }
        public void OnDrag(PointerEventData e) => _hud?.Drag(e.position);
        public void OnEndDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) _hud?.EndDrag(e.position); }
        public void OnDrop(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) _hud?.DropOn(_index); }
        public void Refresh(ItemDefinition item, int quantity)
        {
            if (icon != null) { icon.sprite = item != null ? item.Icon : null; icon.enabled = icon.sprite != null; }
            if (nameText != null) nameText.text = item != null ? item.DisplayName : "";
            if (quantityText != null) quantityText.text = item != null ? quantity.ToString() : "";
        }
    }
}
