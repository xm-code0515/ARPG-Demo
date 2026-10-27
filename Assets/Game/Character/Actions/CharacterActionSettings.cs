using System;

namespace Game.Character.Actions
{
    public readonly struct CharacterActionSettings
    {
        public readonly float JumpCost;
        public readonly float NormalJumpHeight;
        public readonly float TiredJumpHeight;
        public readonly float SprintCostPerSecond;
        public readonly float SprintMultiplier;
        public readonly float RecoveryPerSecond;
        public readonly float RecoveryDelay;
        public readonly float SprintResumeStamina;

        public CharacterActionSettings(float jumpCost, float normalJumpHeight, float tiredJumpHeight,
            float sprintCostPerSecond, float sprintMultiplier, float recoveryPerSecond, float recoveryDelay,
            float sprintResumeStamina)
        {
            JumpCost = Validate(jumpCost, 0f);
            NormalJumpHeight = Validate(normalJumpHeight, .01f);
            TiredJumpHeight = Validate(tiredJumpHeight, .01f);
            if (tiredJumpHeight > normalJumpHeight) throw new ArgumentOutOfRangeException(nameof(tiredJumpHeight));
            SprintCostPerSecond = Validate(sprintCostPerSecond, 0f);
            SprintMultiplier = Validate(sprintMultiplier, 1f);
            RecoveryPerSecond = Validate(recoveryPerSecond, 0f);
            RecoveryDelay = Validate(recoveryDelay, 0f);
            SprintResumeStamina = Validate(sprintResumeStamina, 0f);
        }

        private static float Validate(float value, float minimum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum)
                throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }
    }
}
