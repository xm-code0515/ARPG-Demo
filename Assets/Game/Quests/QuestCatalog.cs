using System;
using UnityEngine;

namespace Game.Quests
{
    [CreateAssetMenu(menuName = "Game/Quests/Quest Catalog")]
    public sealed class QuestCatalog : ScriptableObject
    {
        [SerializeField] private QuestDefinition[] quests = Array.Empty<QuestDefinition>();
        public int Count => quests.Length;
        public QuestDefinition Get(int index) => index >= 0 && index < quests.Length ? quests[index] : null;
        public int IndexOf(QuestDefinition quest) => Array.IndexOf(quests, quest);
        public QuestDefinition Find(string questId)
        {
            QuestDefinition found = null;
            foreach (var quest in quests)
                if (quest != null && quest.QuestId == questId)
                { if (found != null) return null; found = quest; }
            return found;
        }
    }
}
