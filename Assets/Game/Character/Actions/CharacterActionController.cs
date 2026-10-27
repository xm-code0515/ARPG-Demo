using Game.Attributes;
using Game.Attributes.Unity;
using Game.Combat.Unity;
using UnityEngine;

namespace Game.Character.Actions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMotor), typeof(CharacterAttributes))]
    public sealed class CharacterActionController : MonoBehaviour
    {
        [SerializeField] private CharacterActionDefinition definition;

        private CharacterMotor _motor;
        private CharacterAttributes _attributes;
        private CharacterCombatController _combat;
        private CharacterActionSettings _settings;
        private CharacterActionState _state;
        private bool _prediction;

        public bool IsConfigured => definition != null;
        public bool IsReady { get; private set; }
        public CharacterActionState State => new CharacterActionState(_state.Stamina, _state.MaxStamina, _state.MoveSpeed,
            _state.RecoveryDelayRemaining, _state.SprintExhausted, _state.IsDead,
            _combat != null ? _combat.State : _state.Combat);

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _attributes = GetComponent<CharacterAttributes>();
            _combat = GetComponent<CharacterCombatController>();
            if (definition != null) _settings = definition.CreateSettings();
        }

        public void InitializeAuthority()
        {
            if (!IsConfigured || !_attributes.TryGetAuthority(out _))
                throw new System.InvalidOperationException("Character actions require a definition and authoritative attributes.");
            _settings = definition.CreateSettings();
            AttributeSnapshot snapshot = _attributes.Snapshot;
            _state = new CharacterActionState(snapshot.Stamina, snapshot.Values.MaxStamina,
                snapshot.Values.MoveSpeed, 0f, false, snapshot.IsDead);
            _prediction = false;
            if (_combat != null) _combat.RestoreState(default);
            IsReady = true;
        }

        public void ConfigureDefinition(CharacterActionDefinition value)
        {
            if (value == null || IsReady) throw new System.InvalidOperationException("Configure actions before simulation starts.");
            definition = value;
            _settings = value.CreateSettings();
        }

        public void RestorePrediction(CharacterActionState state)
        {
            if (!IsConfigured) throw new System.InvalidOperationException("Character action definition is missing.");
            _settings = definition.CreateSettings();
            _state = state;
            if (_combat != null) _combat.RestoreState(state.Combat);
            _prediction = true;
            IsReady = true;
        }

        public void PublishPrediction()
        {
            if (_prediction && IsReady) _attributes.SetPredictedStamina(_state.Stamina, _state.MaxStamina);
        }

        public void ResetActions()
        {
            IsReady = false;
            _state = default;
            if (_combat != null) _combat.RestoreState(default);
            _prediction = false;
            _attributes.ClearPredictedStamina();
        }

        public void Simulate(CharacterMoveCommand command, float deltaTime)
        {
            if (!IsReady) return;
            IAttributeService service = null;
            if (!_prediction)
            {
                if (!_attributes.TryGetAuthority(out service)) return;
                AttributeSnapshot snapshot = service.Snapshot;
                float delay = _state.RecoveryDelayRemaining;
                if (snapshot.Stamina < _state.Stamina - .0001f) delay = _settings.RecoveryDelay;
                _state = new CharacterActionState(snapshot.Stamina, snapshot.Values.MaxStamina,
                    snapshot.Values.MoveSpeed, delay, _state.SprintExhausted, snapshot.IsDead,
                    _combat != null ? _combat.State : _state.Combat);
            }
            bool hitStopped = !_state.IsDead && _combat != null && _combat.State.HitStopRemaining > 0f;
            bool knockedBack = _motor.IsKnockedBack;
            bool attackActiveThisStep = false;
            Vector3 attackMovement = Vector3.zero;
            if (_combat != null && _combat.IsConfigured)
            {
                bool wasAttacking = _combat.State.Attacking;
                var step = _combat.Step(command, _state.Stamina, _state.IsDead, deltaTime);
                attackActiveThisStep = !_state.IsDead && (wasAttacking || step.State.Attacking);
                attackMovement = new Vector3(step.RightMovement, 0, step.ForwardMovement);
                _state = new CharacterActionState(_state.Stamina - step.Cost, _state.MaxStamina, _state.MoveSpeed,
                    step.Cost > 0f ? _settings.RecoveryDelay : _state.RecoveryDelayRemaining,
                    _state.SprintExhausted, _state.IsDead, step.State);
                if (!hitStopped && !knockedBack && attackActiveThisStep && command.LookDirection.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.LookRotation(command.LookDirection);
            }
            bool dodging = _combat != null && _combat.DodgeActiveThisStep;
            if (dodging && !hitStopped && !knockedBack)
            {
                var dodge = _combat.State;
                transform.rotation = Quaternion.LookRotation(new Vector3(dodge.DodgeDirectionX, 0f, dodge.DodgeDirectionZ));
            }
            bool movementLocked = attackActiveThisStep || dodging || hitStopped || knockedBack;
            CharacterActionDecision decision = CharacterActionPolicy.BeginStep(_settings, _state, command.JumpPressed && !movementLocked,
                command.SprintHeld && !movementLocked, command.MoveDirection.sqrMagnitude > .01f && !movementLocked, _motor.IsGrounded, deltaTime);
            CharacterHeldButtons held = decision.CanMove ? command.HeldButtons : CharacterHeldButtons.None;
            CharacterPressedButtons pressed = decision.CanMove ? command.PressedButtons : CharacterPressedButtons.None;
            if (!decision.Sprint) held &= ~CharacterHeldButtons.Sprint;
            if (!decision.Jump) pressed &= ~CharacterPressedButtons.Jump;
            var allowed = new CharacterMoveCommand(decision.CanMove && !movementLocked ? command.MoveDirection : Vector3.zero, held, pressed);
            if (hitStopped) _motor.PauseMovement();
            else _motor.Simulate(allowed, deltaTime, decision.JumpHeight, _state.MoveSpeed, _settings.SprintMultiplier,
                decision.CanMove && !knockedBack ? (dodging ? _combat.DodgeMovement :
                    _motor.IsGrounded ? transform.rotation * attackMovement : Vector3.zero) : Vector3.zero);
            _state = CharacterActionPolicy.CompleteStep(_settings, decision,
                _motor.CurrentMovementState.IsSprinting && _motor.CurrentMovementState.HorizontalSpeed > .05f, deltaTime);
            if (service != null)
            {
                float difference = _state.Stamina - service.Snapshot.Stamina;
                if (difference < 0f) service.TryConsumeStamina(-difference);
                else if (difference > 0f) service.RestoreStamina(difference);
                if (_combat != null) _combat.ResolveAuthorityHit();
            }
        }
    }
}
