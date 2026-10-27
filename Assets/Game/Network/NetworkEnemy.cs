using Game.Attributes.Unity;
using Game.Character;
using Game.Character.Actions;
using Game.Combat.Unity;
using Game.Enemies.Unity;
using Game.Presentation.Character;
using Game.Presentation.UI;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(NetworkObject), typeof(CharacterMotor), typeof(CharacterAttributes))]
    public sealed class NetworkEnemy : NetworkBehaviour, IMovementStateSource, ICharacterCommandReceiver
    {
        [SerializeField] private EnemyCatalog catalog;
        [SerializeField, Range(10, 30)] private int snapshotRate = 20;
        [SerializeField, Range(.03f, .25f)] private float interpolationDelay = .1f;
        [SerializeField] private Collider[] bodyColliders;
        private readonly NetworkVariable<int> _definitionIndex = new NetworkVariable<int>(0);
        private readonly NetworkVariable<NetworkMovementSnapshot> _snapshot = new NetworkVariable<NetworkMovementSnapshot>();
        private readonly NetworkCharacterPoseHistory _history = new NetworkCharacterPoseHistory();
        private CharacterMotor _motor;
        private CharacterAttributes _attributes;
        private CharacterActionController _actions;
        private CharacterCombatController _combat;
        private CharacterController _controller;
        private CapsuleCollider _proxy;
        private EnemyBrain _brain;
        private EnemyDefinition _pendingDefinition;
        private MovementState _renderState;
        private CharacterMoveCommand _command;
        private CharacterPressedButtons _pressed;
        private float _accumulator, _nextSnapshot;
        private double _removeAt;
        private bool _killCredited;
        public EnemyDefinition Definition { get; private set; }
        public EnemyCatalog Catalog => catalog;
        public MovementState CurrentMovementState => IsServer ? _motor.CurrentMovementState : _renderState;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _motor.ManualSimulation = true;
            _attributes = GetComponent<CharacterAttributes>();
            _actions = GetComponent<CharacterActionController>();
            _combat = GetComponent<CharacterCombatController>();
            _combat.DamageReceived += CreditKillingBlow;
            _controller = GetComponent<CharacterController>();
            _proxy = GetComponent<CapsuleCollider>();
            _brain = GetComponent<EnemyBrain>();
            _controller.enabled = false;
            if (_proxy != null) _proxy.enabled = false;
            SetBodyColliders(false);
            if (catalog != null) ApplyDefinition(0);
        }

        public void Configure(EnemyDefinition definition)
        {
            if (IsSpawned || definition == null || catalog == null || catalog.IndexOf(definition) < 0)
                throw new System.InvalidOperationException("Configure an unspawned enemy using a catalog entry.");
            _pendingDefinition = definition;
            ApplyDefinition(catalog.IndexOf(definition));
        }

        public override void OnNetworkSpawn()
        {
            _accumulator = _nextSnapshot = 0f; _removeAt = 0d;
            _killCredited = false;
            _command = default; _pressed = default; _history.Clear();
            _definitionIndex.OnValueChanged += HandleDefinition;
            _snapshot.OnValueChanged += HandleSnapshot;
            if (IsServer && _pendingDefinition != null) _definitionIndex.Value = catalog.IndexOf(_pendingDefinition);
            ApplyDefinition(_definitionIndex.Value);
            _controller.enabled = IsServer;
            if (_proxy != null) _proxy.enabled = !IsServer;
            SetBodyColliders(true);
            GetComponent<CharacterView>()?.SetMovementStateSource(this);
        }

        protected override void OnNetworkPostSpawn()
        {
            if (IsServer)
            {
                _actions.InitializeAuthority();
                _brain?.Begin(Definition.AI, true);
                Publish();
            }
            else if (_snapshot.Value.HasState)
            {
                HandleSnapshot(default, _snapshot.Value);
                transform.SetPositionAndRotation(_snapshot.Value.Position, _snapshot.Value.Rotation);
                _renderState = _snapshot.Value.ToMovementState();
            }
            if (!IsServer) _brain?.Begin(Definition.AI, false);
        }

        private void ApplyDefinition(int index)
        {
            var definition = catalog != null ? catalog.Get(index) : null;
            if (definition == null) { Debug.LogError("[NetworkEnemy] 找不到敌人目录条目。", this); return; }
            if (Definition == definition) return;
            Definition = definition;
            _attributes.ConfigureDefinition(definition.Attributes);
            _actions.ConfigureDefinition(definition.Actions);
            if (_combat.CurrentWeapon != definition.Weapon) _combat.ConfigureDefaultWeapon(definition.Weapon);
            GetComponent<CharacterFaction>()?.Configure(definition.Team);
            GetComponentInChildren<EnemyHealthBar>()?.SetDisplayName(definition.DisplayName);
        }

        private void HandleDefinition(int previous, int next) => ApplyDefinition(next);
        private void HandleSnapshot(NetworkMovementSnapshot previous, NetworkMovementSnapshot next)
        {
            if (IsServer || !next.HasState) return;
            _history.Add(next);
            _combat.RestoreState(next.Combat.Value);
        }

        public void ReceiveCommand(CharacterMoveCommand command)
        {
            if (!IsSpawned || !IsServer || _attributes.Snapshot.IsDead) return;
            _command = new CharacterMoveCommand(command.MoveDirection, command.HeldButtons, CharacterPressedButtons.None, command.LookDirection);
            _pressed |= command.PressedButtons;
        }

        private void Update()
        {
            if (!IsSpawned || NetworkManager.ShutdownInProgress || Definition == null) return;
            bool dead = _attributes.IsReady && _attributes.Snapshot.IsDead;
            bool movingCorpse = IsServer ? _motor.IsKnockedBack : _snapshot.Value.KnockbackRemaining > 0f;
            _controller.enabled = IsServer && (!dead || movingCorpse);
            if (_proxy != null) _proxy.enabled = !IsServer && (!dead || movingCorpse);
            SetBodyColliders(!dead);
            if (!IsServer) return;
            if (dead)
            {
                _command = default; _pressed = default;
                if (_removeAt <= 0d) _removeAt = NetworkManager.ServerTime.Time + Definition.CorpseLifetime;
                if (NetworkManager.ServerTime.Time >= _removeAt) { NetworkObject.Despawn(); return; }
            }
            _accumulator += Time.unscaledDeltaTime;
            int steps = 0;
            while (_accumulator >= 1f / 60f && steps++ < 8)
            {
                if (_brain != null && Definition.AI != null)
                {
                    _command = _brain.Tick(1f / 60f, dead, _attributes.Snapshot.Values.MoveSpeed);
                    _pressed |= _command.PressedButtons;
                }
                _actions.Simulate(new CharacterMoveCommand(_command.MoveDirection, _command.HeldButtons, _pressed, _command.LookDirection), 1f / 60f);
                _brain?.SyncNavigation();
                _pressed = default;
                _accumulator -= 1f / 60f;
            }
            if (Time.unscaledTime >= _nextSnapshot) Publish();
        }

        private void Publish()
        {
            _snapshot.Value = new NetworkMovementSnapshot(_motor.CaptureState(), 0, NetworkManager.ServerTime.Time,
                NetworkManager.ServerTime.Time, new NetworkCombatState(_combat.State));
            _nextSnapshot = Time.unscaledTime + 1f / snapshotRate;
        }

        private void LateUpdate()
        {
            if (IsSpawned && !IsServer && _history.TrySample(NetworkManager.ServerTime.Time - interpolationDelay, false, out var state))
            { transform.SetPositionAndRotation(state.Position, state.Rotation); _renderState = state.ToMovementState(); }
        }

        public override void OnNetworkDespawn()
        {
            _definitionIndex.OnValueChanged -= HandleDefinition;
            _snapshot.OnValueChanged -= HandleSnapshot;
            _actions.ResetActions(); _history.Clear(); _command = default; _pressed = default;
            _brain?.Stop();
            _controller.enabled = false;
            if (_proxy != null) _proxy.enabled = false;
            SetBodyColliders(false);
        }

        private void SetBodyColliders(bool enabled)
        {
            if (bodyColliders == null) return;
            foreach (var body in bodyColliders)
            {
                if (body == null) continue;
                if (body.enabled == enabled) continue;
                body.enabled = enabled;
                // The motor moves this body; it must not collide with its own shell.
                if (enabled && _controller != null && _controller.enabled)
                    Physics.IgnoreCollision(_controller, body, true);
            }
        }

        private void CreditKillingBlow(CharacterCombatController source, float applied)
        {
            if (_killCredited || !IsSpawned || !IsServer || Definition == null || !_attributes.Snapshot.IsDead || source == null) return;
            _killCredited = true;
            var player = source.GetComponent<NetworkCharacter>();
            if (player != null && player.IsSpawned && player.IsServer)
                player.GetComponent<NetworkPlayerQuests>()?.RecordKill(Definition.EnemyId);
        }

        [ContextMenu("Development/Attack nearest player")]
        private void AttackNearestPlayer()
        {
            if (!IsSpawned || !IsServer || _attributes.Snapshot.IsDead) return;
            NetworkCharacter nearest = null; float distance = float.MaxValue;
            foreach (var player in FindObjectsByType<NetworkCharacter>())
            {
                var attributes = player.GetComponent<CharacterAttributes>();
                if (!attributes.IsReady || attributes.Snapshot.IsDead) continue;
                float candidate = (player.transform.position - transform.position).sqrMagnitude;
                if (candidate < distance) { nearest = player; distance = candidate; }
            }
            if (nearest != null) ReceiveCommand(new CharacterMoveCommand(Vector3.zero, CharacterHeldButtons.None,
                CharacterPressedButtons.Attack, Vector3.ProjectOnPlane(nearest.transform.position - transform.position, Vector3.up).normalized));
        }
    }
}
