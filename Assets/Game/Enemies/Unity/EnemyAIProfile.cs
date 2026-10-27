using UnityEngine;

namespace Game.Enemies.Unity
{
    [CreateAssetMenu(menuName = "Game/Enemies/AI Profile")]
    public sealed class EnemyAIProfile : ScriptableObject
    {
        [SerializeField, Min(1f)] private float detectionRange = 10f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 120f;
        [SerializeField, Min(1f)] private float loseRange = 14f;
        [SerializeField, Min(1f)] private float homeLeash = 18f;
        [SerializeField, Min(.1f)] private float lostSightGrace = 4f;
        [SerializeField, Min(.05f)] private float perceptionInterval = .25f;
        [SerializeField, Min(.05f)] private float repathInterval = .3f;
        [SerializeField, Min(.1f)] private float unreachableGrace = 3f;
        [SerializeField, Min(.1f)] private float attackDistance = 1.4f;
        [SerializeField, Min(.1f)] private float attackHeightTolerance = 1.2f;
        [SerializeField, Min(0f)] private float attackCooldown = .7f;
        [Header("Attack Groups")]
        [SerializeField, Min(1)] private int attacksPerGroup = 1;
        [SerializeField, Min(0f)] private float intraGroupCooldown = .2f;
        [SerializeField, Min(0f)] private float hitReactionDuration;
        [Header("Patrol (zero radius disables patrol)")]
        [SerializeField, Min(0f)] private float patrolRadius;
        [SerializeField, Range(.05f, 1f)] private float patrolSpeedMultiplier = .32f;
        [SerializeField, Min(0f)] private float patrolWaitMinimum = 1f;
        [SerializeField, Min(0f)] private float patrolWaitMaximum = 3f;
        [SerializeField, Min(.1f)] private float sightHeight = 1.2f;
        [SerializeField, Range(1f, 90f)] private float attackFacingAngle = 12f;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;
        [SerializeField, Min(.1f)] private float homeArrivalDistance = .4f;
        [SerializeField] private LayerMask sightMask = ~0;
        public float DetectionRange => detectionRange;
        public float FieldOfView => fieldOfView;
        public float LoseRange => Mathf.Max(detectionRange, loseRange);
        public float HomeLeash => homeLeash;
        public float LostSightGrace => lostSightGrace;
        public float PerceptionInterval => perceptionInterval;
        public float RepathInterval => repathInterval;
        public float UnreachableGrace => unreachableGrace;
        public float AttackDistance => attackDistance;
        public float AttackHeightTolerance => attackHeightTolerance;
        public float AttackCooldown => attackCooldown;
        public int AttacksPerGroup => Mathf.Max(1, attacksPerGroup);
        public float IntraGroupCooldown => intraGroupCooldown;
        public float HitReactionDuration => hitReactionDuration;
        public float PatrolRadius => patrolRadius;
        public float PatrolSpeedMultiplier => patrolSpeedMultiplier;
        public float PatrolWaitMinimum => patrolWaitMinimum;
        public float PatrolWaitMaximum => Mathf.Max(patrolWaitMinimum, patrolWaitMaximum);
        public float SightHeight => sightHeight;
        public float AttackFacingAngle => attackFacingAngle;
        public float TurnSpeed => turnSpeed;
        public float HomeArrivalDistance => homeArrivalDistance;
        public LayerMask SightMask => sightMask;
    }
}
