using System;
using System.Collections.Generic;

namespace Game.Attributes
{
    public sealed class AttributeSet : IAttributeService
    {
        private readonly struct Entry
        {
            public readonly Guid Handle;
            public readonly AttributeModifier Modifier;
            public Entry(Guid handle, AttributeModifier modifier) { Handle = handle; Modifier = modifier; }
        }

        private AttributeValues _baseValues;
        private readonly List<Entry> _modifiers = new List<Entry>();
        private AttributeSnapshot _snapshot;

        public bool IsReady => true;
        public AttributeSnapshot Snapshot => _snapshot;
        public event Action<AttributeSnapshot> Changed;
        public event Action Died;

        public AttributeSet(AttributeValues values)
        {
            ValidateValues(values);
            _baseValues = values;
            _snapshot = new AttributeSnapshot(values, values.MaxHealth, values.MaxStamina, 0);
        }

        public float GetBaseValue(AttributeType type) => _baseValues.Get(type);
        public float GetValue(AttributeType type) => _snapshot.Values.Get(type);

        public void SetBaseValues(AttributeValues values)
        {
            ValidateValues(values);
            if (_baseValues.Equals(values)) return;
            _baseValues = values;
            Recalculate();
        }

        public Guid AddModifier(AttributeModifier modifier)
        {
            return AddModifiers(new[] { modifier })[0];
        }

        public Guid[] AddModifiers(IReadOnlyList<AttributeModifier> modifiers)
        {
            Entry[] entries = PrepareEntries(modifiers, null);
            if (entries.Length == 0) return Array.Empty<Guid>();
            _modifiers.AddRange(entries);
            Recalculate();
            return GetHandles(entries);
        }

        public Guid[] ReplaceModifiersFromSource(string sourceId, IReadOnlyList<AttributeModifier> modifiers)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source instance ID is required.", nameof(sourceId));
            Entry[] entries = PrepareEntries(modifiers, sourceId);
            int removed = _modifiers.RemoveAll(entry => entry.Modifier.SourceId == sourceId);
            _modifiers.AddRange(entries);
            if (removed > 0 || entries.Length > 0) Recalculate();
            return GetHandles(entries);
        }

        public bool RemoveModifier(Guid handle)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Handle != handle) continue;
                _modifiers.RemoveAt(i);
                Recalculate();
                return true;
            }
            return false;
        }

        public int RemoveModifiersFromSource(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("A source instance ID is required.", nameof(sourceId));
            int removed = _modifiers.RemoveAll(entry => entry.Modifier.SourceId == sourceId);
            if (removed > 0) Recalculate();
            return removed;
        }

        public float ApplyDamage(float amount)
        {
            AttributeValues.Validate(amount, 0f, nameof(amount));
            float applied = Math.Min(amount, _snapshot.Health);
            if (applied == 0f) return 0f;
            bool died = applied == _snapshot.Health;
            Commit(_snapshot.Values, _snapshot.Health - applied, _snapshot.Stamina);
            if (died) Died?.Invoke();
            return applied;
        }

        public float Heal(float amount)
        {
            AttributeValues.Validate(amount, 0f, nameof(amount));
            if (_snapshot.IsDead) return 0f;
            float applied = Math.Min(amount, _snapshot.Values.MaxHealth - _snapshot.Health);
            if (applied > 0f) Commit(_snapshot.Values, _snapshot.Health + applied, _snapshot.Stamina);
            return applied;
        }

        public bool TryConsumeStamina(float amount)
        {
            AttributeValues.Validate(amount, 0f, nameof(amount));
            if (_snapshot.IsDead || _snapshot.Stamina < amount) return false;
            if (amount > 0f) Commit(_snapshot.Values, _snapshot.Health, _snapshot.Stamina - amount);
            return true;
        }

        public float RestoreStamina(float amount)
        {
            AttributeValues.Validate(amount, 0f, nameof(amount));
            if (_snapshot.IsDead) return 0f;
            float applied = Math.Min(amount, _snapshot.Values.MaxStamina - _snapshot.Stamina);
            if (applied > 0f) Commit(_snapshot.Values, _snapshot.Health, _snapshot.Stamina + applied);
            return applied;
        }

        public bool Revive(float health)
        {
            AttributeValues.Validate(health, 0f, nameof(health));
            if (!_snapshot.IsDead || health <= 0f) return false;
            Commit(_snapshot.Values, Math.Min(health, _snapshot.Values.MaxHealth), _snapshot.Stamina);
            return true;
        }

        private void Recalculate()
        {
            var values = new AttributeValues(Calculate(AttributeType.MaxHealth, 1f),
                Calculate(AttributeType.MaxStamina, 0f), Calculate(AttributeType.AttackPower, 0f),
                Calculate(AttributeType.Defense, 0f), Calculate(AttributeType.MoveSpeed, 0f));
            Commit(values, Math.Min(_snapshot.Health, values.MaxHealth), Math.Min(_snapshot.Stamina, values.MaxStamina));
        }

        private float Calculate(AttributeType type, float minimum)
        {
            double flat = 0, percentage = 0;
            bool hasOverride = false;
            int overridePriority = int.MinValue;
            float overrideValue = 0;
            foreach (Entry entry in _modifiers)
            {
                AttributeModifier modifier = entry.Modifier;
                if (modifier.Attribute != type) continue;
                switch (modifier.Operation)
                {
                    case AttributeModifierOperation.Flat: flat += modifier.Value; break;
                    case AttributeModifierOperation.AdditivePercent: percentage += modifier.Value; break;
                    case AttributeModifierOperation.Override:
                        if (!hasOverride || modifier.Priority >= overridePriority)
                        {
                            hasOverride = true;
                            overridePriority = modifier.Priority;
                            overrideValue = modifier.Value;
                        }
                        break;
                }
            }
            if (hasOverride) return Math.Max(minimum, overrideValue);
            double value = Math.Max(0d, _baseValues.Get(type) + flat) * Math.Max(0d, 1d + percentage);
            if (value <= 0d) return minimum;
            double logValue = Math.Log(value);
            foreach (Entry entry in _modifiers)
                if (entry.Modifier.Attribute == type && entry.Modifier.Operation == AttributeModifierOperation.Multiply)
                {
                    if (entry.Modifier.Value == 0f) return minimum;
                    logValue += Math.Log(entry.Modifier.Value);
                }
            value = logValue >= Math.Log(float.MaxValue) ? float.MaxValue : Math.Exp(logValue);
            return (float)Math.Max(minimum, value);
        }

        private static Entry[] PrepareEntries(IReadOnlyList<AttributeModifier> modifiers, string requiredSource)
        {
            if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
            var entries = new Entry[modifiers.Count];
            for (int i = 0; i < modifiers.Count; i++)
            {
                AttributeModifier modifier = modifiers[i];
                modifier = new AttributeModifier(modifier.SourceId, modifier.Attribute, modifier.Operation,
                    modifier.Value, modifier.Priority);
                if (requiredSource != null && modifier.SourceId != requiredSource)
                    throw new ArgumentException("Replacement modifiers must belong to the specified source.", nameof(modifiers));
                entries[i] = new Entry(Guid.NewGuid(), modifier);
            }
            return entries;
        }

        private static Guid[] GetHandles(Entry[] entries)
        {
            var handles = new Guid[entries.Length];
            for (int i = 0; i < entries.Length; i++) handles[i] = entries[i].Handle;
            return handles;
        }

        private void Commit(AttributeValues values, float health, float stamina)
        {
            var next = new AttributeSnapshot(values, health, stamina, _snapshot.Revision);
            if (next.Equals(_snapshot)) return;
            _snapshot = new AttributeSnapshot(values, health, stamina, unchecked(_snapshot.Revision + 1));
            Changed?.Invoke(_snapshot);
        }

        private static void ValidateValues(AttributeValues values)
        {
            if (values.MaxHealth < 1f) throw new ArgumentException("MaxHealth must be at least one.", nameof(values));
        }
    }
}
