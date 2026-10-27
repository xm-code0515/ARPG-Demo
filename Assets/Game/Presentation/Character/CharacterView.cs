using Game.Character;
using Game.Attributes.Unity;
using Game.Combat.Unity;
using UnityEngine;

namespace Game.Presentation.Character
{
    public sealed class CharacterView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterMotor motor;
        [SerializeField] private Animator animator;
        [Header("Animation")]
        [SerializeField] private float speedDampTime = .1f;
        [SerializeField, Min(.01f)] private float weaponBlendTime = .18f;

        private IMovementStateSource _movementSource;
        private CharacterCombatController _combat;
        private CharacterAttributes _attributes;
        private uint _hitSequence;
        private uint _attackSequence;
        private bool _wasAttacking;
        private RuntimeAnimatorController _fallbackController;
        private RuntimeAnimatorController _activeController;
        private bool _hasWeaponType;
        private bool _hasWeaponBlend;
        private int _weaponGripLayer = -1;
        private bool _hasAttackPlaybackSpeed;
        private bool _hasDodge;
        private bool _hasRecovering;
        private IBossAnimationStateSource _bossState;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int AttackingHash = Animator.StringToHash("Attacking");
        private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int WeaponTypeHash = Animator.StringToHash("WeaponType");
        private static readonly int WeaponBlendHash = Animator.StringToHash("WeaponBlend");
        private static readonly int AttackPlaybackSpeedHash = Animator.StringToHash("AttackPlaybackSpeed");
        private static readonly int DodgingHash = Animator.StringToHash("Dodging");
        private static readonly int DodgePlaybackSpeedHash = Animator.StringToHash("DodgePlaybackSpeed");
        private static readonly int RecoveringHash = Animator.StringToHash("Recovering");

        private void Awake()
        {
            _combat = GetComponent<CharacterCombatController>();
            _attributes = GetComponent<CharacterAttributes>();
            _bossState = GetComponent<IBossAnimationStateSource>();
            if (animator != null) _fallbackController = animator.runtimeAnimatorController;
        }

        public void SetMovementStateSource(IMovementStateSource source)
        {
            _movementSource = source;
            _hitSequence = _combat != null ? _combat.State.HitSequence : 0;
            _attackSequence = _combat != null ? _combat.State.AttackSequence : 0;
            _wasAttacking = false;
            if (animator != null)
            {
                animator.ResetTrigger(HitHash);
                animator.ResetTrigger(AttackHash);
            }
        }

        private void LateUpdate()
        {
            if (animator == null) return;
            var pattern = _combat != null && _combat.CurrentWeapon != null ? _combat.CurrentWeapon.AttackPattern : null;
            var controller = pattern != null && pattern.AnimatorController != null ? pattern.AnimatorController : _fallbackController;
            bool controllerRebound = false;
            if (_activeController != controller)
            {
                _activeController = controller;
                if (animator.runtimeAnimatorController != controller)
                {
                    animator.runtimeAnimatorController = controller;
                    controllerRebound = true;
                }
                _hasWeaponType = false;
                _hasWeaponBlend = false;
                _hasAttackPlaybackSpeed = false;
                _hasDodge = false;
                _hasRecovering = false;
                _weaponGripLayer = animator.GetLayerIndex("Weapon Grip");
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.nameHash == WeaponTypeHash) _hasWeaponType = true;
                    if (parameter.nameHash == WeaponBlendHash) _hasWeaponBlend = true;
                    if (parameter.nameHash == AttackPlaybackSpeedHash) _hasAttackPlaybackSpeed = true;
                    if (parameter.nameHash == DodgingHash) _hasDodge = true;
                    if (parameter.nameHash == RecoveringHash) _hasRecovering = true;
                }
                _wasAttacking = false;
            }
            if (_hasWeaponType) animator.SetInteger(WeaponTypeHash, pattern != null ? pattern.AnimationType : 0);
            float weaponBlend = pattern != null && pattern.AnimationType == 1 ? 1f : 0f;
            if (_hasWeaponBlend)
            {
                // A shared locomotion tree blends poses without replacing the running controller.
                animator.SetFloat(WeaponBlendHash, weaponBlend, weaponBlendTime, Time.deltaTime);
                weaponBlend = animator.GetFloat(WeaponBlendHash);
            }
            if (_hasAttackPlaybackSpeed) animator.SetFloat(AttackPlaybackSpeedHash, pattern != null ? pattern.AttackPlaybackSpeed : 1f);
            if (_weaponGripLayer >= 0)
                animator.SetLayerWeight(_weaponGripLayer, weaponBlend);
            IMovementStateSource source = _movementSource ?? motor;
            if (source != null)
            {
                MovementState movement = source.CurrentMovementState;
                animator.SetFloat(SpeedHash, movement.HorizontalSpeed, speedDampTime, Time.deltaTime);
                animator.SetBool(GroundedHash, movement.IsGrounded);
                animator.SetFloat(VerticalSpeedHash, movement.VerticalVelocity);
            }
            bool dead = _attributes != null && _attributes.IsReady && _attributes.Snapshot.IsDead;
            var combat = _combat != null ? _combat.State : default;
            bool attacking = !dead && combat.Attacking;
            if (_hasDodge)
            {
                animator.SetBool(DodgingHash, !dead && combat.Dodging);
                animator.SetFloat(DodgePlaybackSpeedHash, _combat.Dodge != null ? _combat.Dodge.AnimationSpeed : 1f);
            }
            animator.speed = !dead && combat.HitStopRemaining > 0f ? 0f : 1f;
            animator.SetBool(DeadHash, dead);
            animator.SetBool(AttackingHash, attacking);
            if (_hasRecovering) animator.SetBool(RecoveringHash, !dead && _bossState != null && _bossState.Recovering);
            animator.SetInteger(AttackIndexHash, combat.AttackIndex);
            if (!attacking) animator.ResetTrigger(AttackHash);
            // Consecutive simulation ticks can end and start an attack between rendered frames.
            // Each attack has its own sequence even when Attacking never appears false here.
            else if (!_wasAttacking || combat.AttackSequence != _attackSequence) animator.SetTrigger(AttackHash);
            _attackSequence = combat.AttackSequence;
            _wasAttacking = attacking;
            animator.ResetTrigger(HitHash);
            if (combat.HitSequence != _hitSequence)
            {
                _hitSequence = combat.HitSequence;
                if (!dead && !attacking && !combat.Dodging && combat.HitSequence != 0) animator.SetTrigger(HitHash);
            }
            // A different future weapon controller must be sampled in this frame as well.
            if (controllerRebound) animator.Update(0f);
        }

        private void OnDisable()
        {
            if (animator != null) animator.speed = 1f;
        }
    }
}
