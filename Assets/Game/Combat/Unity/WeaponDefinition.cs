using System;
using UnityEngine;

namespace Game.Combat.Unity
{
    [Serializable]
    public sealed class MeleeAttackDefinition
    {
        public bool suppressDamage;
        public AnimationClip animation;
        [Min(.05f)] public float duration = 1f;
        [Min(0f)] public float hitTime = .25f;
        [Min(0f)] public float staminaCost = 6f;
        [Min(0f)] public float baseDamage = 5f;
        [Min(0f)] public float attackPowerScale = .5f;
        [Min(.1f)] public float range = 1.5f;
        [Range(1f, 180f)] public float angle = 100f;
        [Min(.05f)] public float verticalReach = 1f;
        [Header("Attack Step")]
        [Min(0f)] public float forwardDistance;
        [Min(0f)] public float movementStart = .06f;
        [Min(0f)] public float movementEnd = .3f;
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public Vector2[] rootMotionSamples;
        [Min(0)] public float damageMultiplier = 1f;
        [Min(0)] public float staminaMultiplier = 1f;

        public AttackSettings CreateSettings(float? damage = null, float? powerScale = null, float? stamina = null, float playbackSpeed = 1f)
        {
            float[] samples = null;
            if (forwardDistance > 0f && movementCurve != null && movementCurve.length > 0)
            {
                samples = new float[33];
                for (int i = 1; i < samples.Length - 1; i++)
                    samples[i] = Mathf.Max(samples[i - 1], Mathf.Clamp01(movementCurve.Evaluate(i / 32f)));
                samples[samples.Length - 1] = 1f;
            }
            float[] right = null, forward = null;
            if (rootMotionSamples != null && rootMotionSamples.Length >= 2)
            {
                right = new float[rootMotionSamples.Length]; forward = new float[right.Length];
                for (int i = 0; i < right.Length; i++)
                { right[i] = rootMotionSamples[i].x; forward[i] = rootMotionSamples[i].y; }
            }
            return new AttackSettings(duration / playbackSpeed, hitTime / playbackSpeed, stamina.HasValue ? stamina.Value * staminaMultiplier : staminaCost,
                damage.HasValue ? damage.Value * damageMultiplier : baseDamage, powerScale ?? attackPowerScale,
                forwardDistance, movementStart / playbackSpeed, movementEnd / playbackSpeed, samples, right, forward);
        }
    }

    [CreateAssetMenu(menuName = "Game/Combat/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string weaponId = "unarmed";
        [SerializeField] private GameObject appearance;
        [SerializeField] private MeleeAttackDefinition[] attacks;
        [SerializeField] private MeleeAttackPattern attackPattern;
        [SerializeField, Min(0)] private float baseDamage = 18;
        [SerializeField, Min(0)] private float attackPowerScale = .5f;
        [SerializeField, Min(0)] private float staminaCost = 8;
        [SerializeField, Min(.1f)] private float range = 2.1f;
        [Header("Impact")]
        [SerializeField, Min(0f)] private float knockbackDistance = .4f;
        [SerializeField, Min(.01f)] private float knockbackDuration = .25f;
        [SerializeField, Min(0f)] private float hitStopDuration = .2f;
        [SerializeField] private Vector3 heldPosition;
        [SerializeField] private Vector3 heldEulerAngles;
        [SerializeField] private Vector3 heldScale = Vector3.one;
        public string WeaponId => weaponId;
        public GameObject Appearance => appearance;
        public MeleeAttackDefinition[] Attacks => attackPattern != null ? attackPattern.Attacks : attacks;
        public MeleeAttackPattern AttackPattern => attackPattern;
        public float BaseDamage => attackPattern != null ? baseDamage : Attacks[0].baseDamage;
        public float AttackPowerScale => attackPattern != null ? attackPowerScale : Attacks[0].attackPowerScale;
        public float StaminaCost => attackPattern != null ? staminaCost : Attacks[0].staminaCost;
        public float Range => attackPattern != null ? range : Attacks[0].range;
        public float KnockbackDistance => Mathf.Max(0f, knockbackDistance);
        public float KnockbackDuration => Mathf.Max(.01f, knockbackDuration);
        public float HitStopDuration => Mathf.Max(0f, hitStopDuration);
        public Vector3 HeldPosition => heldPosition;
        public Vector3 HeldEulerAngles => heldEulerAngles;
        public Vector3 HeldScale => heldScale;
        public float AttackRange(int index) => attackPattern != null ? range : Attacks[index].range;
        public AttackSettings[] CreateSettings()
        {
            var actions = Attacks;
            if (actions == null || actions.Length == 0) throw new InvalidOperationException("Weapon has no attacks.");
            var result = new AttackSettings[actions.Length];
            for (int i = 0; i < result.Length; i++) result[i] = attackPattern != null
                ? actions[i].CreateSettings(baseDamage, attackPowerScale, staminaCost, attackPattern.AttackPlaybackSpeed) : actions[i].CreateSettings();
            return result;
        }
    }
}
