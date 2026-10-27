using Game.Attributes.Unity;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Presentation.World
{
    [RequireComponent(typeof(NpcFacing))]
    public sealed class NpcDialogueInteractable : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] private string displayName = "NPC";
        [SerializeField, TextArea(2, 6)] private string[] dialogueLines;
        [SerializeField, Min(.5f)] private float interactionRange = 2.2f;
        [SerializeField] private UnityEvent onDialogueOpened;
        public string InteractionPrompt => "[E] 对话 " + displayName;
        public bool CanInteract(GameObject actor)
        {
            if (!isActiveAndEnabled || dialogueLines == null || dialogueLines.Length == 0 || actor == null || actor.scene != gameObject.scene ||
                Vector3.Distance(actor.transform.position, transform.position) > interactionRange) return false;
            var attributes = actor.GetComponent<CharacterAttributes>();
            if (attributes == null || !attributes.IsReady || attributes.Snapshot.IsDead) return false;
            Vector3 start = actor.transform.position + Vector3.up;
            Vector3 end = transform.position + Vector3.up;
            foreach (var hit in Physics.RaycastAll(start, (end - start).normalized, Vector3.Distance(start, end), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(actor.transform) && !hit.collider.transform.IsChildOf(transform)) return false;
            return true;
        }
        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            var view = FindAnyObjectByType<NpcDialogueView>();
            if (view == null) return;
            GetComponent<NpcFacing>().Face(actor.transform);
            view.Show(displayName, dialogueLines);
            onDialogueOpened?.Invoke();
        }
    }
}
