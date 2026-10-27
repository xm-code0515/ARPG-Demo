using System.Collections.Generic;
using Game.Attributes.Unity;
using Game.Enemies.Unity;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    // Placeable scene component: any catalog enemy with a registered network prefab can be used.
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField] private EnemyDefinition definition;
        [SerializeField, Min(1)] private int minimumCount = 2;
        [SerializeField, Min(1)] private int maximumCount = 4;
        [SerializeField, Min(1)] private int maximumAlive = 4;
        [SerializeField, Min(.5f)] private float spawnRadius = 4f;
        [SerializeField, Min(0f)] private float spawnInnerRadius;
        [SerializeField, Min(.5f)] private float minimumSpacing = 1.4f;
        [SerializeField, Min(0f)] private float initialDelay;
        [SerializeField, Min(1f)] private float respawnInterval = 30f;
        [SerializeField] private bool waitUntilCleared = true;
        private readonly List<NetworkEnemy> _wave = new List<NetworkEnemy>();
        private readonly Collider[] _spawnHits = new Collider[32];
        private bool _started, _waitingForClear, _reportedError;
        private double _nextSpawn;
        public EnemyDefinition Definition => definition;
        public float SpawnRadius => spawnRadius;
        public int WaveCount { get; private set; }
        public event System.Action<NetworkEnemy> EnemySpawned;
        public int AliveCount
        {
            get
            {
                int alive = 0;
                foreach (var enemy in _wave)
                    if (enemy != null && enemy.IsSpawned && !enemy.GetComponent<CharacterAttributes>().Snapshot.IsDead) alive++;
                return alive;
            }
        }
        private void Update()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.ShutdownInProgress) return;
            _wave.RemoveAll(enemy => enemy == null || !enemy.IsSpawned || enemy.GetComponent<CharacterAttributes>().Snapshot.IsDead);
            double now = manager.ServerTime.Time;
            if (!_started) { _started = true; _nextSpawn = now + initialDelay; }
            if (_waitingForClear)
            {
                if (AliveCount > 0) return;
                _waitingForClear = false;
                _nextSpawn = now + Mathf.Max(1f, respawnInterval);
            }
            if (now >= _nextSpawn) SpawnNow();
        }
        public NetworkEnemy SpawnNow()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.ShutdownInProgress) return null;
            _wave.RemoveAll(enemy => enemy == null || !enemy.IsSpawned || enemy.GetComponent<CharacterAttributes>().Snapshot.IsDead);
            _started = true;
            _nextSpawn = manager.ServerTime.Time + Mathf.Max(1f, respawnInterval);
            if (definition == null || !definition.IsConfigured || !manager.NetworkConfig.Prefabs.Contains(definition.Prefab))
            {
                if (!_reportedError) Debug.LogError("[EnemySpawnPoint] 请配置完整敌人定义并注册网络 Prefab。", this);
                _reportedError = true;
                return null;
            }
            var template = definition.Prefab.GetComponent<NetworkEnemy>();
            if (template == null || template.Catalog == null || template.Catalog.IndexOf(definition) < 0)
            {
                if (!_reportedError) Debug.LogError("[EnemySpawnPoint] 敌人定义必须加入该 Prefab 的 EnemyCatalog。", this);
                _reportedError = true;
                return null;
            }
            int min = Mathf.Max(1, minimumCount), max = Mathf.Max(min, maximumCount);
            int count = Mathf.Min(Random.Range(min, max + 1), Mathf.Max(0, maximumAlive - AliveCount));
            NetworkEnemy first = null;
            for (int i = 0; i < count; i++)
            {
                if (!TryPosition(out var position)) break;
                var instance = Instantiate(definition.Prefab, position, transform.rotation);
                SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                var enemy = instance.GetComponent<NetworkEnemy>();
                if (enemy == null) { Destroy(instance); Debug.LogError("[EnemySpawnPoint] Prefab 缺少 NetworkEnemy。", this); break; }
                enemy.Configure(definition);
                enemy.NetworkObject.Spawn(destroyWithScene: true);
                _wave.Add(enemy);
                EnemySpawned?.Invoke(enemy);
                if (first == null) first = enemy;
            }
            if (first != null) WaveCount++;
            _waitingForClear = waitUntilCleared && AliveCount > 0;
            return first;
        }

        public void DespawnWave()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.ShutdownInProgress) return;
            foreach (var enemy in _wave)
                if (enemy != null && enemy.IsSpawned) enemy.NetworkObject.Despawn();
            _wave.Clear();
            _started = _waitingForClear = false;
        }
        private bool TryPosition(out Vector3 position)
        {
            var agent = definition.Prefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent != null ? agent.agentTypeID : 0, areaMask = NavMesh.AllAreas };
            float radius = agent != null ? agent.radius : .35f;
            float height = agent != null ? agent.height : 2f;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var offset = Random.insideUnitCircle * Mathf.Max(.5f, spawnRadius);
                if (offset.magnitude < Mathf.Min(spawnInnerRadius, spawnRadius * .9f)) continue;
                var candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, filter)) continue;
                position = hit.position;
                if (Vector3.ProjectOnPlane(position - transform.position, Vector3.up).magnitude > spawnRadius + .2f) continue;
                if (Physics.CheckCapsule(position + Vector3.up * (radius + .15f),
                    position + Vector3.up * Mathf.Max(radius + .15f, height - radius), radius, 1, QueryTriggerInteraction.Ignore)) continue;
                int occupied = Physics.OverlapCapsuleNonAlloc(position + Vector3.up * (radius + .15f),
                    position + Vector3.up * Mathf.Max(radius + .15f, height - radius), radius, _spawnHits, ~0, QueryTriggerInteraction.Ignore);
                bool characterBlocked = occupied == _spawnHits.Length;
                for (int j = 0; j < occupied && !characterBlocked; j++)
                    characterBlocked = _spawnHits[j].GetComponentInParent<CharacterAttributes>() != null;
                if (characterBlocked) continue;
                bool overlaps = false;
                foreach (var enemy in _wave)
                    if (enemy != null && enemy.IsSpawned && Vector3.ProjectOnPlane(enemy.transform.position - position, Vector3.up).magnitude < Mathf.Max(minimumSpacing, radius * 2f))
                    { overlaps = true; break; }
                if (overlaps) continue;
                return true;
            }
            position = default;
            if (!_reportedError) Debug.LogWarning("[EnemySpawnPoint] 刷怪范围缺少可用导航位置，请调整位置或半径。", this);
            _reportedError = true;
            return false;
        }
        private void OnDrawGizmosSelected()
        { Gizmos.color = new Color(1f, .3f, .2f, .6f); Gizmos.DrawWireSphere(transform.position, spawnRadius); }
    }
}
