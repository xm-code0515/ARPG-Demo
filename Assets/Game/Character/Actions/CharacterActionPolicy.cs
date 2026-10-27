using System;

namespace Game.Character.Actions
{
    public static class CharacterActionPolicy
    {
        public static CharacterActionDecision BeginStep(CharacterActionSettings settings, CharacterActionState state,
            bool jumpPressed, bool sprintHeld, bool hasMoveInput, bool grounded, float deltaTime)
        {
            ValidateStep(deltaTime);
            if (state.IsDead) return new CharacterActionDecision(state, false, false, false, settings.TiredJumpHeight);
            float stamina = state.Stamina;
            bool jump = jumpPressed && grounded;
            float height = settings.NormalJumpHeight;
            float delay = state.RecoveryDelayRemaining;
            bool exhausted = state.SprintExhausted;
            if (jump)
            {
                bool normal = stamina >= settings.JumpCost;
                height = normal ? settings.NormalJumpHeight : settings.TiredJumpHeight;
                stamina = Math.Max(0f, stamina - settings.JumpCost);
                delay = settings.RecoveryDelay;
                exhausted |= stamina <= 0f && settings.SprintCostPerSecond > 0f;
            }
            if (settings.SprintCostPerSecond == 0f ||
                (stamina > 0f && stamina >= Math.Min(state.MaxStamina, settings.SprintResumeStamina))) exhausted = false;
            bool wantsSprint = sprintHeld && hasMoveInput && grounded && !jump;
            bool affordable = settings.SprintCostPerSecond == 0f || stamina > 0f;
            if (wantsSprint && !affordable) exhausted = true;
            var next = new CharacterActionState(stamina, state.MaxStamina, state.MoveSpeed, delay, exhausted, state.IsDead, state.Combat);
            return new CharacterActionDecision(next, true, jump, wantsSprint && affordable && !exhausted, height);
        }

        public static CharacterActionState CompleteStep(CharacterActionSettings settings, CharacterActionDecision decision,
            bool actuallySprinting, float deltaTime)
        {
            ValidateStep(deltaTime);
            CharacterActionState state = decision.State;
            if (state.IsDead) return state;
            float stamina = state.Stamina;
            float delay = state.RecoveryDelayRemaining;
            bool exhausted = state.SprintExhausted;
            bool spend = actuallySprinting && decision.Sprint && settings.SprintCostPerSecond > 0f;
            if (spend)
            {
                stamina = Math.Max(0f, stamina - settings.SprintCostPerSecond * deltaTime);
                exhausted |= stamina <= .00001f;
            }
            if (spend || decision.Jump) delay = settings.RecoveryDelay;
            else
            {
                float recoveryTime = Math.Max(0f, deltaTime - delay);
                delay = Math.Max(0f, delay - deltaTime);
                stamina = (float)Math.Min(state.MaxStamina, stamina + (double)settings.RecoveryPerSecond * recoveryTime);
            }
            if (stamina > 0f && stamina >= Math.Min(state.MaxStamina, settings.SprintResumeStamina)) exhausted = false;
            return new CharacterActionState(stamina, state.MaxStamina, state.MoveSpeed, delay, exhausted, state.IsDead, state.Combat);
        }

        private static void ValidateStep(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }
    }
}
