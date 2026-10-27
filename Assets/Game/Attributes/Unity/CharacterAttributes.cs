using System;
using Game.Attributes;
using UnityEngine;

namespace Game.Attributes.Unity
{
    [DefaultExecutionOrder(-1200)]
    [DisallowMultipleComponent]
    public sealed class CharacterAttributes : MonoBehaviour, IAttributeReadModel
    {
        [SerializeField] private CharacterAttributeDefinition definition;
        [SerializeField] private AttributeModifierGroupDefinition developmentModifierGroup;

        private AttributeSet _runtime;
        private AttributeSnapshot _replica;
        private bool _hasReplica;
        private bool _hasAuthority;
        private bool _hasPredictedStamina;
        private float _predictedStamina;
        private float _predictedMaxStamina;
        private readonly string _developmentSource = "development:" + Guid.NewGuid().ToString("N");

        public bool IsReady => _hasAuthority ? _runtime != null : _hasReplica;
        public bool HasAuthority => _hasAuthority && _runtime != null;
        public AttributeSnapshot Snapshot
        {
            get
            {
                if (HasAuthority) return _runtime.Snapshot;
                if (!_hasReplica || !_hasPredictedStamina) return _replica;
                AttributeValues values = _replica.Values;
                return new AttributeSnapshot(new AttributeValues(values.MaxHealth, _predictedMaxStamina,
                    values.AttackPower, values.Defense, values.MoveSpeed), _replica.Health, _predictedStamina,
                    _replica.Revision, true);
            }
        }
        public event Action<AttributeSnapshot> Changed;
        public event Action Died;

        private void Awake()
        {
            if (definition != null) InitializeAuthority();
        }

        private void OnDestroy() => DetachRuntime();

        public void ConfigureDefinition(CharacterAttributeDefinition value)
        {
            if (value == null || HasAuthority) throw new InvalidOperationException("Configure attributes before initializing authority.");
            definition = value;
        }

        public bool InitializeAuthority()
        {
            DetachRuntime();
            _hasReplica = false;
            _hasPredictedStamina = false;
            _replica = default;
            _hasAuthority = false;
            if (definition == null)
            {
                Debug.LogError("[CharacterAttributes] 请配置 Character Attribute Definition。", this);
                Changed?.Invoke(default);
                return false;
            }
            _runtime = new AttributeSet(definition.CreateValues());
            _runtime.Changed += HandleRuntimeChanged;
            _runtime.Died += HandleDeath;
            _hasAuthority = true;
            Changed?.Invoke(_runtime.Snapshot);
            return true;
        }

        public void BeginReplica()
        {
            DetachRuntime();
            _hasAuthority = false;
            _hasReplica = false;
            _hasPredictedStamina = false;
            _replica = default;
            Changed?.Invoke(default);
        }

        public void ApplyReplica(AttributeSnapshot snapshot)
        {
            if (_hasAuthority) throw new InvalidOperationException("Cannot apply a replica to the authority.");
            if (_hasReplica && _replica.Equals(snapshot)) return;
            _replica = snapshot;
            _hasReplica = true;
            Changed?.Invoke(Snapshot);
        }

        public void SetPredictedStamina(float stamina, float maximum)
        {
            if (_hasAuthority) return;
            if (_hasPredictedStamina && _predictedStamina.Equals(stamina) && _predictedMaxStamina.Equals(maximum)) return;
            _hasPredictedStamina = true;
            _predictedStamina = stamina;
            _predictedMaxStamina = maximum;
            if (_hasReplica) Changed?.Invoke(Snapshot);
        }

        public void ClearPredictedStamina()
        {
            if (!_hasPredictedStamina) return;
            _hasPredictedStamina = false;
            if (_hasReplica) Changed?.Invoke(Snapshot);
        }

        public bool TryGetAuthority(out IAttributeService service)
        {
            service = HasAuthority ? _runtime : null;
            return service != null;
        }

        private void HandleRuntimeChanged(AttributeSnapshot snapshot) => Changed?.Invoke(snapshot);
        private void HandleDeath() => Died?.Invoke();

        private void DetachRuntime()
        {
            if (_runtime == null) return;
            _runtime.Changed -= HandleRuntimeChanged;
            _runtime.Died -= HandleDeath;
            _runtime = null;
        }

        private bool TryGetDevelopmentAuthority(out IAttributeService service)
        {
            service = null;
            if (!Application.isPlaying || !Debug.isDebugBuild) return false;
            if (TryGetAuthority(out service)) return true;
            Debug.LogWarning("[CharacterAttributes] 测试操作只允许在单机角色或 Host / Server 的角色上执行。", this);
            return false;
        }

        [ContextMenu("Development/Damage 10")]
        public void DevelopmentDamage()
        {
            if (TryGetDevelopmentAuthority(out IAttributeService service)) service.ApplyDamage(10f);
        }

        [ContextMenu("Development/Heal 10")]
        public void DevelopmentHeal()
        {
            if (TryGetDevelopmentAuthority(out IAttributeService service)) service.Heal(10f);
        }

        [ContextMenu("Development/Consume Stamina 15")]
        public void DevelopmentConsumeStamina()
        {
            if (TryGetDevelopmentAuthority(out IAttributeService service)) service.TryConsumeStamina(15f);
        }

        [ContextMenu("Development/Restore Stamina 15")]
        public void DevelopmentRestoreStamina()
        {
            if (TryGetDevelopmentAuthority(out IAttributeService service)) service.RestoreStamina(15f);
        }

        [ContextMenu("Development/Apply Modifier Group")]
        public void DevelopmentApplyModifiers()
        {
            if (!TryGetDevelopmentAuthority(out IAttributeService service) || developmentModifierGroup == null) return;
            AttributeModifier[] modifiers = developmentModifierGroup.CreateModifiers(_developmentSource);
            service.ReplaceModifiersFromSource(_developmentSource, modifiers);
        }

        [ContextMenu("Development/Remove Modifier Group")]
        public void DevelopmentRemoveModifiers()
        {
            if (TryGetDevelopmentAuthority(out IAttributeService service)) service.RemoveModifiersFromSource(_developmentSource);
        }

        [ContextMenu("Development/Revive")]
        public void DevelopmentRevive()
        {
            if (TryGetDevelopmentAuthority(out IAttributeService service)) service.Revive(service.GetValue(AttributeType.MaxHealth));
        }
    }
}
