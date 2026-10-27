using UnityEngine;

namespace Game.Combat.Unity
{
    [CreateAssetMenu(menuName = "Game/Combat/Melee Attack Pattern")]
    public sealed class MeleeAttackPattern : ScriptableObject
    {
        [SerializeField] private string displayName = "单手剑";
        [SerializeField] private int animationType = 1;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField, Min(0)] private float comboResetDelay = .8f;
        [SerializeField, Range(.1f, 3f)] private float attackPlaybackSpeed = 1f;
        [SerializeField] private MeleeAttackDefinition[] attacks;
        public string DisplayName => displayName;
        public int AnimationType => animationType;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public float ComboResetDelay => comboResetDelay;
        public float AttackPlaybackSpeed => Mathf.Clamp(attackPlaybackSpeed, .1f, 3f);
        public MeleeAttackDefinition[] Attacks => attacks;
    }
}
