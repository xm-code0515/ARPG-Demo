using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    [RequireComponent(typeof(Slider))]
    public sealed class InventoryQuantityPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform window;
        [SerializeField] private Text quantityText;
        [SerializeField] private Button confirmButton;
        private Slider _slider;
        private bool _initialized;
        public RectTransform Window => window;
        public event Action<int> Confirmed;
        private void Awake() => Initialize();
        private void Initialize()
        {
            if (_initialized) return;
            _slider = GetComponent<Slider>();
            _slider.onValueChanged.AddListener(UpdateQuantity);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmSelection);
            _initialized = true;
        }
        public void Show(int maximum)
        {
            Initialize();
            _slider.minValue = 1; _slider.maxValue = Mathf.Max(1, maximum); _slider.wholeNumbers = true;
            _slider.SetValueWithoutNotify(Mathf.Max(1, maximum / 2));
            UpdateQuantity(_slider.value);
            window.gameObject.SetActive(true); window.SetAsLastSibling();
        }
        public void Hide() { if (window != null) window.gameObject.SetActive(false); }
        private void UpdateQuantity(float value)
        { if (quantityText != null) quantityText.text = "选中数量：" + Mathf.RoundToInt(value); }
        private void ConfirmSelection()
        { if (window != null && window.gameObject.activeInHierarchy && _slider != null) Confirmed?.Invoke(Mathf.RoundToInt(_slider.value)); }
        private void OnDestroy()
        {
            if (_slider != null) _slider.onValueChanged.RemoveListener(UpdateQuantity);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmSelection);
        }
    }
}
