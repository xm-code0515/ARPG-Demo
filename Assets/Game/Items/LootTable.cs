using UnityEngine;

namespace Game.Items
{
    [System.Serializable]
    public sealed class LootEntry
    {
        public ItemDefinition item;
        [Range(0f, 1f)] public float chance = 1f;
        [Min(1)] public int minimum = 1;
        [Min(1)] public int maximum = 3;
    }

    [CreateAssetMenu(menuName = "Game/Items/Loot Table")]
    public sealed class LootTable : ScriptableObject
    {
        [SerializeField] private LootEntry[] entries;
        public System.Collections.Generic.IReadOnlyList<LootEntry> Entries => entries ?? System.Array.Empty<LootEntry>();
        public void Roll(System.Action<ItemDefinition, int> spawn)
        {
            if (entries == null || spawn == null) return;
            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null || entry.chance <= 0f || (entry.chance < 1f && Random.value >= entry.chance)) continue;
                int min = Mathf.Clamp(entry.minimum, 1, 999);
                int max = Mathf.Clamp(entry.maximum, min, 999);
                spawn(entry.item, Random.Range(min, max + 1));
            }
        }
    }
}
