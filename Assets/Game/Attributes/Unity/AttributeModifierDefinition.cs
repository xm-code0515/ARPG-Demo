using System;
using Game.Attributes;
using UnityEngine;

namespace Game.Attributes.Unity
{
    [Serializable]
    public sealed class AttributeModifierDefinition
    {
        [SerializeField] private AttributeType attribute;
        [SerializeField] private AttributeModifierOperation operation;
        [SerializeField] private float value;
        [SerializeField] private int priority;

        public AttributeModifier CreateModifier(string sourceInstanceId) =>
            new AttributeModifier(sourceInstanceId, attribute, operation, value, priority);
    }

}
