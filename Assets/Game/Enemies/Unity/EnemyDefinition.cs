using Game.Attributes.Unity;
using Game.Character.Actions;
using Game.Combat.Unity;
using UnityEngine;
using Game.Items;

namespace Game.Enemies.Unity
{
    [CreateAssetMenu(menuName = "Game/Enemies/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string enemyId = "skeleton";
        [SerializeField] private string displayName = "骷髅";
        [SerializeField] private GameObject prefab;
        [SerializeField] private CharacterAttributeDefinition attributes;
        [SerializeField] private CharacterActionDefinition actions;
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private EnemyAIProfile ai;
        [SerializeField] private LootTable loot;
        [SerializeField] private CharacterTeam team = CharacterTeam.Enemy;
        [SerializeField, Min(1f)] private float corpseLifetime = 20f;
        public string EnemyId => enemyId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public CharacterAttributeDefinition Attributes => attributes;
        public CharacterActionDefinition Actions => actions;
        public WeaponDefinition Weapon => weapon;
        public EnemyAIProfile AI => ai;
        public LootTable Loot => loot;
        public CharacterTeam Team => team;
        public float CorpseLifetime => corpseLifetime;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(enemyId) && prefab != null && attributes != null && actions != null && weapon != null;
    }
}
