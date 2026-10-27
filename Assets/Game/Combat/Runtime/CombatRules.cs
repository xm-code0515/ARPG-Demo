using System;
using System.Collections.Generic;

namespace Game.Combat
{
    public static class CombatRules
    {
        public static CombatStep Step(CombatState state, IReadOnlyList<AttackSettings> attacks,
            bool pressed, bool held, bool grounded, bool dead, float stamina, float deltaTime, float comboResetDelay = float.PositiveInfinity)
        {
            if (attacks == null || attacks.Count == 0) throw new ArgumentException("A weapon requires attacks.");
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            state.HitElapsed = Math.Min(10f, state.HitElapsed + deltaTime);
            if (dead)
            {
                state.Attacking = state.Buffered = false;
                state.Dodging = false;
                state.DodgeCooldownRemaining = 0f;
                state.HitStopRemaining = 0f;
                return new CombatStep(state, 0f, false);
            }
            if (state.HitStopRemaining > 0f)
            {
                state.Buffered |= pressed;
                state.HitStopRemaining = Math.Max(0f, state.HitStopRemaining - deltaTime);
                if (state.HitStopRemaining < .00001f) state.HitStopRemaining = 0f;
                return new CombatStep(state, 0f, false);
            }
            if (state.Dodging) return new CombatStep(state, 0f, false);
            bool hit = false;
            if (state.Attacking)
            {
                state.Buffered |= pressed;
                AttackSettings attack = attacks[state.AttackIndex];
                float previousElapsed = state.Elapsed;
                state.Elapsed += deltaTime;
                if (!state.HitApplied && state.Elapsed >= attack.HitTime)
                {
                    state.HitApplied = true;
                    hit = true;
                }
                if (state.Elapsed >= attack.Duration) state.Attacking = false;
                return new CombatStep(state, 0f, hit, attack.MovementBetween(previousElapsed, state.Elapsed), attack.RightMovementBetween(previousElapsed, state.Elapsed));
            }
            state.Elapsed = Math.Min(60f, state.Elapsed + deltaTime);
            if (state.Elapsed >= attacks[state.AttackIndex % attacks.Count].Duration + comboResetDelay) state.NextAttackIndex = 0;
            bool requested = pressed || held || state.Buffered;
            state.Buffered = false;
            if (!requested || !grounded) return new CombatStep(state, 0f, false);
            int index = state.NextAttackIndex % attacks.Count;
            AttackSettings next = attacks[index];
            if (stamina < next.StaminaCost) return new CombatStep(state, 0f, false);
            state.Attacking = true;
            state.HitApplied = false;
            state.AttackIndex = index;
            state.NextAttackIndex = (index + 1) % attacks.Count;
            state.AttackSequence = unchecked(state.AttackSequence + 1);
            state.Elapsed = 0f;
            return new CombatStep(state, next.StaminaCost, false);
        }

        public static float Damage(AttackSettings attack, float attackPower, float defense) =>
            Math.Max(0f, attack.BaseDamage + Math.Max(0f, attackPower) * attack.AttackPowerScale - Math.Max(0f, defense));

        public static CombatState ReceiveHit(CombatState state, bool stationaryAndGrounded)
        {
            if (!state.Attacking && stationaryAndGrounded)
            {
                state.HitSequence = unchecked(state.HitSequence + 1);
                state.HitElapsed = 0f;
            }
            return state;
        }

        public static CombatState ApplyHitStop(CombatState state, float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));
            state.HitStopRemaining = Math.Max(state.HitStopRemaining, duration);
            return state;
        }
    }
}
