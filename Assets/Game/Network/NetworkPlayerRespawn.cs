using Game.Attributes.Unity;
using Game.Character;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkCharacter), typeof(CharacterAttributes))]
    public sealed class NetworkPlayerRespawn : NetworkBehaviour, IRespawnReadModel
    {
        [SerializeField, Min(.1f)] private float respawnDelay = 5f;
        private readonly NetworkVariable<double> _respawnAt = new NetworkVariable<double>(0d,
            NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        private CharacterAttributes _attributes;
        private NetworkCharacter _character;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        public bool IsDead => IsSpawned && _attributes != null && _attributes.IsReady && _attributes.Snapshot.IsDead;
        public float RemainingSeconds => !IsDead ? 0f : _respawnAt.Value > 0d
            ? Mathf.Max(0f, (float)(_respawnAt.Value - NetworkManager.ServerTime.Time)) : respawnDelay;

        private void Awake()
        {
            _attributes = GetComponent<CharacterAttributes>();
            _character = GetComponent<NetworkCharacter>();
        }

        protected override void OnNetworkPostSpawn()
        {
            if (!IsServer) return;
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _respawnAt.Value = 0d;
            _attributes.Died += HandleDeath;
            if (IsDead) HandleDeath();
        }

        public override void OnNetworkDespawn() => _attributes.Died -= HandleDeath;

        private void HandleDeath()
        {
            if (!IsServer || !IsSpawned || _respawnAt.Value > 0d) return;
            _respawnAt.Value = NetworkManager.ServerTime.Time + Mathf.Max(.1f, respawnDelay);
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || NetworkManager.ShutdownInProgress || _respawnAt.Value <= 0d) return;
            if (!IsDead)
            {
                _respawnAt.Value = 0d;
                return;
            }
            if (NetworkManager.ServerTime.Time < _respawnAt.Value) return;
            Vector3 position = _initialPosition;
            Quaternion rotation = _initialRotation;
            var spawner = FindFirstObjectByType<NetworkPlayerSpawner>();
            if (spawner != null && spawner.TryGetRespawnPose(OwnerClientId, out Vector3 configuredPosition, out Quaternion configuredRotation))
            {
                position = configuredPosition;
                rotation = configuredRotation;
            }
            if (_character.TryRespawn(position, rotation)) _respawnAt.Value = 0d;
        }
    }
}
