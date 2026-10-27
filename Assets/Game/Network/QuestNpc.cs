using Game.Attributes.Unity;
using Game.Presentation.UI;
using Game.Presentation.World;
using Game.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    [RequireComponent(typeof(NpcFacing))]
    public sealed class QuestNpc : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] private string npcId = "bridge_guard";
        [SerializeField] private string displayName = "桥头守卫";
        [SerializeField] private QuestDefinition quest;
        [SerializeField] private QuestDefinition[] followUpQuests;
        [SerializeField, TextArea(2, 5)] private string[] dialogueLines;
        [SerializeField, TextArea(2, 5)] private string[] followUpDialogueLines;
        [SerializeField, Min(.5f)] private float interactionRange = 3.5f;
        [SerializeField] private Transform dialogueAnchor;
        private static readonly QuestStatus[] Priority = { QuestStatus.Ready, QuestStatus.Active, QuestStatus.Available };
        public string NpcId => npcId;
        public float InteractionRange => interactionRange;
        public QuestDefinition Quest => quest;
        public bool Offers(QuestDefinition candidate) => candidate != null &&
            (candidate == quest || (followUpQuests != null && System.Array.IndexOf(followUpQuests, candidate) >= 0));
        public QuestDefinition GetQuest(NetworkPlayerQuests player)
        {
            if (player == null || player.Catalog == null) return quest;
            // Unclaimed rewards take priority; teammates may already be on the next quest.
            foreach (var status in Priority)
                for (int i = 0; i < player.Catalog.Count; i++)
                {
                    var candidate = player.Catalog.Get(i);
                    if (Offers(candidate) && player.GetState(candidate).Status == status) return candidate;
                }
            for (int i = player.Catalog.Count - 1; i >= 0; i--)
            {
                var candidate = player.Catalog.Get(i);
                if (Offers(candidate) && player.GetState(candidate).Status != QuestStatus.Locked) return candidate;
            }
            return null;
        }
        public string InteractionPrompt => "[E] 对话 " + displayName;
        public static QuestNpc Find(string id, Scene scene)
        {
            QuestNpc found = null;
            foreach (var npc in FindObjectsByType<QuestNpc>())
                if (npc.gameObject.scene == scene && npc.npcId == id)
                { if (found != null) return null; found = npc; }
            return found;
        }
        public bool CanInteract(GameObject actor)
        {
            if (!isActiveAndEnabled || quest == null || actor == null || actor.scene != gameObject.scene ||
                Vector3.Distance(actor.transform.position, transform.position) > interactionRange) return false;
            var attributes = actor.GetComponent<CharacterAttributes>();
            if (attributes == null || !attributes.IsReady || attributes.Snapshot.IsDead) return false;
            var start = actor.transform.position + Vector3.up;
            var end = transform.position + Vector3.up;
            foreach (var hit in Physics.RaycastAll(start, (end - start).normalized, Vector3.Distance(start, end), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(actor.transform) && !hit.collider.transform.IsChildOf(transform)) return false;
            return true;
        }
        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            var player = actor.GetComponent<NetworkPlayerQuests>();
            var window = FindAnyObjectByType<QuestWindowView>();
            if (player == null || window == null) return;
            var selected = GetQuest(player);
            if (selected == null) return;
            if (player.GetState(selected).Status != QuestStatus.Available)
            {
                GetComponent<NpcFacing>().Face(actor.transform);
                FindAnyObjectByType<NpcDialogueView>()?.Close();
                OpenQuestWindow(actor, player, window, selected);
                return;
            }
            var dialogue = FindAnyObjectByType<NpcDialogueView>();
            if (dialogue == null) return;
            GetComponent<NpcFacing>().Face(actor.transform);
            dialogue.Show(displayName, selected == quest ? dialogueLines : followUpDialogueLines,
                dialogueAnchor != null ? dialogueAnchor : transform,
                () => OpenQuestWindow(actor, player, window, selected));
        }
        private void OpenQuestWindow(GameObject actor, NetworkPlayerQuests player, QuestWindowView window, QuestDefinition selected)
        {
            if (this == null || player == null || window == null || !CanInteract(actor)) return;
            window.Show(selected, () => player.Request(this, selected, false), () => player.Request(this, selected, true));
            var state = player.GetState(selected);
            window.Refresh(state.Status, state.Kills);
        }
        private void OnDrawGizmosSelected()
        { Gizmos.color = new Color(.3f, 1f, .6f, .5f); Gizmos.DrawWireSphere(transform.position, interactionRange); }
    }
}
