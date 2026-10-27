using Game.Attributes.Unity;
using Game.Presentation.World;
using Game.Quests;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Network
{
    // Local presentation only. Each player independently finds the nearest reachable destination.
    public sealed class NetworkQuestGuidance : MonoBehaviour
    {
        [SerializeField] private QuestGroundArrowView arrow;
        [SerializeField, Min(.2f)] private float routeInterval = .75f;
        [SerializeField, Min(.5f)] private float lookAhead = 2f;
        [Header("Hide guidance near the objective")]
        [SerializeField, Min(1f)] private float minimumArrivalRange = 10f;
        [SerializeField, Min(.5f)] private float resumeDistanceMargin = 4f;
        private NetworkPlayerQuests _quests;
        private NetworkCharacter _player;
        private Vector3[] _corners;
        private float _nextRoute;
        private NavMeshPath _path;
        private RaycastHit[] _groundHits;
        private NetworkEnemy[] _nearbyEnemies;
        private QuestDefinition _proximityQuest;
        private bool _proximityReturning, _proximityHidden;
        public QuestDefinition TargetQuest { get; private set; }
        public EnemySpawnPoint TargetSpawn { get; private set; }
        public QuestNpc TargetNpc { get; private set; }
        public float RouteLength { get; private set; }
        public bool IsReturning => TargetNpc != null;
        public bool IsVisible => arrow != null && arrow.IsVisible;

        private void Awake() => EnsureBuffers();
        private void OnEnable() { EnsureBuffers(); Invalidate(); }
        private void EnsureBuffers()
        {
            // Also survives editor hot reload and entering play mode without a scene/domain reload.
            if (_path == null) _path = new NavMeshPath();
            if (_groundHits == null) _groundHits = new RaycastHit[24];
        }

        private void Update()
        {
            EnsureBuffers();
            var player = NetworkCharacter.LocalPlayer;
            if (_player != player)
            {
                if (_quests != null) _quests.Changed -= Invalidate;
                _player = player;
                _quests = player != null ? player.GetComponent<NetworkPlayerQuests>() : null;
                if (_quests != null) _quests.Changed += Invalidate;
                ClearRoute(); Invalidate();
            }
            var attributes = _player != null ? _player.GetComponent<CharacterAttributes>() : null;
            if (arrow == null || _player == null || !_player.IsSpawned || _quests == null || !_quests.IsSpawned ||
                _quests.Catalog == null || attributes == null || !attributes.IsReady || attributes.Snapshot.IsDead)
            { ClearRoute(); return; }
            if (Time.unscaledTime >= _nextRoute)
            {
                _nextRoute = Time.unscaledTime + routeInterval;
                RebuildRoute();
            }
            var position = _player.transform.position;
            if (ShouldHideNearObjective(position) || _corners == null || _corners.Length < 2)
            { arrow.Hide(); return; }
            GetArrowPose(position, out var point, out var direction);
            Vector3 normal = Vector3.up;
            float expectedGroundHeight = point.y;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, _groundHits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = _groundHits[i];
                if (hit.distance >= nearest || Mathf.Abs(hit.point.y - expectedGroundHeight) > .7f || hit.normal.y < .25f ||
                    hit.collider.GetComponentInParent<CharacterAttributes>() != null) continue;
                nearest = hit.distance; point = hit.point; normal = hit.normal;
            }
            arrow.Show(point, direction, normal, IsReturning);
        }

        private void Invalidate() => _nextRoute = 0f;
        private bool ShouldHideNearObjective(Vector3 position)
        {
            // Keep the arrival latch across route refreshes and changes to the nearest spawn point.
            // A new quest or switching to the return journey starts a fresh proximity check.
            if (_proximityQuest != TargetQuest || _proximityReturning != IsReturning)
            {
                _proximityQuest = TargetQuest;
                _proximityReturning = IsReturning;
                _proximityHidden = false;
            }
            float margin = _proximityHidden ? resumeDistanceMargin : 0f;
            bool near = false;
            if (TargetNpc != null)
                near = Vector3.Distance(position, TargetNpc.transform.position) <= TargetNpc.InteractionRange + margin;
            else if (TargetSpawn != null && TargetQuest != null)
            {
                var ai = TargetSpawn.Definition != null ? TargetSpawn.Definition.AI : null;
                float arrivalRange = Mathf.Max(minimumArrivalRange,
                    TargetSpawn.SpawnRadius + (ai != null ? ai.DetectionRange : 0f));
                near = Vector3.Distance(position, TargetSpawn.transform.position) <= arrivalRange + margin;
                // Use replicated positions and health, not the server-only AI target. This also
                // hides guidance when a quest enemy pursues the player away from its spawn point.
                if (!near && _nearbyEnemies != null)
                    foreach (var enemy in _nearbyEnemies)
                    {
                        if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsSpawned ||
                            enemy.gameObject.scene != _player.gameObject.scene || enemy.Definition == null ||
                            enemy.Definition.EnemyId != TargetQuest.TargetEnemyId) continue;
                        var health = enemy.GetComponent<CharacterAttributes>();
                        if (health == null || !health.IsReady || health.Snapshot.IsDead) continue;
                        var enemyAi = enemy.Definition.AI;
                        float enemyRange = Mathf.Max(minimumArrivalRange, enemyAi != null ? enemyAi.DetectionRange : 0f);
                        if (Vector3.Distance(position, enemy.transform.position) > enemyRange + margin) continue;
                        near = true;
                        break;
                    }
            }
            _proximityHidden = near;
            return near;
        }

        private void ClearRoute(bool resetProximity = true)
        {
            _corners = null; TargetQuest = null; TargetSpawn = null; TargetNpc = null; RouteLength = 0f;
            if (resetProximity)
            {
                _proximityQuest = null;
                _proximityReturning = _proximityHidden = false;
                _nearbyEnemies = null;
            }
            if (arrow != null) arrow.Hide();
        }

        private void RebuildRoute()
        {
            ClearRoute(false);
            _nearbyEnemies = FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None);
            // Combat objectives take priority over earlier rewards waiting at the NPC.
            foreach (var status in new[] { QuestStatus.Active, QuestStatus.Ready })
            {
                for (int i = 0; i < _quests.Catalog.Count; i++)
                {
                    var quest = _quests.Catalog.Get(i);
                    if (quest == null || _quests.GetState(quest).Status != status) continue;
                    TargetQuest = quest;
                    if (FindDestination(status == QuestStatus.Ready)) return;
                }
            }
            ClearRoute();
        }

        private bool FindDestination(bool returning)
        {
            if (!NavMesh.SamplePosition(_player.transform.position, out var start, 2.5f, NavMesh.AllAreas)) return false;
            float shortest = float.MaxValue;
            if (returning)
            {
                foreach (var npc in FindObjectsByType<QuestNpc>())
                    if (npc.isActiveAndEnabled && npc.gameObject.scene == _player.gameObject.scene && npc.Offers(TargetQuest) &&
                        TryRoute(start.position, npc.transform.position, 4f, out var corners, out float length) && length < shortest)
                    { shortest = length; _corners = corners; TargetNpc = npc; TargetSpawn = null; }
            }
            else
            {
                foreach (var spawn in FindObjectsByType<EnemySpawnPoint>())
                    if (spawn.isActiveAndEnabled && spawn.gameObject.scene == _player.gameObject.scene && spawn.Definition != null &&
                        spawn.Definition.EnemyId == TargetQuest.TargetEnemyId &&
                        TryRoute(start.position, spawn.transform.position, spawn.SpawnRadius + 1f, out var corners, out float length) && length < shortest)
                    { shortest = length; _corners = corners; TargetSpawn = spawn; TargetNpc = null; }
            }
            RouteLength = shortest < float.MaxValue ? shortest : 0f;
            return _corners != null;
        }

        private bool TryRoute(Vector3 start, Vector3 target, float sampleRadius, out Vector3[] corners, out float length)
        {
            corners = null; length = 0f;
            if (!NavMesh.SamplePosition(target, out var end, sampleRadius, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(start, end.position, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete) return false;
            corners = _path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return corners.Length > 0;
        }

        private void GetArrowPose(Vector3 player, out Vector3 point, out Vector3 direction)
        {
            // Project onto the cached polyline, then move ahead along its corners instead of pointing through walls.
            int segment = 0; float closest = float.MaxValue;
            point = _corners[0];
            for (int i = 0; i < _corners.Length - 1; i++)
            {
                var delta = _corners[i + 1] - _corners[i];
                float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(player - _corners[i], delta) / delta.sqrMagnitude) : 0f;
                var projected = _corners[i] + delta * t;
                float distance = (player - projected).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance; segment = i; point = projected;
            }
            float remaining = lookAhead;
            direction = _corners[segment + 1] - point;
            for (int i = segment + 1; i < _corners.Length; i++)
            {
                var delta = _corners[i] - point;
                float distance = delta.magnitude;
                if (distance > .001f) direction = delta / distance;
                if (distance >= remaining) { point += direction * remaining; return; }
                point = _corners[i]; remaining -= distance;
            }
        }

        private void OnDisable()
        {
            if (_quests != null) _quests.Changed -= Invalidate;
            _player = null; _quests = null; ClearRoute();
        }
    }
}
