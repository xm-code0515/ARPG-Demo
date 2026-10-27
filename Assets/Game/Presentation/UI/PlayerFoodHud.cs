using Game.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class PlayerFoodHud : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private FoodSlotView[] slots;
        private float _nextRefresh;
        private Font _font;
        public IActiveFoodReadModel BoundSource { get; private set; }
        private void Awake()
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.font == null || text.font.name == "LegacyRuntime")
                {
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 20);
                    text.font = _font;
                }
        }
        public void Bind(IActiveFoodReadModel source)
        {
            if (ReferenceEquals(source, BoundSource)) return;
            if (BoundSource != null) BoundSource.FoodsChanged -= Refresh;
            BoundSource = source;
            if (BoundSource != null) BoundSource.FoodsChanged += Refresh;
            Refresh();
        }
        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }
        private void Refresh()
        {
            bool ready = BoundSource != null && BoundSource.IsReady;
            if (visibility != null) { visibility.alpha = ready ? 1f : 0f; visibility.interactable = visibility.blocksRaycasts = false; }
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                var food = ready && i < BoundSource.FoodSlotCount ? BoundSource.GetFood(i) : new ActiveFood(-1, 0);
                double remaining = ready ? System.Math.Max(0, food.EndsAt - BoundSource.CurrentTime) : 0;
                var item = ready && !food.IsEmpty && remaining > 0 ? BoundSource.Catalog.Get(food.ItemIndex) : null;
                slots[i].Refresh(item, remaining);
                slots[i].gameObject.SetActive(ready && i < BoundSource.FoodSlotCount);
            }
        }
        private void OnDisable() => Bind(null);
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
