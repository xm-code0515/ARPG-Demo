using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Presentation.UI
{
    public sealed class InventoryOutsideDropArea : MonoBehaviour, IPointerClickHandler, IDropHandler
    {
        [SerializeField] private InventoryHud hud;
        public void OnPointerClick(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) hud.OutsideClick(); }
        public void OnDrop(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) hud.DropOutside(); }
    }
}
