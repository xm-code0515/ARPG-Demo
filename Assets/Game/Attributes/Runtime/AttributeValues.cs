using System;

namespace Game.Attributes
{
    public readonly struct AttributeValues : IEquatable<AttributeValues>
    {
        public float MaxHealth { get; }
        public float MaxStamina { get; }
        public float AttackPower { get; }
        public float Defense { get; }
        public float MoveSpeed { get; }

        public AttributeValues(float maxHealth, float maxStamina, float attackPower, float defense, float moveSpeed)
        {
            MaxHealth = Validate(maxHealth, 1f, nameof(maxHealth));
            MaxStamina = Validate(maxStamina, 0f, nameof(maxStamina));
            AttackPower = Validate(attackPower, 0f, nameof(attackPower));
            Defense = Validate(defense, 0f, nameof(defense));
            MoveSpeed = Validate(moveSpeed, 0f, nameof(moveSpeed));
        }

        public float Get(AttributeType type)
        {
            switch (type)
            {
                case AttributeType.MaxHealth: return MaxHealth;
                case AttributeType.MaxStamina: return MaxStamina;
                case AttributeType.AttackPower: return AttackPower;
                case AttributeType.Defense: return Defense;
                case AttributeType.MoveSpeed: return MoveSpeed;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public bool Equals(AttributeValues other) => MaxHealth.Equals(other.MaxHealth) &&
            MaxStamina.Equals(other.MaxStamina) && AttackPower.Equals(other.AttackPower) &&
            Defense.Equals(other.Defense) && MoveSpeed.Equals(other.MoveSpeed);
        public override bool Equals(object obj) => obj is AttributeValues other && Equals(other);
        public override int GetHashCode() => MaxHealth.GetHashCode() ^ AttackPower.GetHashCode();

        internal static float Validate(float value, float minimum, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum)
                throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }
}
