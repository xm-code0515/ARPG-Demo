using System;
using Game.Items;
using UnityEngine;

namespace Game.Quests
{
    [Serializable]
    public struct QuestReward
    {
        public ItemDefinition item;
        [Min(1)] public int quantity;
    }
    [CreateAssetMenu(menuName = "Game/Quests/Kill Quest")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string questId = "clear_skeletons";
        [SerializeField] private string title = "桥头的骷髅";
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField] private string targetEnemyId = "skeleton";
        [SerializeField] private string targetName = "骷髅";
        [SerializeField, Min(1)] private int requiredKills = 3;
        [SerializeField] private QuestDefinition prerequisite;
        [SerializeField] private QuestReward[] rewards = Array.Empty<QuestReward>();
        public string QuestId => questId;
        public string Title => title;
        public string Description => description;
        public string TargetEnemyId => targetEnemyId;
        public string TargetName => targetName;
        public int RequiredKills => Mathf.Max(1, requiredKills);
        public QuestDefinition Prerequisite => prerequisite;
        public QuestReward[] Rewards => rewards;
    }
}
