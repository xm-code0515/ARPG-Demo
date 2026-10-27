using Game.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class BossHealthBarView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private Text bossName;
        [SerializeField] private Image fill;
        [SerializeField] private Text healthText;
        private Font _font;
        public bool IsVisible => visibility != null && visibility.alpha > .5f;

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 32);
            if (bossName != null) bossName.font = _font;
            if (healthText != null) healthText.font = _font;
            Hide();
        }

        public void Show(string name, IAttributeReadModel attributes)
        {
            if (attributes == null || !attributes.IsReady || attributes.Snapshot.IsDead) { Hide(); return; }
            var snapshot = attributes.Snapshot;
            if (visibility != null) { visibility.alpha = 1f; visibility.blocksRaycasts = visibility.interactable = false; }
            if (bossName != null && bossName.text != name) bossName.text = name;
            if (fill != null) fill.fillAmount = Mathf.Clamp01(snapshot.Health / snapshot.Values.MaxHealth);
            if (healthText != null) healthText.text = Mathf.CeilToInt(snapshot.Health) + " / " + Mathf.CeilToInt(snapshot.Values.MaxHealth);
        }

        public void Hide() { if (visibility != null) { visibility.alpha = 0f; visibility.blocksRaycasts = visibility.interactable = false; } }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
