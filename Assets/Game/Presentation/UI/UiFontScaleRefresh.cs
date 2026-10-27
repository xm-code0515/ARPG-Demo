using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class UiFontScaleRefresh : MonoBehaviour
    {
        private Canvas _canvas;
        private Text[] _texts;
        private float _scale = -1;
        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _texts = GetComponentsInChildren<Text>(true);
        }
        private void OnEnable() => _scale = -1;
        private void LateUpdate()
        {
            if (_canvas == null || Mathf.Approximately(_scale, _canvas.scaleFactor)) return;
            _scale = _canvas.scaleFactor;
            foreach (var text in _texts)
            {
                if (text == null || text.font == null) continue;
                if (text.font.dynamic) text.font.RequestCharactersInTexture(text.text, Mathf.Max(1, Mathf.RoundToInt(text.fontSize * _scale)), text.fontStyle);
                text.SetAllDirty();
            }
        }
    }
}
