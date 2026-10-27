using UnityEngine;

namespace Game.Combat.Unity
{
    [CreateAssetMenu(menuName = "Game/Combat/Dodge", fileName = "PlayerDodge")]
    public sealed class DodgeDefinition : ScriptableObject
    {
        [SerializeField] private AnimationClip animation;
        [SerializeField, Range(.2f, 2f)] private float duration = .75f;
        [SerializeField, Range(0f, 3f)] private float distance = 1.5f;
        [SerializeField, Min(0f)] private float staminaCost = 18f;
        [SerializeField, Min(0f)] private float cooldown = .95f;
        [SerializeField, Min(0f)] private float invulnerableStart = 0f;
        [SerializeField, Min(0f)] private float invulnerableEnd = .45f;
        [SerializeField, Min(0f)] private float perfectWindow = .2f;
        [SerializeField, Min(0f)] private float perfectStaminaRecovery = 10f;

        public AnimationClip Animation => animation;
        public float Duration => Mathf.Max(.2f, duration);
        public float Distance => Mathf.Clamp(distance, 0f, 3f);
        public float StaminaCost => Mathf.Max(0f, staminaCost);
        public float Cooldown => Mathf.Max(Duration, cooldown);
        public float AnimationSpeed => animation != null ? animation.length / Duration : 1f;
        public float PerfectStaminaRecovery => Mathf.Max(0f, perfectStaminaRecovery);
        public bool IsInvulnerable(CombatState state) => state.Dodging &&
            state.DodgeElapsed >= invulnerableStart && state.DodgeElapsed <= Mathf.Min(Duration, invulnerableEnd);
        public bool IsPerfectWindow(CombatState state) => IsInvulnerable(state) &&
            !state.DodgeRewarded && state.DodgeElapsed <= perfectWindow;

        public float MovementBetween(float from, float to)
        {
            float a = Mathf.Clamp01(from / Duration);
            float b = Mathf.Clamp01(to / Duration);
            return Distance * (b * b * (3f - 2f * b) - a * a * (3f - 2f * a));
        }
    }
}
