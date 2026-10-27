using Game.Attributes;
using Game.Attributes.Unity;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    [DefaultExecutionOrder(-1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(CharacterAttributes))]
    public sealed class NetworkAttributes : NetworkBehaviour
    {
        [SerializeField, Range(1, 30)] private int snapshotRate = 10;

        private CharacterAttributes _attributes;
        private bool _dirty;
        private bool _urgent;
        private float _nextSnapshotTime;
        private readonly NetworkVariable<NetworkAttributePublicState> _publicState =
            new NetworkVariable<NetworkAttributePublicState>(default,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkAttributeOwnerState> _ownerState =
            new NetworkVariable<NetworkAttributeOwnerState>(default,
                NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _attributes = GetComponent<CharacterAttributes>();
            _attributes.BeginReplica();
        }

        public override void OnNetworkSpawn()
        {
            _publicState.OnValueChanged += HandlePublicState;
            _ownerState.OnValueChanged += HandleOwnerState;
            if (IsServer)
            {
                if (!_attributes.InitializeAuthority()) return;
                _attributes.Changed += HandleAuthoritativeChange;
                Publish();
            }
            else
            {
                _attributes.BeginReplica();
                ApplyReplica();
            }
        }

        public override void OnNetworkDespawn()
        {
            _publicState.OnValueChanged -= HandlePublicState;
            _ownerState.OnValueChanged -= HandleOwnerState;
            _attributes.Changed -= HandleAuthoritativeChange;
            _dirty = _urgent = false;
            _attributes.BeginReplica();
        }

        public override void OnGainedOwnership()
        {
            if (IsSpawned && !IsServer) ApplyReplica();
        }

        public override void OnLostOwnership()
        {
            if (IsSpawned && !IsServer) ApplyReplica();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !_dirty || NetworkManager.ShutdownInProgress) return;
            if (_urgent || Time.unscaledTime >= _nextSnapshotTime) Publish();
        }

        private void HandleAuthoritativeChange(AttributeSnapshot snapshot)
        {
            _dirty = true;
            _urgent |= !_publicState.Value.HasState || snapshot.Health < _publicState.Value.Health;
        }

        private void Publish()
        {
            if (!_attributes.IsReady || !IsServer) return;
            AttributeSnapshot snapshot = _attributes.Snapshot;
            _publicState.Value = new NetworkAttributePublicState
            {
                HasState = true, Revision = snapshot.Revision, Health = snapshot.Health,
                MaxHealth = snapshot.Values.MaxHealth, AttackPower = snapshot.Values.AttackPower,
                Defense = snapshot.Values.Defense, MoveSpeed = snapshot.Values.MoveSpeed
            };
            _ownerState.Value = new NetworkAttributeOwnerState
            {
                HasState = true, Revision = snapshot.Revision, Stamina = snapshot.Stamina,
                MaxStamina = snapshot.Values.MaxStamina
            };
            _dirty = _urgent = false;
            _nextSnapshotTime = Time.unscaledTime + 1f / Mathf.Clamp(snapshotRate, 1, 30);
        }

        public void PublishAuthorityState()
        {
            if (IsSpawned && IsServer) Publish();
        }

        private void HandlePublicState(NetworkAttributePublicState previous, NetworkAttributePublicState next) => ApplyReplica();
        private void HandleOwnerState(NetworkAttributeOwnerState previous, NetworkAttributeOwnerState next) => ApplyReplica();

        private void ApplyReplica()
        {
            if (!IsSpawned || IsServer || !_publicState.Value.HasState) return;
            NetworkAttributePublicState state = _publicState.Value;
            bool hasStamina = IsOwner && _ownerState.Value.HasState;
            if (IsOwner && !hasStamina) return;
            NetworkAttributeOwnerState owner = hasStamina ? _ownerState.Value : default;
            var values = new AttributeValues(state.MaxHealth, owner.MaxStamina, state.AttackPower, state.Defense, state.MoveSpeed);
            _attributes.ApplyReplica(new AttributeSnapshot(values, state.Health, owner.Stamina,
                hasStamina ? owner.Revision : state.Revision, hasStamina));
        }
    }
}
