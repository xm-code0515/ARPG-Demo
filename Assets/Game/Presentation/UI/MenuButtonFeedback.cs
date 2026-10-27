using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverClip, clickClip;
        private Button _button;
        private void Awake() => _button = GetComponent<Button>();
        public void OnPointerEnter(PointerEventData data) => Play(hoverClip);
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Play(clickClip); }
        private void Play(AudioClip clip)
        {
            if (_button != null && _button.IsInteractable() && audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, .35f);
        }
    }
}
