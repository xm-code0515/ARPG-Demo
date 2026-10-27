using Game.Attributes.Unity;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Network
{
    [RequireComponent(typeof(QuestNpc))]
    public sealed class NpcQuestMarkerBinder : MonoBehaviour
    {
        [SerializeField] private NpcQuestMarkerView view;
        private QuestNpc _npc;
        private void Awake() => _npc = GetComponent<QuestNpc>();

        private void LateUpdate()
        {
            if (view == null) return;
            var player = NetworkCharacter.LocalPlayer;
            var attributes = player != null ? player.GetComponent<CharacterAttributes>() : null;
            var quests = player != null ? player.GetComponent<NetworkPlayerQuests>() : null;
            if (player == null || !player.IsSpawned || player.gameObject.scene != gameObject.scene ||
                attributes == null || !attributes.IsReady || attributes.Snapshot.IsDead ||
                quests == null || !quests.IsSpawned || quests.Catalog == null)
            {
                view.Hide();
                return;
            }
            var quest = _npc.GetQuest(quests);
            if (quest == null) view.Hide();
            else view.SetStatus(quests.GetState(quest).Status);
        }

        private void OnDisable() { if (view != null) view.Hide(); }
    }
}
