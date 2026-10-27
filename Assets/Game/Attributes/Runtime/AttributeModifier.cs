using System;

namespace Game.Attributes
{
    public readonly struct AttributeModifier
    {
        public string SourceId { get; }
        public AttributeType Attribute { get; }
        public AttributeModifierOperation Operation { get; }
        public float Value { get; }
        public int Priority { get; }

        public AttributeModifier(string sourceId, AttributeType attribute, AttributeModifierOperation operation,
            float value, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source instance ID is required.", nameof(sourceId));
            if (!Enum.IsDefined(typeof(AttributeType), attribute)) throw new ArgumentOutOfRangeException(nameof(attribute));
            if (!Enum.IsDefined(typeof(AttributeModifierOperation), operation)) throw new ArgumentOutOfRangeException(nameof(operation));
            if (float.IsNaN(value) || float.IsInfinity(value) ||
                ((operation == AttributeModifierOperation.Multiply || operation == AttributeModifierOperation.Override) && value < 0f))
                throw new ArgumentOutOfRangeException(nameof(value));
            SourceId = sourceId;
            Attribute = attribute;
            Operation = operation;
            Value = value;
            Priority = priority;
        }
    }
}
