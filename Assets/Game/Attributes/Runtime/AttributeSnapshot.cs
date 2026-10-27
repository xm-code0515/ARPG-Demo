using System;

namespace Game.Attributes
{
    public readonly struct AttributeSnapshot : IEquatable<AttributeSnapshot>
    {
        public AttributeValues Values { get; }
        public float Health { get; }
        public float Stamina { get; }
        public bool HasStaminaState { get; }
        public bool IsDead => Health <= 0f;
        public uint Revision { get; }

        public AttributeSnapshot(AttributeValues values, float health, float stamina, uint revision,
            bool hasStaminaState = true)
        {
            if (values.MaxHealth < 1f) throw new ArgumentException("MaxHealth must be at least one.", nameof(values));
            Values = values;
            Health = Math.Min(values.MaxHealth, AttributeValues.Validate(health, 0f, nameof(health)));
            Stamina = Math.Min(values.MaxStamina, AttributeValues.Validate(stamina, 0f, nameof(stamina)));
            Revision = revision;
            HasStaminaState = hasStaminaState;
        }

        public bool Equals(AttributeSnapshot other) => Values.Equals(other.Values) && Health.Equals(other.Health) &&
            Stamina.Equals(other.Stamina) && HasStaminaState == other.HasStaminaState && Revision == other.Revision;
        public override bool Equals(object obj) => obj is AttributeSnapshot other && Equals(other);
        public override int GetHashCode() => Values.GetHashCode() ^ Revision.GetHashCode();
    }
}
