using System.Collections.Generic;
using Game.Attributes.Unity;
using Game.Character;
using UnityEngine;

namespace Game.Combat.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterAttributes), typeof(CharacterMotor))]
    public sealed class CharacterCombatController : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition unarmedWeapon;
        [SerializeField] private bool friendlyFire = true;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Collider damageBody;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private DodgeDefinition dodgeDefinition;
        [Header("Hit Reaction")]
        [SerializeField] private bool reactToHitsWhileMoving;
        [SerializeField] private bool interruptAttackOnHit;
        private WeaponDefinition _equippedWeapon;
        private AttackSettings[] _settings;
        private CharacterAttributes _attributes;
        private CharacterMotor _motor;
        private bool _pendingHit;
        private readonly HashSet<CharacterCombatController> _hitTargets = new HashSet<CharacterCombatController>();
        private readonly List<CharacterCombatController> _candidates = new List<CharacterCombatController>();
        private static readonly HashSet<CharacterCombatController> ActiveCharacters = new HashSet<CharacterCombatController>();

        public WeaponDefinition CurrentWeapon => _equippedWeapon != null ? _equippedWeapon : unarmedWeapon;
        public CombatState State { get; private set; }
        public bool IsConfigured => unarmedWeapon != null;
        public int ResolvedHits { get; private set; }
        public DodgeDefinition Dodge => dodgeDefinition;
        public Vector3 DodgeMovement { get; private set; }
        public bool DodgeActiveThisStep { get; private set; }
        public event System.Action<CharacterCombatController, float> DamageReceived;

        private void Awake()
        {
            _attributes = GetComponent<CharacterAttributes>();
            _motor = GetComponent<CharacterMotor>();
            if (IsConfigured) _settings = CurrentWeapon.CreateSettings();
        }
        private void OnEnable() => ActiveCharacters.Add(this);
        private void OnDisable() => ActiveCharacters.Remove(this);

        public bool TryEquip(WeaponDefinition weapon)
        {
            if (!_attributes.HasAuthority || State.Attacking || State.Dodging) return false;
            ApplyWeapon(weapon);
            return true;
        }
        public void ApplyWeapon(WeaponDefinition weapon)
        {
            WeaponDefinition next = weapon != null ? weapon : unarmedWeapon;
            AttackSettings[] settings = next.CreateSettings();
            _equippedWeapon = weapon;
            _settings = settings;
            var previous = State;
            previous.Attacking = previous.Buffered = previous.HitApplied = false;
            previous.AttackIndex = previous.NextAttackIndex = 0;
            previous.Elapsed = 0f;
            State = previous;
            _pendingHit = false;
            DodgeMovement = Vector3.zero;
            DodgeActiveThisStep = false;
        }

        public void ConfigureDefaultWeapon(WeaponDefinition weapon)
        {
            if (weapon == null || State.Attacking) throw new System.InvalidOperationException("Configure an idle character with a valid weapon.");
            unarmedWeapon = weapon;
            _equippedWeapon = null;
            _settings = weapon.CreateSettings();
            RestoreState(default);
        }

        public void RestoreState(CombatState state)
        {
            State = state;
            _pendingHit = false;
            DodgeMovement = Vector3.zero;
            DodgeActiveThisStep = false;
        }

        public bool TryPrepareAttack(int index)
        {
            if (!_attributes.HasAuthority || State.Attacking || State.Dodging || State.HitStopRemaining > 0f ||
                _settings == null || index < 0 || index >= _settings.Length) return false;
            var state = State;
            state.NextAttackIndex = index;
            State = state;
            return true;
        }

        public CombatStep Step(CharacterMoveCommand command, float stamina, bool dead, float deltaTime)
        {
            if (_settings == null) return new CombatStep(State, 0f, false);
            float dodgeCost = StepDodge(command, stamina, dead, deltaTime);
            CombatStep step = CombatRules.Step(State, _settings, command.AttackPressed && !DodgeActiveThisStep, command.AttackHeld && !DodgeActiveThisStep,
                _motor.IsGrounded, dead, stamina - dodgeCost, deltaTime, CurrentWeapon.AttackPattern != null ? CurrentWeapon.AttackPattern.ComboResetDelay : float.PositiveInfinity);
            State = step.State;
            _pendingHit = step.ResolveHit;
            return new CombatStep(State, step.Cost + dodgeCost, step.ResolveHit, step.ForwardMovement, step.RightMovement);
        }

        private float StepDodge(CharacterMoveCommand command, float stamina, bool dead, float deltaTime)
        {
            DodgeMovement = Vector3.zero;
            DodgeActiveThisStep = false;
            var state = State;
            state.DodgeCooldownRemaining = Mathf.Max(0f, state.DodgeCooldownRemaining - deltaTime);
            if (dodgeDefinition == null || dead)
            {
                state.Dodging = false;
                State = state;
                return 0f;
            }
            float cost = 0f;
            if (command.DodgePressed && !state.Dodging && !state.Attacking && state.HitStopRemaining <= 0f &&
                !_motor.IsKnockedBack && _motor.IsGrounded && state.DodgeCooldownRemaining <= 0f && stamina >= dodgeDefinition.StaminaCost)
            {
                Vector3 direction = command.MoveDirection.sqrMagnitude > .01f ? command.MoveDirection.normalized : transform.forward;
                direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                state.Dodging = true;
                state.DodgeRewarded = state.Buffered = false;
                state.DodgeSequence = unchecked(state.DodgeSequence + 1);
                state.DodgeElapsed = 0f;
                state.DodgeCooldownRemaining = dodgeDefinition.Cooldown;
                state.DodgeDirectionX = direction.x;
                state.DodgeDirectionZ = direction.z;
                state.DodgeOriginX = transform.position.x;
                state.DodgeOriginY = transform.position.y;
                state.DodgeOriginZ = transform.position.z;
                cost = dodgeDefinition.StaminaCost;
            }
            DodgeActiveThisStep = state.Dodging;
            if (state.Dodging && state.HitStopRemaining <= 0f)
            {
                float previous = state.DodgeElapsed;
                state.DodgeElapsed = Mathf.Min(dodgeDefinition.Duration, previous + deltaTime);
                DodgeMovement = new Vector3(state.DodgeDirectionX, 0f, state.DodgeDirectionZ) *
                    dodgeDefinition.MovementBetween(previous, state.DodgeElapsed);
                if (state.DodgeElapsed >= dodgeDefinition.Duration - .00001f) state.Dodging = false;
            }
            State = state;
            return cost;
        }

        private bool CanHitPoint(Vector3 origin, Vector3 targetPosition, CharacterCombatController target,
            MeleeAttackDefinition attack, float range)
        {
            // Large creatures can be hit at their body surface without reaching their root.
            Vector3 positionOffset = targetPosition - target.transform.position;
            Vector3 center = target.damageBody != null && target.damageBody.enabled
                ? target.damageBody.ClosestPoint(origin - positionOffset) + positionOffset
                : targetPosition + Vector3.up;
            Vector3 offset = center - origin;
            Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (Mathf.Abs(offset.y) > attack.verticalReach || horizontal.magnitude > range ||
                (horizontal.sqrMagnitude > .001f && Vector3.Angle(transform.forward, horizontal) > attack.angle * .5f)) return false;
            return !Physics.Linecast(origin, center, out RaycastHit obstacle, hitMask, QueryTriggerInteraction.Ignore) ||
                obstacle.collider.GetComponentInParent<CharacterCombatController>() == target;
        }

        public void ResolveAuthorityHit()
        {
            if (!_pendingHit || !_attributes.TryGetAuthority(out var source)) return;
            _pendingHit = false;
            if (source.Snapshot.IsDead) return;
            MeleeAttackDefinition attack = CurrentWeapon.Attacks[State.AttackIndex];
            if (attack.suppressDamage) return;
            Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up;
            _hitTargets.Clear();
            _candidates.Clear();
            float range = CurrentWeapon.AttackRange(State.AttackIndex);
            foreach (Collider collider in Physics.OverlapSphere(origin, range + .5f, hitMask, QueryTriggerInteraction.Ignore))
            {
                CharacterCombatController target = collider.GetComponentInParent<CharacterCombatController>();
                if (target != null && _hitTargets.Add(target)) _candidates.Add(target);
            }
            foreach (var target in ActiveCharacters)
                if (target.State.Dodging && (hitMask.value & (1 << target.gameObject.layer)) != 0 &&
                    _hitTargets.Add(target)) _candidates.Add(target);
            foreach (var target in _candidates)
            {
                if (target == this || !CharacterFaction.CanDamage(this, target, friendlyFire) ||
                    !target._attributes.TryGetAuthority(out var receiver) || receiver.Snapshot.IsDead) continue;
                bool currentHit = CanHitPoint(origin, target.transform.position, target, attack, range);
                bool perfectWindow = target.dodgeDefinition != null && target.dodgeDefinition.IsPerfectWindow(target.State);
                bool avoidedAtOrigin = perfectWindow && CanHitPoint(origin,
                    new Vector3(target.State.DodgeOriginX, target.State.DodgeOriginY, target.State.DodgeOriginZ), target, attack, range);
                if (!currentHit && !avoidedAtOrigin) continue;
                if (target.dodgeDefinition != null && target.dodgeDefinition.IsInvulnerable(target.State))
                {
                    var sourceFaction = GetComponent<CharacterFaction>();
                    var targetFaction = target.GetComponent<CharacterFaction>();
                    if (perfectWindow && sourceFaction != null && sourceFaction.Team == CharacterTeam.Enemy &&
                        targetFaction != null && targetFaction.Team == CharacterTeam.Player)
                    {
                        receiver.RestoreStamina(target.dodgeDefinition.PerfectStaminaRecovery);
                        var evaded = target.State;
                        evaded.DodgeRewarded = true;
                        target.State = evaded;
                    }
                    continue;
                }
                if (!currentHit) continue;
                float damage = CombatRules.Damage(_settings[State.AttackIndex], source.Snapshot.Values.AttackPower,
                    receiver.Snapshot.Values.Defense);
                float applied = receiver.ApplyDamage(damage);
                if (applied <= 0f) continue;
                var interrupted = target.State;
                interrupted.Dodging = false;
                if (target.interruptAttackOnHit)
                {
                    interrupted.Attacking = interrupted.Buffered = false;
                    interrupted.NextAttackIndex = 0;
                    target._pendingHit = false;
                }
                target.State = interrupted;
                target.State = CombatRules.ReceiveHit(target.State,
                    target.reactToHitsWhileMoving ||
                    (target._motor.IsGrounded && target._motor.CurrentMovementState.HorizontalSpeed < .1f));
                State = CombatRules.ApplyHitStop(State, CurrentWeapon.HitStopDuration);
                target.State = CombatRules.ApplyHitStop(target.State, CurrentWeapon.HitStopDuration);
                Vector3 knockbackDirection = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
                if (knockbackDirection.sqrMagnitude < .001f) knockbackDirection = transform.forward;
                target._motor.ApplyKnockback(knockbackDirection, CurrentWeapon.KnockbackDistance, CurrentWeapon.KnockbackDuration);
                target.DamageReceived?.Invoke(this, applied);
                ResolvedHits++;
            }
        }
    }
}
