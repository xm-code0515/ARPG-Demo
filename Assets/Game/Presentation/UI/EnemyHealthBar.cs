using Game.Attributes.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private CharacterAttributes attributes;
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private Image fill;
        [SerializeField] private Text nameText;
        [SerializeField] private Text healthText;
        [SerializeField, Min(1f)] private float visibleDistance = 20f;
        private Font _font;

        public void SetDisplayName(string value) { if (nameText != null) nameText.text = value; }

        private void Awake()
        {
            if (attributes == null) attributes = GetComponentInParent<CharacterAttributes>();
            if (nameText != null && (nameText.font == null || nameText.font.name == "LegacyRuntime"))
            {
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 32);
                nameText.font = _font;
                if (healthText != null) healthText.font = _font;
            }
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            bool show = attributes != null && attributes.IsReady && !attributes.Snapshot.IsDead && camera != null &&
                (camera.transform.position - transform.position).sqrMagnitude < visibleDistance * visibleDistance;
            if (visibility != null) { visibility.alpha = show ? 1f : 0f; visibility.blocksRaycasts = visibility.interactable = false; }
            if (!show) return;
            transform.rotation = camera.transform.rotation;
            var snapshot = attributes.Snapshot;
            if (fill != null) fill.fillAmount = snapshot.Health / snapshot.Values.MaxHealth;
            if (healthText != null) healthText.text = Mathf.CeilToInt(snapshot.Health) + " / " + Mathf.CeilToInt(snapshot.Values.MaxHealth);
        }

        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
