using Game.Character;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeathScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibilityGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Text countdownText;
        [SerializeField] private string countdownFormat = "{0} 秒后复活";
        private Font _systemFont;
        private int _displayedSeconds = -1;
        public IRespawnReadModel BoundSource { get; private set; }
        public bool IsVisible => visibilityGroup != null && visibilityGroup.alpha > 0f;

        private void Awake()
        {
            bool needsFont = (titleText != null && (titleText.font == null || titleText.font.name == "LegacyRuntime")) ||
                (countdownText != null && (countdownText.font == null || countdownText.font.name == "LegacyRuntime"));
            if (!needsFont) return;
            _systemFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 36);
            if (titleText != null && (titleText.font == null || titleText.font.name == "LegacyRuntime")) titleText.font = _systemFont;
            if (countdownText != null && (countdownText.font == null || countdownText.font.name == "LegacyRuntime")) countdownText.font = _systemFont;
        }

        private void OnEnable() => Refresh();
        private void Update() => Refresh();
        private void OnDisable() => Bind(null);
        private void OnDestroy()
        {
            if (_systemFont != null) Destroy(_systemFont);
        }

        public void Bind(IRespawnReadModel source)
        {
            if (!ReferenceEquals(BoundSource, source)) _displayedSeconds = -1;
            BoundSource = source;
            Refresh();
        }

        private void Refresh()
        {
            bool dead = BoundSource != null && BoundSource.IsDead;
            if (visibilityGroup != null)
            {
                visibilityGroup.alpha = dead ? 1f : 0f;
                visibilityGroup.interactable = false;
                visibilityGroup.blocksRaycasts = false;
            }
            if (!dead)
            {
                _displayedSeconds = -1;
                return;
            }
            int seconds = Mathf.CeilToInt(BoundSource.RemainingSeconds);
            if (countdownText != null && seconds != _displayedSeconds)
                countdownText.text = string.Format(countdownFormat, seconds);
            _displayedSeconds = seconds;
        }
    }
}
