using System.Collections.Generic;
using Game.Attributes.Unity;
using Game.Boss;
using Game.Combat.Unity;
using Game.Enemies.Unity;
using Game.Presentation.Character;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    [RequireComponent(typeof(NetworkEnemy), typeof(EnemyBrain))]
    public sealed class BossEncounterController : NetworkBehaviour, IBossAnimationStateSource
    {
        [SerializeField] private BossEncounterDefinition encounter;
        [SerializeField] private EnemySpawnPoint summonPoint;
        private static readonly HashSet<BossEncounterController> Registry = new HashSet<BossEncounterController>();
        private readonly NetworkVariable<bool> _recovering = new NetworkVariable<bool>();
        private BossAttackCycle _cycle;
        private CharacterCombatController _combat;
        private EnemyBrain _brain;
        private NetworkEnemy _enemy;
        private CharacterAttributes _attributes;
        private bool _authority, _wasAttacking, _summoned;
        private uint _attackSequence;
        public static IEnumerable<BossEncounterController> ActiveBosses => Registry;
        public bool Recovering => _recovering.Value;
        public bool Engaged { get; private set; }
        public float CooldownRemaining => _cycle?.CooldownRemaining ?? 0f;
        public float HealthBarRange => encounter != null ? encounter.HealthBarRange : 28f;
        public CharacterAttributes Attributes => _attributes;
        public string DisplayName => _enemy.Definition != null ? _enemy.Definition.DisplayName : "狼王";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Registry.Clear();

        private void Awake()
        {
            _enemy = GetComponent<NetworkEnemy>();
            _brain = GetComponent<EnemyBrain>();
            _combat = GetComponent<CharacterCombatController>();
            _attributes = GetComponent<CharacterAttributes>();
            if (summonPoint != null) summonPoint.EnemySpawned += AlertSummonedWolf;
        }

        public override void OnNetworkSpawn() => Registry.Add(this);

        public void Begin(bool authority)
        {
            _authority = authority && encounter != null;
            if (!_authority) return;
            _cycle = new BossAttackCycle(_combat.CurrentWeapon.Attacks.Length, encounter.BetweenAttacks,
                encounter.RecoveryDuration, Random.Range(0, int.MaxValue));
            _attackSequence = _combat.State.AttackSequence;
            _wasAttacking = Engaged = _summoned = false;
            if (IsSpawned) _recovering.Value = false;
        }

        public void Observe(float deltaTime, bool dead)
        {
            if (!_authority || _cycle == null) return;
            if (dead) { EndEncounter(); return; }
            _cycle.Tick(deltaTime);
            var state = _combat.State;
            if (state.AttackSequence != _attackSequence)
            {
                _attackSequence = state.AttackSequence;
                _cycle.AttackStarted(state.AttackIndex);
                _summoned = false;
            }
            if (state.Attacking && state.AttackIndex == 2 && !_summoned && state.Elapsed >= encounter.SummonTime)
            {
                _summoned = true;
                summonPoint?.SpawnNow();
            }
            if (_wasAttacking && !state.Attacking) _cycle.AttackFinished();
            _wasAttacking = state.Attacking;
            _recovering.Value = _cycle.Recovering;
        }

        public int PrepareAttack() => _authority && _cycle != null ? _cycle.PrepareAttack() : -1;
        public void Engage() { if (_authority) Engaged = true; }

        public void EndEncounter()
        {
            if (!_authority) return;
            Engaged = _wasAttacking = false;
            _cycle?.Reset();
            if (IsSpawned) _recovering.Value = false;
            summonPoint?.DespawnWave();
        }

        private void AlertSummonedWolf(NetworkEnemy enemy)
        {
            if (_authority && _brain.Target != null) enemy.GetComponent<EnemyBrain>()?.AlertToTarget(_brain.Target);
        }

        public override void OnNetworkDespawn()
        {
            Registry.Remove(this);
            EndEncounter();
            _authority = false;
        }

        public override void OnDestroy()
        {
            Registry.Remove(this);
            if (summonPoint != null) summonPoint.EnemySpawned -= AlertSummonedWolf;
            base.OnDestroy();
        }
    }
}
