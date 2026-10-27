using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class PickupPromptView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private Text pickupText;
        [SerializeField] private Text messageText;
        [SerializeField] private RectTransform crosshair;
        private Font _font;
        private float _messageUntil;
        private void Awake()
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.font == null || text.font.name == "LegacyRuntime")
                {
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                    text.font = _font;
                }
        }
        public void ShowPickup(string name, int quantity)
        {
            ShowInteraction(string.IsNullOrEmpty(name) ? "" : "[E] 拾取 " + name + " × " + quantity, true);
        }
        public void ShowInteraction(string prompt, bool showCrosshair)
        {
            if (pickupText != null) pickupText.text = prompt;
            if (crosshair != null) crosshair.gameObject.SetActive(showCrosshair);
            if (messageText != null && Time.unscaledTime > _messageUntil) messageText.text = "";
            if (visibility != null) { visibility.alpha = 1f; visibility.interactable = visibility.blocksRaycasts = false; }
        }
        public void ShowMessage(string message)
        {
            _messageUntil = Time.unscaledTime + 2f;
            if (messageText != null) messageText.text = message;
        }
        public void Hide() { if (visibility != null) visibility.alpha = 0f; }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
