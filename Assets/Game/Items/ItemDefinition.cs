using UnityEngine;

namespace Game.Items
{
    public enum ItemCategory { Material, Weapon, Armor, Food }

    [CreateAssetMenu(menuName = "Game/Items/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ItemCategory category;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject worldAppearance;
        [SerializeField, Range(1, 999)] private int stackLimit = 20;
        [SerializeField] private ItemDisplayProperty[] displayProperties = System.Array.Empty<ItemDisplayProperty>();
        [SerializeField] private Game.Combat.Unity.WeaponDefinition weapon;
        [SerializeField] private FoodDefinition food;
        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemCategory Category => category;
        public Sprite Icon => icon;
        public GameObject WorldAppearance => worldAppearance;
        public int StackLimit => Mathf.Max(1, stackLimit);
        public System.Collections.Generic.IReadOnlyList<ItemDisplayProperty> DisplayProperties => displayProperties;
        public Game.Combat.Unity.WeaponDefinition Weapon => weapon;
        public FoodDefinition Food => food;
    }

    [System.Serializable]
    public struct ItemDisplayProperty
    {
        public string name;
        public string value;
    }
}
