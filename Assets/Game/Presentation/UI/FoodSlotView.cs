using Game.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    public sealed class FoodSlotView : MonoBehaviour
    {
        [SerializeField] private Image icon, remainingFill;
        [SerializeField] private Text nameText, timeText;
        public void Refresh(ItemDefinition item, double remaining)
        {
            if (icon != null) { icon.sprite = item != null ? item.Icon : null; icon.enabled = icon.sprite != null; }
            if (nameText != null) nameText.text = item != null ? item.DisplayName : "";
            if (timeText != null)
            {
                int seconds = (int)System.Math.Ceiling(remaining);
                timeText.text = item != null ? (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") : "";
            }
            if (remainingFill != null) remainingFill.fillAmount = item != null && item.Food != null
                ? Mathf.Clamp01((float)(remaining / item.Food.Duration)) : 0;
        }
    }
}
