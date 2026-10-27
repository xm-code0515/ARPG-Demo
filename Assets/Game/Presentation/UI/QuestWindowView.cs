using System;
using Game.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class QuestWindowView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private Text title, description, objective, message, acceptLabel;
        [SerializeField] private Button acceptButton, rejectButton;
        [SerializeField] private Image[] rewardIcons;
        [SerializeField] private Text[] rewardLabels;
        [SerializeField] private Text tracker;
        private Font _font;
        private Action _accept, _claim;
        private QuestStatus _status;
        private bool _pending;
        public bool IsOpen { get; private set; }
        public QuestDefinition CurrentQuest { get; private set; }
        public event Action<bool> OpenChanged;
        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
            foreach (var text in GetComponentsInChildren<Text>(true)) text.font = _font;
            acceptButton.onClick.AddListener(Submit);
            rejectButton.onClick.AddListener(Close);
            Close();
            if (tracker != null) tracker.text = "";
        }
        public void Show(QuestDefinition quest, Action accept, Action claim)
        {
            CurrentQuest = quest; _accept = accept; _claim = claim; _pending = false;
            title.text = quest.Title; description.text = quest.Description; message.text = "";
            for (int i = 0; i < rewardLabels.Length; i++)
            {
                bool valid = i < quest.Rewards.Length && quest.Rewards[i].item != null;
                rewardLabels[i].transform.parent.gameObject.SetActive(valid);
                if (!valid) continue;
                var reward = quest.Rewards[i];
                rewardLabels[i].text = reward.item.DisplayName + " × " + reward.quantity;
                if (i < rewardIcons.Length) { rewardIcons[i].sprite = reward.item.Icon; rewardIcons[i].enabled = reward.item.Icon != null; }
            }
            SetOpen(true);
        }
        public void Refresh(QuestStatus status, int kills)
        {
            _status = status;
            if (CurrentQuest == null) return;
            objective.text = "目标：击杀 " + CurrentQuest.TargetName + "  " + kills + " / " + CurrentQuest.RequiredKills;
            acceptLabel.text = status == QuestStatus.Ready ? "领取奖励" : status == QuestStatus.Active ? "进行中" : status == QuestStatus.Rewarded ? "已完成" : status == QuestStatus.Locked ? "尚未解锁" : "接受任务";
            rejectButton.GetComponentInChildren<Text>().text = status == QuestStatus.Available ? "拒绝" : "关闭";
            acceptButton.interactable = !_pending && (status == QuestStatus.Available || status == QuestStatus.Ready);
        }
        public void SetTracker(string text) { if (tracker != null) tracker.text = text; }
        private void Submit()
        {
            if (!IsOpen || _pending || (_status != QuestStatus.Available && _status != QuestStatus.Ready)) return;
            _pending = true; acceptButton.interactable = false;
            message.text = "正在确认…";
            if (_status == QuestStatus.Ready) _claim?.Invoke(); else _accept?.Invoke();
        }
        public void Complete(string result, bool success)
        {
            _pending = false; message.text = result;
            // Owner RPC can arrive before the replicated quest list on a remote client.
            if (success) Close();
            else acceptButton.interactable = _status == QuestStatus.Available || _status == QuestStatus.Ready;
        }
        public void Close() { _pending = false; SetOpen(false); }
        private void SetOpen(bool value)
        {
            bool changed = IsOpen != value; IsOpen = value;
            if (visibility != null) { visibility.alpha = value ? 1f : 0f; visibility.interactable = visibility.blocksRaycasts = value; }
            if (changed) OpenChanged?.Invoke(value);
        }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
