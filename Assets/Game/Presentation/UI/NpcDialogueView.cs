using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class NpcDialogueView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private Text speaker, content;
        [SerializeField] private Button continueButton;
        [SerializeField] private RectTransform bubble;
        private string[] _lines;
        private int _line;
        private Font _font;
        private Transform _anchor;
        private Action _finished;
        public bool IsOpen { get; private set; }
        public event Action<bool> OpenChanged;
        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(Advance);
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.font == null || text.font.name == "LegacyRuntime")
                {
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                    text.font = _font;
                }
            Close();
        }
        public void Show(string name, string[] lines, Transform anchor = null, Action finished = null)
        {
            if (lines == null || lines.Length == 0) return;
            _anchor = anchor; _finished = finished;
            _lines = lines; _line = 0;
            if (speaker != null) speaker.text = name;
            if (content != null) content.text = _lines[0];
            SetOpen(true);
        }
        public void Advance()
        {
            if (!IsOpen) return;
            if (++_line >= _lines.Length)
            {
                var finished = _finished;
                Close();
                finished?.Invoke();
                return;
            }
            if (content != null) content.text = _lines[_line];
        }
        public void Close() { _lines = null; _anchor = null; _finished = null; SetOpen(false); }
        private void LateUpdate()
        {
            if (!IsOpen || _anchor == null || bubble == null || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(_anchor.position);
            if (screen.z <= 0f) { Close(); return; }
            var canvas = bubble.GetComponentInParent<Canvas>();
            var parent = bubble.parent as RectTransform;
            if (canvas == null || parent == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local);
            float halfWidth = bubble.rect.width * .5f;
            local.x = Mathf.Clamp(local.x, parent.rect.xMin + halfWidth + 12f, parent.rect.xMax - halfWidth - 12f);
            local.y = Mathf.Clamp(local.y, parent.rect.yMin + 12f, parent.rect.yMax - bubble.rect.height - 12f);
            bubble.anchoredPosition = local;
        }
        private void SetOpen(bool value)
        {
            bool changed = IsOpen != value; IsOpen = value;
            if (visibility != null) { visibility.alpha = value ? 1 : 0; visibility.interactable = visibility.blocksRaycasts = value; }
            if (changed) OpenChanged?.Invoke(value);
        }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
