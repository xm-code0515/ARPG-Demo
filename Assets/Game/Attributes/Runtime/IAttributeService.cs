using System;
using System.Collections.Generic;

namespace Game.Attributes
{
    public interface IAttributeReadModel
    {
        bool IsReady { get; }
        AttributeSnapshot Snapshot { get; }
        event Action<AttributeSnapshot> Changed;
    }

    public interface IAttributeService : IAttributeReadModel
    {
        event Action Died;
        float GetBaseValue(AttributeType type);
        float GetValue(AttributeType type);
        void SetBaseValues(AttributeValues values);
        Guid AddModifier(AttributeModifier modifier);
        Guid[] AddModifiers(IReadOnlyList<AttributeModifier> modifiers);
        Guid[] ReplaceModifiersFromSource(string sourceId, IReadOnlyList<AttributeModifier> modifiers);
        bool RemoveModifier(Guid handle);
        int RemoveModifiersFromSource(string sourceId);
        float ApplyDamage(float amount);
        float Heal(float amount);
        bool TryConsumeStamina(float amount);
        float RestoreStamina(float amount);
        bool Revive(float health);
    }
}
