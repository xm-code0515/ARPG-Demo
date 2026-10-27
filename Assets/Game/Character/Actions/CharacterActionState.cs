using System;
using Game.Combat;

namespace Game.Character.Actions
{
    public readonly struct CharacterActionState : IEquatable<CharacterActionState>
    {
        public readonly float Stamina;
        public readonly float MaxStamina;
        public readonly float MoveSpeed;
        public readonly float RecoveryDelayRemaining;
        public readonly bool SprintExhausted;
        public readonly bool IsDead;
        public readonly CombatState Combat;

        public CharacterActionState(float stamina, float maxStamina, float moveSpeed,
            float recoveryDelayRemaining, bool sprintExhausted, bool isDead, CombatState combat = default)
        {
            Validate(stamina);
            Validate(maxStamina);
            Validate(moveSpeed);
            Validate(recoveryDelayRemaining);
            Stamina = Math.Min(stamina, maxStamina);
            MaxStamina = maxStamina;
            MoveSpeed = moveSpeed;
            RecoveryDelayRemaining = recoveryDelayRemaining;
            SprintExhausted = sprintExhausted;
            IsDead = isDead;
            Combat = combat;
        }

        public bool Equals(CharacterActionState other) => Stamina.Equals(other.Stamina) &&
            MaxStamina.Equals(other.MaxStamina) && MoveSpeed.Equals(other.MoveSpeed) &&
            RecoveryDelayRemaining.Equals(other.RecoveryDelayRemaining) &&
            SprintExhausted == other.SprintExhausted && IsDead == other.IsDead && Combat.Equals(other.Combat);
        public override bool Equals(object obj) => obj is CharacterActionState other && Equals(other);
        public override int GetHashCode() => Stamina.GetHashCode() ^ RecoveryDelayRemaining.GetHashCode();

        private static void Validate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public readonly struct CharacterActionDecision
    {
        public readonly CharacterActionState State;
        public readonly bool CanMove;
        public readonly bool Jump;
        public readonly bool Sprint;
        public readonly float JumpHeight;

        public CharacterActionDecision(CharacterActionState state, bool canMove, bool jump, bool sprint, float jumpHeight)
        {
            State = state;
            CanMove = canMove;
            Jump = jump;
            Sprint = sprint;
            JumpHeight = jumpHeight;
        }
    }
}
