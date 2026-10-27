using UnityEngine;

namespace Game.Presentation.World
{
    public interface IWorldInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(GameObject actor);
        void Interact(GameObject actor);
    }
}
