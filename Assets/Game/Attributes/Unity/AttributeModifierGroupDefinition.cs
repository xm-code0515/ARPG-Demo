using System;
using Game.Attributes;
using UnityEngine;

namespace Game.Attributes.Unity
{
    [CreateAssetMenu(menuName = "Game/Attributes/Modifier Group", fileName = "AttributeModifierGroup")]
    public sealed class AttributeModifierGroupDefinition : ScriptableObject
    {
        [SerializeField] private AttributeModifierDefinition[] modifiers = Array.Empty<AttributeModifierDefinition>();

        public AttributeModifier[] CreateModifiers(string sourceInstanceId)
        {
            var result = new AttributeModifier[modifiers.Length];
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i] == null) throw new InvalidOperationException("Modifier entries cannot be empty.");
                result[i] = modifiers[i].CreateModifier(sourceInstanceId);
            }
            return result;
        }
    }
}
