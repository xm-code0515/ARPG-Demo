using Game.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class HotbarHud : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private InventorySlotView[] slots;
        private InventoryHud _inventory;
        private Font _font;
        private void Awake()
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.font == null || text.font.name == "LegacyRuntime")
                {
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                    text.font = _font;
                }
            Refresh();
        }
        public void Attach(InventoryHud inventory)
        {
            _inventory = inventory;
            if (slots != null) for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].Bind(inventory, InventorySlotAddress.HotbarStart + i);
            Refresh();
        }
        public InventorySlotView GetSlotView(int index) => slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
        public bool Contains(Vector2 point)
        {
            var rect = transform as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            return _inventory != null && _inventory.IsOpen && rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, point,
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null);
        }
        public void Refresh()
        {
            var source = _inventory != null ? _inventory.BoundSource : null;
            bool ready = source != null && source.IsReady && source is IHotbarReadModel;
            if (visibility != null)
            {
                visibility.alpha = ready ? 1 : 0;
                visibility.interactable = visibility.blocksRaycasts = ready && _inventory.IsOpen;
            }
            if (slots == null) return;
            var hotbar = source as IHotbarReadModel;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                int address = InventorySlotAddress.HotbarStart + i;
                var stack = ready && i < hotbar.HotbarSlotCount ? source.GetSlot(address) : new ItemStack(-1, 0);
                int quantity = ready ? _inventory.DisplayQuantity(address) : 0;
                slots[i].Refresh(quantity > 0 ? source.Catalog.Get(stack.ItemIndex) : null, quantity);
                slots[i].SetSelected(ready && hotbar.EquippedHotbarSlot == address);
            }
        }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
