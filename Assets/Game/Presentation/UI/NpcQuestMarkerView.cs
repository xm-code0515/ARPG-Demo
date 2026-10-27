using Game.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    [DefaultExecutionOrder(1000)]
    public sealed class NpcQuestMarkerView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private Text symbol;
        [SerializeField] private Color availableColor = new Color(1f, .82f, .2f);
        [SerializeField] private Color activeColor = new Color(.25f, .65f, 1f);
        [SerializeField] private Color readyColor = new Color(1f, .82f, .2f);
        [SerializeField, Min(1f)] private float visibleDistance = 35f;
        private bool _hasQuest;
        public QuestStatus Status { get; private set; }
        public bool IsVisible => visibility != null && visibility.alpha > .5f;

        private void Awake() => Hide();

        public void SetStatus(QuestStatus status)
        {
            Status = status;
            _hasQuest = status != QuestStatus.Rewarded && status != QuestStatus.Locked;
            if (symbol == null) return;
            string text = status == QuestStatus.Available ? "!" : "?";
            if (symbol.text != text) symbol.text = text;
            symbol.color = status == QuestStatus.Active ? activeColor
                : status == QuestStatus.Ready ? readyColor : availableColor;
        }

        public void Hide()
        {
            _hasQuest = false;
            if (visibility != null) visibility.alpha = 0f;
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            bool show = _hasQuest && camera != null &&
                (camera.transform.position - transform.position).sqrMagnitude <= visibleDistance * visibleDistance;
            if (visibility != null)
            {
                visibility.alpha = show ? 1f : 0f;
                visibility.blocksRaycasts = visibility.interactable = false;
            }
            if (show) transform.rotation = camera.transform.rotation;
        }
    }
}
