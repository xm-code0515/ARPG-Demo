using System;
using Game.Attributes;
using Game.Attributes.Unity;
using UnityEngine;

namespace Game.Items
{
    [CreateAssetMenu(menuName = "Game/Items/Food Effect", fileName = "FoodEffect")]
    public sealed class FoodDefinition : ScriptableObject
    {
        [SerializeField, Min(1f)] private float duration = 300f;
        [SerializeField, Min(0f)] private float healthRecoveryPerSecond = 1f;
        [SerializeField, Min(0f)] private float staminaRecoveryPerSecond;
        [SerializeField] private AttributeModifierGroupDefinition bonuses;
        public float Duration => Mathf.Max(1f, duration);
        public float HealthRecoveryPerSecond => Mathf.Max(0f, healthRecoveryPerSecond);
        public float StaminaRecoveryPerSecond => Mathf.Max(0f, staminaRecoveryPerSecond);
        public AttributeModifier[] CreateModifiers(string source)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0 ||
                float.IsNaN(healthRecoveryPerSecond) || float.IsInfinity(healthRecoveryPerSecond) || healthRecoveryPerSecond < 0 ||
                float.IsNaN(staminaRecoveryPerSecond) || float.IsInfinity(staminaRecoveryPerSecond) || staminaRecoveryPerSecond < 0)
                throw new InvalidOperationException("Food duration and recovery must be finite and nonnegative; duration must be positive.");
            return bonuses != null ? bonuses.CreateModifiers(source) : Array.Empty<AttributeModifier>();
        }
    }

    public readonly struct ActiveFood
    {
        public readonly int ItemIndex;
        public readonly double EndsAt;
        public ActiveFood(int itemIndex, double endsAt) { ItemIndex = itemIndex; EndsAt = endsAt; }
        public bool IsEmpty => ItemIndex < 0;
    }

    public interface IActiveFoodReadModel
    {
        bool IsReady { get; }
        int FoodSlotCount { get; }
        ItemCatalog Catalog { get; }
        double CurrentTime { get; }
        ActiveFood GetFood(int slot);
        event Action FoodsChanged;
    }
}
