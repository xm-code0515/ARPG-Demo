using UnityEngine;

namespace Game.Enemies.Unity
{
    [CreateAssetMenu(menuName = "Game/Enemies/Enemy Catalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [SerializeField] private EnemyDefinition[] definitions;
        public System.Collections.Generic.IReadOnlyList<EnemyDefinition> Definitions => definitions ?? System.Array.Empty<EnemyDefinition>();
        public EnemyDefinition Get(int index) => definitions != null && index >= 0 && index < definitions.Length ? definitions[index] : null;
        public int IndexOf(EnemyDefinition definition) => definitions == null ? -1 : System.Array.IndexOf(definitions, definition);
    }
}
