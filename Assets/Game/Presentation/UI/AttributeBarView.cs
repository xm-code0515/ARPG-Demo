using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class AttributeBarView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text uguiValueText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private bool showMaximum = true;
        [SerializeField, Min(0f)] private float smoothingSharpness = 15f;

        private float _targetFill;
        private bool _hasValue;

        private void Awake()
        {
            if (fillImage == null)
                Debug.LogError("[AttributeBarView] 请引用自己配置的 Fill Image。", this);
            else if (fillImage.type != Image.Type.Filled)
                Debug.LogError("[AttributeBarView] 请将 Fill Image 的 Image Type 设置为 Filled。", this);
        }

        public void SetValue(float current, float maximum, bool immediate = false)
        {
            bool firstValue = !_hasValue;
            _hasValue = true;
            _targetFill = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            if (fillImage != null && (immediate || firstValue || smoothingSharpness <= 0f))
                fillImage.fillAmount = _targetFill;
            string label = showMaximum ? $"{current:0} / {maximum:0}" : $"{current:0}";
            if (uguiValueText != null) uguiValueText.text = label;
            if (valueText != null) valueText.text = label;
        }

        public void Clear()
        {
            _hasValue = false;
            _targetFill = 0f;
            if (fillImage != null) fillImage.fillAmount = 0f;
            if (uguiValueText != null) uguiValueText.text = string.Empty;
            if (valueText != null) valueText.text = string.Empty;
        }

        private void Update()
        {
            if (!_hasValue || fillImage == null) return;
            float factor = smoothingSharpness > 0f ? 1f - Mathf.Exp(-smoothingSharpness * Time.unscaledDeltaTime) : 1f;
            fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, _targetFill, factor);
        }
    }
}
