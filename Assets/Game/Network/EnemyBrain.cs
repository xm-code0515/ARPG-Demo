using Game.Attributes.Unity;
using Game.Character;
using Game.Combat.Unity;
using Game.Enemies.Unity;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Network
{
    public enum EnemyIntent { Idle, Chase, Attack, ReturnHome, Dead, Patrol, HitReaction, Recovering }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private CharacterCombatController _combat;
        private BossEncounterController _boss;
        private EnemyAIProfile _profile;
        private NetworkCharacter _target;
        private Vector3 _home;
        private bool _authority, _warned;
        private float _time, _nextPerception, _nextPath, _lastSeen, _nextAttack, _unreachableSince = -1f;
        private readonly RaycastHit[] _sightHits = new RaycastHit[32];
        private NavMeshPath _patrolPath;
        private Vector3 _patrolDestination;
        private bool _hasPatrolDestination, _wasAttacking;
        private float _patrolWaitUntil, _patrolStartedAt, _hitRecoveryRemaining;
        private uint _attackSequence, _hitSequence;
        private int _attacksInGroup;
        public EnemyIntent Intent { get; private set; }
        public NetworkCharacter Target => _target;
        public Vector3 Home => _home;

        private void Awake()
        {
            _patrolPath = new NavMeshPath();
            _agent = GetComponent<NavMeshAgent>();
            _agent.enabled = false;
            _agent.updatePosition = _agent.updateRotation = false;
            _agent.autoTraverseOffMeshLink = false;
            _combat = GetComponent<CharacterCombatController>();
            _boss = GetComponent<BossEncounterController>();
            _combat.DamageReceived += HandleDamageReceived;
        }

        public void Begin(EnemyAIProfile profile, bool authority)
        {
            Stop();
            _profile = profile;
            _authority = authority && profile != null;
            _home = transform.position;
            _time = _nextPerception = _nextPath = _lastSeen = _nextAttack = 0f;
            _unreachableSince = -1f;
            _hasPatrolDestination = _wasAttacking = false;
            _patrolWaitUntil = _hitRecoveryRemaining = 0f;
            _attackSequence = _combat.State.AttackSequence;
            _hitSequence = _combat.State.HitSequence;
            _attacksInGroup = 0;
            _warned = false;
            Intent = EnemyIntent.Idle;
            _boss?.Begin(_authority);
            if (_authority) EnsureNavigation();
        }

        public void Stop()
        {
            _authority = false;
            _target = null;
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_agent != null) _agent.enabled = false;
        }

        public CharacterMoveCommand Tick(float deltaTime, bool dead, float moveSpeed)
        {
            if (!_authority) return default;
            _time += deltaTime;
            _boss?.Observe(deltaTime, dead);
            if (dead)
            {
                Intent = EnemyIntent.Dead;
                _target = null;
                _agent.enabled = false;
                return default;
            }
            if (!EnsureNavigation()) return default;
            _agent.speed = Mathf.Max(.1f, moveSpeed);
            // Count attacks accepted by Combat, then start cooldown after the actual end.
            // Hit stop can extend an attack, so a timer started on the request is inaccurate.
            var combat = _combat.State;
            if (combat.AttackSequence != _attackSequence)
            {
                _attackSequence = combat.AttackSequence;
                _attacksInGroup++;
            }
            if (combat.HitSequence != _hitSequence)
            {
                _hitSequence = combat.HitSequence;
                _hitRecoveryRemaining = _profile.HitReactionDuration;
                if (_hitRecoveryRemaining > 0f && _boss == null) _attacksInGroup = 0;
            }
            if (_boss == null && _wasAttacking && !combat.Attacking)
            {
                bool groupFinished = _attacksInGroup >= _profile.AttacksPerGroup;
                _nextAttack = _time + (groupFinished ? _profile.AttackCooldown : _profile.IntraGroupCooldown);
                if (groupFinished) _attacksInGroup = 0;
            }
            _wasAttacking = combat.Attacking;
            if (_hitRecoveryRemaining > 0f)
            {
                if (combat.HitStopRemaining <= 0f) _hitRecoveryRemaining = Mathf.Max(0f, _hitRecoveryRemaining - deltaTime);
                _agent.isStopped = true;
                Intent = EnemyIntent.HitReaction;
                return default;
            }
            if (_combat.State.Attacking)
            {
                _agent.isStopped = true;
                Intent = EnemyIntent.Attack;
                return new CharacterMoveCommand(Vector3.zero, CharacterHeldButtons.None, CharacterPressedButtons.None, transform.forward);
            }
            if (_target != null && (!IsAlive(_target) || Distance(_target.transform.position, _home) > _profile.HomeLeash ||
                Distance(_target.transform.position, transform.position) > _profile.LoseRange || Distance(transform.position, _home) > _profile.HomeLeash + 2f))
                ReturnHome();
            if (Intent == EnemyIntent.ReturnHome)
            {
                if (Distance(transform.position, _home) <= _profile.HomeArrivalDistance)
                {
                    Intent = EnemyIntent.Idle;
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    _nextPerception = _time + _profile.PerceptionInterval;
                    return default;
                }
                return Navigate(_home, deltaTime, _profile.PatrolRadius > 0f ? _profile.PatrolSpeedMultiplier : 1f);
            }
            if (_boss != null && _boss.Recovering)
            {
                Intent = EnemyIntent.Recovering;
                _agent.isStopped = true;
                return default;
            }
            if (_time >= _nextPerception)
            {
                _nextPerception = _time + _profile.PerceptionInterval;
                if (_target == null) AcquireTarget();
                else if (CanSee(_target) || (_boss != null && _boss.Engaged)) _lastSeen = _time;
            }
            if (_target == null)
            {
                return Patrol(deltaTime);
            }
            if (_time - _lastSeen > _profile.LostSightGrace)
            {
                ReturnHome();
                return default;
            }
            Vector3 difference = _target.transform.position - transform.position;
            Vector3 facing = Vector3.ProjectOnPlane(difference, Vector3.up).normalized;
            if (Distance(_target.transform.position, transform.position) <= _profile.AttackDistance &&
                Mathf.Abs(difference.y) <= _profile.AttackHeightTolerance && HasClearSight(_target))
            {
                Intent = EnemyIntent.Attack;
                _agent.isStopped = true;
                _unreachableSince = -1f;
                Turn(facing, deltaTime);
                bool attack = _time >= _nextAttack && Vector3.Angle(transform.forward, facing) <= _profile.AttackFacingAngle;
                if (attack && _boss != null)
                {
                    int index = _boss.PrepareAttack();
                    attack = index >= 0 && _combat.TryPrepareAttack(index);
                }
                return new CharacterMoveCommand(Vector3.zero, CharacterHeldButtons.None,
                    attack ? CharacterPressedButtons.Attack : CharacterPressedButtons.None, facing);
            }
            Intent = EnemyIntent.Chase;
            return Navigate(_target.transform.position, deltaTime);
        }

        private bool EnsureNavigation()
        {
            if (_agent.enabled && _agent.isOnNavMesh) return true;
            if (_time < _nextPath) return false;
            _nextPath = _time + _profile.RepathInterval;
            var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
            if (!NavMesh.SamplePosition(transform.position, out var hit, 2f, filter))
            {
                _agent.enabled = false;
                if (!_warned) { Debug.LogWarning("[EnemyBrain] 敌人附近没有可用 NavMesh，请烘焙场景导航并检查生成点。", this); _warned = true; }
                return false;
            }
            _agent.enabled = true;
            return _agent.Warp(hit.position);
        }

        private CharacterMoveCommand Navigate(Vector3 destination, float deltaTime, float speedMultiplier = 1f)
        {
            _agent.speed *= speedMultiplier;
            _agent.isStopped = false;
            if (_time >= _nextPath)
            {
                _nextPath = _time + _profile.RepathInterval;
                var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
                if (NavMesh.SamplePosition(destination, out var hit, 1.5f, filter)) _agent.SetDestination(hit.position);
                else _agent.ResetPath();
            }
            if (_agent.pathPending) return default;
            if (!_agent.hasPath || _agent.pathStatus != NavMeshPathStatus.PathComplete || _agent.isOnOffMeshLink)
            {
                if (_unreachableSince < 0f) _unreachableSince = _time;
                if (Intent == EnemyIntent.Chase && _time - _unreachableSince >= _profile.UnreachableGrace) ReturnHome();
                return default;
            }
            _unreachableSince = -1f;
            var velocity = Vector3.ProjectOnPlane(_agent.desiredVelocity, Vector3.up);
            var direction = Vector3.ClampMagnitude(velocity / _agent.speed, 1f);
            Turn(direction, deltaTime);
            return new CharacterMoveCommand(direction * speedMultiplier, CharacterHeldButtons.None, CharacterPressedButtons.None, direction);
        }

        private CharacterMoveCommand Patrol(float deltaTime)
        {
            if (_profile.PatrolRadius <= 0f || _time < _patrolWaitUntil)
            {
                Intent = EnemyIntent.Idle;
                _agent.isStopped = true;
                return default;
            }
            if (_hasPatrolDestination && (Distance(transform.position, _patrolDestination) <= _profile.HomeArrivalDistance ||
                _time - _patrolStartedAt > 12f))
            {
                _hasPatrolDestination = false;
                _agent.isStopped = true;
                _agent.ResetPath();
                _patrolWaitUntil = _time + Random.Range(_profile.PatrolWaitMinimum, _profile.PatrolWaitMaximum);
                Intent = EnemyIntent.Idle;
                return default;
            }
            if (!_hasPatrolDestination)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    var offset = Random.insideUnitCircle * _profile.PatrolRadius;
                    if (!NavMesh.SamplePosition(_home + new Vector3(offset.x, 0f, offset.y), out var hit, 1.5f, filter) ||
                        Distance(hit.position, _home) > _profile.PatrolRadius || Distance(hit.position, transform.position) < 1f ||
                        !_agent.CalculatePath(hit.position, _patrolPath) || _patrolPath.status != NavMeshPathStatus.PathComplete) continue;
                    bool withinBounds = true;
                    foreach (var corner in _patrolPath.corners)
                        if (Distance(corner, _home) > _profile.PatrolRadius + .5f) { withinBounds = false; break; }
                    if (!withinBounds) continue;
                    _patrolDestination = hit.position;
                    _hasPatrolDestination = true;
                    _patrolStartedAt = _time;
                    _nextPath = _time;
                    break;
                }
                if (!_hasPatrolDestination)
                {
                    _patrolWaitUntil = _time + 1f;
                    _agent.isStopped = true;
                    Intent = EnemyIntent.Idle;
                    return default;
                }
            }
            Intent = EnemyIntent.Patrol;
            return Navigate(_patrolDestination, deltaTime, _profile.PatrolSpeedMultiplier);
        }

        public void SyncNavigation()
        {
            if (_authority && _agent.enabled && _agent.isOnNavMesh) _agent.nextPosition = transform.position;
        }

        private void Turn(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), _profile.TurnSpeed * deltaTime);
        }

        private void AcquireTarget()
        {
            float nearest = _profile.DetectionRange;
            foreach (var player in FindObjectsByType<NetworkCharacter>())
            {
                if (!IsAlive(player) || Distance(player.transform.position, _home) > _profile.HomeLeash) continue;
                float distance = Distance(player.transform.position, transform.position);
                if (distance < nearest && CanSee(player)) { _target = player; nearest = distance; }
            }
            if (_target != null) { _lastSeen = _time; _nextPath = _time; _hasPatrolDestination = false; _boss?.Engage(); }
        }

        private bool CanSee(NetworkCharacter player)
        {
            Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .0001f && Vector3.Angle(transform.forward, direction) > _profile.FieldOfView * .5f)
                return false;
            return HasClearSight(player);
        }

        private bool HasClearSight(NetworkCharacter player)
        {
            Vector3 origin = transform.position + Vector3.up * _profile.SightHeight;
            Vector3 offset = player.transform.position + Vector3.up - origin;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, _sightHits, offset.magnitude,
                _profile.SightMask, QueryTriggerInteraction.Ignore);
            if (count == _sightHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (_sightHits[i].collider.GetComponentInParent<CharacterAttributes>() == null) return false;
            return true;
        }

        private void HandleDamageReceived(CharacterCombatController attacker, float damage)
        {
            if (!_authority || _profile == null || damage <= 0f || attacker == null ||
                GetComponent<CharacterAttributes>().Snapshot.IsDead) return;
            var player = attacker.GetComponent<NetworkCharacter>();
            if (!IsAlive(player)) return;
            _target = player;
            _lastSeen = _time;
            _nextPerception = _time + _profile.PerceptionInterval;
            _nextPath = _time;
            _unreachableSince = -1f;
            _hasPatrolDestination = false;
            _boss?.Engage();
            Intent = EnemyIntent.Chase;
        }

        public void AlertToTarget(NetworkCharacter player)
        {
            if (!_authority || _profile == null || !IsAlive(player)) return;
            _target = player;
            _lastSeen = _time;
            _nextPerception = _time + _profile.PerceptionInterval;
            _nextPath = _time;
            _hasPatrolDestination = false;
            _unreachableSince = -1f;
            _boss?.Engage();
            Intent = EnemyIntent.Chase;
        }

        private static bool IsAlive(NetworkCharacter player)
        {
            if (player == null || !player.IsSpawned) return false;
            var attributes = player.GetComponent<CharacterAttributes>();
            return attributes != null && attributes.IsReady && !attributes.Snapshot.IsDead;
        }

        private void ReturnHome()
        {
            _target = null;
            Intent = EnemyIntent.ReturnHome;
            _nextPath = _time;
            _unreachableSince = -1f;
            _hasPatrolDestination = false;
            _attacksInGroup = 0;
            _boss?.EndEncounter();
        }

        private static float Distance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        private void OnDisable() => Stop();
        private void OnDestroy()
        {
            if (_combat != null) _combat.DamageReceived -= HandleDamageReceived;
        }
    }
}
