using UnityEngine;

namespace Game.Items
{
    [CreateAssetMenu(menuName = "Game/Items/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items;
        public int Count => items != null ? items.Length : 0;
        public ItemDefinition Get(int index) => index >= 0 && index < Count ? items[index] : null;
        public int IndexOf(ItemDefinition item) => items != null ? System.Array.IndexOf(items, item) : -1;
    }
}
