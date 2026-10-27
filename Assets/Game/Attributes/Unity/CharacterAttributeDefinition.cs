using Game.Attributes;
using UnityEngine;

namespace Game.Attributes.Unity
{
    [CreateAssetMenu(menuName = "Game/Attributes/Character Attributes", fileName = "CharacterAttributes")]
    public sealed class CharacterAttributeDefinition : ScriptableObject
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float attackPower = 20f;
        [SerializeField, Min(0f)] private float defense = 5f;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        public AttributeValues CreateValues() => new AttributeValues(maxHealth, maxStamina, attackPower, defense, moveSpeed);
    }
}
