using System.Collections.Generic;
using Game.Character;
using Game.Character.Actions;
using Game.Attributes.Unity;
using Game.Combat.Unity;
using Game.Input;
using Game.Presentation.Character;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Game.Network
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkCharacter : NetworkBehaviour, ICharacterCommandReceiver, IMovementStateSource
    {
        private const float SimulationStep = 1f / 60f;
        private const int HistoryLimit = CharacterPredictionBuffer.Capacity;
        private const int PacketLimit = 32;

        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Range(10, 60)] private int commandSendRate = 30;
        [SerializeField, Min(0.2f)] private float inputTimeout = 0.5f;
        [SerializeField, Range(10, 60)] private int movementSnapshotRate = 30;
        [SerializeField, Range(0.03f, 0.25f)] private float remoteInterpolationDelay = 0.1f;
        [SerializeField, Min(1f)] private float correctionSharpness = 15f;

        private PlayerInputReader _input;
        private CharacterMotor _motor;
        private CharacterActionController _actions;
        private CharacterAttributes _attributes;
        private NetworkInventory _inventory;
        private uint EquipmentRevision => _inventory != null ? _inventory.EquipmentRevision : 0;
        private uint _lifeRevision;
        private bool _collisionDead;
        private CharacterController _controller;
        private CharacterView _view;
        private Animator _animator;
        private bool _movementConfigured;
        private CharacterPressedButtons _pendingPressed;
        private float _simulationAccumulator;
        private float _nextCommandTime;
        private float _nextSnapshotTime;
        private float _lastServerCommandTime;
        private uint _nextSequence;
        private uint _lastReceivedSequence;
        private uint _lastProcessedSequence;
        private bool _snapshotPending;
        private bool _hasReconciledSnapshot;
        private NetworkMovementSnapshot _lastReconciledSnapshot;
        private CharacterActionState _lastReconciledActions;
        private MovementState _remoteMovementState;
        private Vector3 _visualLocalPosition;
        private Quaternion _visualLocalRotation;
        private Vector3 _visualOffset;
        private Quaternion _visualRotationOffset = Quaternion.identity;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation;
        private double _simulationTime;
        private double _predictionTime;
        private CharacterMoveCommand _continuousCommand;
        private Transform _presentationRoot;
        private Transform _presentationCameraTarget;
        private CapsuleCollider _collisionProxy;
        private readonly List<ProxyPose> _savedProxies = new List<ProxyPose>();

        private readonly struct ProxyPose
        {
            public readonly Transform Target;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public ProxyPose(Transform target)
            {
                Target = target;
                Position = target.position;
                Rotation = target.rotation;
            }
        }

        private readonly CharacterPredictionBuffer _history = new CharacterPredictionBuffer();
        private readonly List<NetworkCharacterCommand> _unsent = new List<NetworkCharacterCommand>(PacketLimit);
        private readonly Queue<NetworkCharacterCommand> _serverInputs = new Queue<NetworkCharacterCommand>();
        private readonly NetworkCharacterPoseHistory _remoteSnapshots = new NetworkCharacterPoseHistory();
        private readonly NetworkVariable<NetworkMovementSnapshot> _movementSnapshot =
            new NetworkVariable<NetworkMovementSnapshot>(default,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkCharacterPredictionSnapshot> _ownerPredictionSnapshot =
            new NetworkVariable<NetworkCharacterPredictionSnapshot>(default,
                NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        public static NetworkCharacter LocalPlayer { get; private set; }
        public Transform CameraTarget => _presentationCameraTarget != null ? _presentationCameraTarget :
            cameraTarget != null ? cameraTarget : transform;
        public CharacterMoveCommand LatestCommand { get; private set; }
        public int JumpRequests { get; private set; }
        public string MovementStatus { get; private set; } = "等待网络生成。";
        public string AnimationStatus { get; private set; } = "等待网络生成。";
        public MovementState CurrentMovementState => (IsServer || IsOwner) && _motor != null
            ? _motor.CurrentMovementState : _remoteMovementState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLocalPlayer() => LocalPlayer = null;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<CharacterMotor>();
            _actions = GetComponent<CharacterActionController>();
            _attributes = GetComponent<CharacterAttributes>();
            _inventory = GetComponent<NetworkInventory>();
            _controller = GetComponent<CharacterController>();
            _view = GetComponent<CharacterView>();
            _animator = GetComponentInChildren<Animator>(true);
            if (_input != null) _input.enabled = false;
            if (_motor != null) _motor.enabled = false;
            if (_controller != null) _controller.enabled = false;
            if (_view != null) _view.enabled = false;
            if (_animator != null) _animator.enabled = false;
            if (visualRoot != null)
            {
                _visualLocalPosition = visualRoot.localPosition;
                _visualLocalRotation = visualRoot.localRotation;
            }
        }

        public override void OnNetworkSpawn()
        {
            gameObject.name = "Player_Client_" + OwnerClientId + (IsOwner ? "_Local" : "_Remote");
            if (IsOwner) LocalPlayer = this;
            _movementConfigured = _motor != null && _controller != null && _actions != null && _actions.IsConfigured &&
                GetComponent<NetworkTransform>() == null && !NetworkManager.DistributedAuthorityMode;
            if (!_movementConfigured)
            {
                MovementStatus = "预测配置不完整，请查看 Console。";
                Debug.LogError("[NetworkCharacter] 需要 CharacterMotor / CharacterController / 已配置的 CharacterActionController；请移除 NetworkTransform，使用 Client-Server 拓扑。", this);
                return;
            }

            _history.Clear();
            _unsent.Clear();
            _serverInputs.Clear();
            _remoteSnapshots.Clear();
            _nextSequence = _lastReceivedSequence = _lastProcessedSequence = 0;
            _lifeRevision = 0;
            _collisionDead = false;
            _pendingPressed = CharacterPressedButtons.None;
            _simulationAccumulator = 0f;
            _nextCommandTime = _nextSnapshotTime = 0f;
            _lastServerCommandTime = Time.unscaledTime;
            _hasReconciledSnapshot = false;
            _visualOffset = Vector3.zero;
            _visualRotationOffset = Quaternion.identity;
            _continuousCommand = default;
            _simulationTime = _predictionTime = NetworkManager.ServerTime.Time;
            _motor.ResetMovement();
            _motor.ManualSimulation = true;
            _controller.enabled = IsServer || IsOwner;
            _motor.enabled = IsServer || IsOwner;
            _movementSnapshot.OnValueChanged += HandleSnapshot;
            _ownerPredictionSnapshot.OnValueChanged += HandleOwnerSnapshot;
            if (!IsServer && !IsOwner && _movementSnapshot.Value.HasState)
            {
                _predictionTime = _movementSnapshot.Value.SimulationTime;
                HandleSnapshot(default, _movementSnapshot.Value);
            }
            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
            CreatePresentation();

            MovementStatus = IsServer ? "服务器固定步长执行移动" :
                IsOwner ? "本机预测 + 服务器确认 + 输入重放" : "服务器快照插值";
            if (_view != null && _animator != null)
            {
                _view.SetMovementStateSource(this);
                _animator.applyRootMotion = false;
                _animator.enabled = true;
                _view.enabled = true;
                AnimationStatus = IsOwner ? "读取本机运动状态" : "读取服务器快照";
            }
            else Debug.LogError("[NetworkCharacter] 请设置 CharacterView / Animator。", this);
        }

        protected override void OnNetworkPostSpawn()
        {
            if (!_movementConfigured) return;
            if (IsServer)
            {
                _actions.InitializeAuthority();
                PublishSnapshot();
            }
            else if (IsOwner && _ownerPredictionSnapshot.Value.Movement.HasState)
            {
                NetworkCharacterPredictionSnapshot snapshot = _ownerPredictionSnapshot.Value;
                _lifeRevision = snapshot.Movement.LifeRevision;
                _motor.RestoreState(snapshot.Movement.ToMotorState());
                _actions.RestorePrediction(snapshot.ToActionState());
                _actions.PublishPrediction();
                _predictionTime = snapshot.Movement.SimulationTime;
                _previousPosition = transform.position;
                _previousRotation = transform.rotation;
                _snapshotPending = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            ReleaseLocalInput();
            _movementSnapshot.OnValueChanged -= HandleSnapshot;
            _ownerPredictionSnapshot.OnValueChanged -= HandleOwnerSnapshot;
            if (_actions != null) _actions.ResetActions();
            _movementConfigured = false;
            _history.Clear();
            _unsent.Clear();
            _serverInputs.Clear();
            _remoteSnapshots.Clear();
            RestoreCollisionProxies();
            if (_collisionProxy != null)
            {
                _collisionProxy.enabled = false;
                Destroy(_collisionProxy.gameObject);
                _collisionProxy = null;
            }
            if (_presentationRoot != null) Destroy(_presentationRoot.gameObject);
            _presentationRoot = _presentationCameraTarget = null;
            if (_motor != null)
            {
                _motor.ResetMovement();
                _motor.ManualSimulation = false;
                _motor.enabled = false;
            }
            if (_controller != null) _controller.enabled = false;
            if (_view != null)
            {
                _view.enabled = false;
                _view.SetMovementStateSource(null);
            }
            if (_animator != null) _animator.enabled = false;
            if (visualRoot != null)
            {
                visualRoot.localPosition = _visualLocalPosition;
                visualRoot.localRotation = _visualLocalRotation;
            }
            if (LocalPlayer == this) LocalPlayer = null;
            MovementStatus = AnimationStatus = "网络角色已移除。";
        }

        public void BindLocalInput(Transform localCamera)
        {
            if (!IsSpawned || !IsOwner || !_movementConfigured || _input == null) return;
            _input.SetCommandReceiver(this, localCamera);
            _input.enabled = true;
        }

        public void ReleaseLocalInput()
        {
            if (_input != null)
            {
                _input.enabled = false;
                _input.SetCommandReceiver(null, null);
            }
            LatestCommand = default;
            _pendingPressed = CharacterPressedButtons.None;
        }

        public void ReceiveCommand(CharacterMoveCommand command)
        {
            if (!IsSpawned || !IsOwner) return;
            LatestCommand = command;
            _pendingPressed |= command.PressedButtons;
            if (command.JumpPressed) JumpRequests++;
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !_movementConfigured || NetworkManager.ShutdownInProgress) return;
            try
            {
                if (IsOwner && !IsServer && _snapshotPending) Reconcile();
                RefreshLifeCollision();
                if (IsServer || IsOwner)
                {
                    _simulationAccumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    while (_simulationAccumulator >= SimulationStep)
                    {
                        _simulationAccumulator -= SimulationStep;
                        if (IsOwner) GenerateInput();
                        if (IsServer) SimulateServer();
                    }
                    if (IsOwner && !IsServer) SendInputs();
                    if (IsServer && Time.unscaledTime >= _nextSnapshotTime)
                    {
                        _nextSnapshotTime = Time.unscaledTime + 1f / Mathf.Clamp(movementSnapshotRate, 10, 60);
                        PublishSnapshot();
                    }
                }
                else InterpolateRemote();
            }
            finally { RestoreCollisionProxies(); }
            if (IsOwner && !IsServer) _actions.PublishPrediction();
            if (IsOwner) SmoothCorrection();
        }

        private void GenerateInput()
        {
            if (!IsServer && (!_actions.IsReady || _history.Count >= HistoryLimit)) return;
            CharacterMoveCommand command = new CharacterMoveCommand(LatestCommand.MoveDirection,
                LatestCommand.HeldButtons, _pendingPressed, LatestCommand.LookDirection);
            _pendingPressed = CharacterPressedButtons.None;
            var frame = new NetworkCharacterCommand(unchecked(++_nextSequence), command, _lifeRevision, EquipmentRevision);
            if (IsServer)
                QueueInput(frame);
            else
            {
                _predictionTime += SimulationStep;
                PrepareCollisionProxies(_predictionTime);
                _previousPosition = transform.position;
                _previousRotation = transform.rotation;
                _actions.Simulate(command, SimulationStep);
                _history.TryAppend(frame, _motor.CaptureState(), _actions.State);
                _unsent.Add(frame);
            }
        }

        private void SendInputs()
        {
            if (_unsent.Count == 0 || Time.unscaledTime < _nextCommandTime) return;
            _nextCommandTime = Time.unscaledTime + 1f / Mathf.Clamp(commandSendRate, 10, 60);
            int count = Mathf.Min(PacketLimit, _unsent.Count);
            NetworkCharacterCommand[] packet = _unsent.GetRange(0, count).ToArray();
            SubmitCommandsRpc(packet);
            _unsent.RemoveRange(0, count);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitCommandsRpc(NetworkCharacterCommand[] commands, RpcParams rpcParams = default)
        {
            if (!IsServer || !_movementConfigured || !IsSpawned ||
                rpcParams.Receive.SenderClientId != OwnerClientId || commands == null ||
                commands.Length == 0 || commands.Length > PacketLimit) return;
            foreach (NetworkCharacterCommand command in commands) QueueInput(command);
        }

        private void QueueInput(NetworkCharacterCommand frame)
        {
            if (frame.LifeRevision != _lifeRevision || frame.EquipmentRevision != EquipmentRevision || !InputSequence.IsNext(frame.Sequence, _lastReceivedSequence) ||
                _serverInputs.Count >= HistoryLimit || !IsFinite(frame.MoveDirection.x) ||
                !IsFinite(frame.MoveDirection.y) || !IsFinite(frame.MoveDirection.z)) return;
            if ((frame.HeldButtons & ~7) != 0 || (frame.PressedButtons & ~7) != 0 ||
                !IsFinite(frame.LookDirection.x) || !IsFinite(frame.LookDirection.y) || !IsFinite(frame.LookDirection.z)) return;
            _lastReceivedSequence = frame.Sequence;
            _lastServerCommandTime = Time.unscaledTime;
            _serverInputs.Enqueue(frame);
        }

        private void SimulateServer()
        {
            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
            _simulationTime = NetworkManager.ServerTime.Time - _simulationAccumulator;
            if (_serverInputs.Count > 0)
            {
                NetworkCharacterCommand frame = _serverInputs.Dequeue();
                CharacterMoveCommand command = frame.ToCommand();
                _actions.Simulate(command, SimulationStep);
                _continuousCommand = new CharacterMoveCommand(command.MoveDirection,
                    command.HeldButtons, CharacterPressedButtons.None, command.LookDirection);
                _lastProcessedSequence = frame.Sequence;
            }
            else
                _actions.Simulate(Time.unscaledTime - _lastServerCommandTime > Mathf.Max(0.2f, inputTimeout)
                    ? default : _continuousCommand, SimulationStep);
        }

        private void PublishSnapshot()
        {
            var movement = new NetworkMovementSnapshot(_motor.CaptureState(),
                _lastProcessedSequence, NetworkManager.ServerTime.Time, _simulationTime,
                new NetworkCombatState(_actions.State.Combat), _lifeRevision, EquipmentRevision);
            _movementSnapshot.Value = movement;
            _ownerPredictionSnapshot.Value = new NetworkCharacterPredictionSnapshot(movement, _actions.State);
        }

        private void HandleSnapshot(NetworkMovementSnapshot previous, NetworkMovementSnapshot next)
        {
            if (!next.HasState || IsServer || next.EquipmentRevision != EquipmentRevision) return;
            if (!IsOwner)
            {
                if (next.LifeRevision != _lifeRevision)
                {
                    _lifeRevision = next.LifeRevision;
                    _remoteSnapshots.Clear();
                    transform.SetPositionAndRotation(next.Position, next.Rotation);
                    if (_collisionProxy != null) _collisionProxy.transform.SetPositionAndRotation(next.Position, next.Rotation);
                }
                _remoteSnapshots.Add(next);
                GetComponent<CharacterCombatController>()?.RestoreState(next.Combat.Value);
            }
        }

        private void HandleOwnerSnapshot(NetworkCharacterPredictionSnapshot previous, NetworkCharacterPredictionSnapshot next)
        {
            if (!IsOwner || IsServer || !next.Movement.HasState || next.Movement.EquipmentRevision != EquipmentRevision) return;
            if (next.Movement.LifeRevision != _lifeRevision) ApplyOwnerRespawn(next);
            _snapshotPending = true;
        }

        public void RefreshEquipmentSimulation()
        {
            if (!IsSpawned) return;
            _history.Clear(); _unsent.Clear(); _serverInputs.Clear(); _remoteSnapshots.Clear();
            _nextSequence = _lastReceivedSequence = _lastProcessedSequence = 0;
            _pendingPressed = CharacterPressedButtons.None;
            LatestCommand = _continuousCommand = default;
            _snapshotPending = _hasReconciledSnapshot = false;
            _simulationAccumulator = 0;
            _simulationTime = _predictionTime = NetworkManager.ServerTime.Time;
            _previousPosition = transform.position; _previousRotation = transform.rotation;
            if (IsServer && _movementConfigured && _actions.IsReady) PublishSnapshot();
        }

        public bool TryRespawn(Vector3 position, Quaternion rotation)
        {
            if (!IsSpawned || !IsServer || !_movementConfigured || !_attributes.TryGetAuthority(out var service) ||
                !service.Snapshot.IsDead) return false;
            _lifeRevision = unchecked(_lifeRevision + 1);
            ClearLifeInput();
            _controller.enabled = true;
            _motor.ResetMovement();
            _motor.RestoreState(new CharacterMotorState(position, rotation, Vector3.zero, 0f, false, false));
            service.Revive(service.Snapshot.Values.MaxHealth);
            service.RestoreStamina(service.Snapshot.Values.MaxStamina);
            _actions.InitializeAuthority();
            _previousPosition = position;
            _previousRotation = rotation;
            _simulationTime = _predictionTime = NetworkManager.ServerTime.Time;
            _collisionDead = false;
            Physics.SyncTransforms();
            PublishSnapshot();
            GetComponent<NetworkAttributes>()?.PublishAuthorityState();
            return true;
        }

        private void ClearLifeInput()
        {
            RestoreCollisionProxies();
            _history.Clear();
            _unsent.Clear();
            _serverInputs.Clear();
            _remoteSnapshots.Clear();
            _nextSequence = _lastReceivedSequence = _lastProcessedSequence = 0;
            LatestCommand = _continuousCommand = default;
            _pendingPressed = CharacterPressedButtons.None;
            _simulationAccumulator = 0f;
            _lastServerCommandTime = Time.unscaledTime;
            _nextCommandTime = _nextSnapshotTime = 0f;
            _hasReconciledSnapshot = _snapshotPending = false;
            _visualOffset = Vector3.zero;
            _visualRotationOffset = Quaternion.identity;
        }

        private void ApplyOwnerRespawn(NetworkCharacterPredictionSnapshot snapshot)
        {
            _lifeRevision = snapshot.Movement.LifeRevision;
            ClearLifeInput();
            _controller.enabled = true;
            _motor.RestoreState(snapshot.Movement.ToMotorState());
            _actions.RestorePrediction(snapshot.ToActionState());
            _actions.PublishPrediction();
            _previousPosition = snapshot.Movement.Position;
            _previousRotation = snapshot.Movement.Rotation;
            _predictionTime = snapshot.Movement.SimulationTime;
            _collisionDead = false;
            Physics.SyncTransforms();
        }

        private void RefreshLifeCollision()
        {
            bool dead = _attributes != null && _attributes.IsReady && _attributes.Snapshot.IsDead;
            if (dead && !_collisionDead && (IsServer || IsOwner))
            {
                CharacterMotorState state = _motor.CaptureState();
                _motor.RestoreState(new CharacterMotorState(state.Position, state.Rotation, Vector3.zero,
                    0f, state.IsGrounded, false, state.KnockbackVelocity, state.KnockbackRemaining));
            }
            _collisionDead = dead;
            bool movingCorpse = (IsServer || IsOwner) ? _motor.IsKnockedBack : _movementSnapshot.Value.KnockbackRemaining > 0f;
            _controller.enabled = (IsServer || IsOwner) && (!dead || movingCorpse);
            if (_collisionProxy != null) _collisionProxy.enabled = !dead || movingCorpse;
        }

        private void Reconcile()
        {
            _snapshotPending = false;
            NetworkCharacterPredictionSnapshot confirmation = _ownerPredictionSnapshot.Value;
            NetworkMovementSnapshot snapshot = confirmation.Movement;
            CharacterActionState confirmedActions = confirmation.ToActionState();
            if (!snapshot.HasState || snapshot.EquipmentRevision != EquipmentRevision || InputSequence.IsNewer(snapshot.AcknowledgedSequence, _nextSequence)) return;
            if (_hasReconciledSnapshot && snapshot.AcknowledgedSequence == _lastReconciledSnapshot.AcknowledgedSequence &&
                snapshot.Position.Equals(_lastReconciledSnapshot.Position) &&
                snapshot.Rotation.Equals(_lastReconciledSnapshot.Rotation) &&
                snapshot.MotorVerticalVelocity.Equals(_lastReconciledSnapshot.MotorVerticalVelocity) &&
                snapshot.KnockbackVelocity.Equals(_lastReconciledSnapshot.KnockbackVelocity) &&
                snapshot.KnockbackRemaining.Equals(_lastReconciledSnapshot.KnockbackRemaining) &&
                snapshot.IsGrounded == _lastReconciledSnapshot.IsGrounded &&
                confirmedActions.Equals(_lastReconciledActions)) return;
            if (_hasReconciledSnapshot &&
                snapshot.ServerTime <= _lastReconciledSnapshot.ServerTime) return;

            bool matched = _history.Acknowledge(snapshot.AcknowledgedSequence, out CharacterMotorState predictedResult,
                out CharacterActionState predictedActions) &&
                Vector3.Distance(predictedResult.Position, snapshot.Position) < 0.015f &&
                Quaternion.Angle(predictedResult.Rotation, snapshot.Rotation) < 1f &&
                Mathf.Abs(predictedResult.VerticalVelocity - snapshot.MotorVerticalVelocity) < 0.1f &&
                predictedResult.IsGrounded == snapshot.IsGrounded &&
                Vector3.Distance(predictedResult.KnockbackVelocity, snapshot.KnockbackVelocity) < .01f &&
                Mathf.Abs(predictedResult.KnockbackRemaining - snapshot.KnockbackRemaining) < .005f &&
                Mathf.Abs(predictedActions.Stamina - confirmedActions.Stamina) < .005f &&
                Mathf.Abs(predictedActions.RecoveryDelayRemaining - confirmedActions.RecoveryDelayRemaining) < .005f &&
                predictedActions.MaxStamina.Equals(confirmedActions.MaxStamina) &&
                predictedActions.MoveSpeed.Equals(confirmedActions.MoveSpeed) &&
                predictedActions.SprintExhausted == confirmedActions.SprintExhausted &&
                predictedActions.IsDead == confirmedActions.IsDead && predictedActions.Combat.Matches(confirmedActions.Combat);
            _hasReconciledSnapshot = true;
            _lastReconciledSnapshot = snapshot;
            _lastReconciledActions = confirmedActions;
            _predictionTime = snapshot.SimulationTime + _history.Count * (double)SimulationStep;
            if (matched) return;

            Vector3 oldPosition = _presentationRoot != null ? _presentationRoot.position : transform.position;
            Quaternion oldRotation = _presentationRoot != null ? _presentationRoot.rotation : transform.rotation;
            _motor.RestoreState(snapshot.ToMotorState());
            _actions.RestorePrediction(confirmedActions);
            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
            Physics.SyncTransforms();
            for (int i = 0; i < _history.Count; i++)
            {
                CharacterPredictionBuffer.Entry predicted = _history[i];
                PrepareCollisionProxies(snapshot.SimulationTime + (i + 1) * (double)SimulationStep);
                _previousPosition = transform.position;
                _previousRotation = transform.rotation;
                _actions.Simulate(predicted.Frame.ToCommand(), SimulationStep);
                _history.ReplaceResult(i, _motor.CaptureState(), _actions.State);
            }
            GetRenderPose(out Vector3 renderPosition, out Quaternion renderRotation);
            Vector3 correction = oldPosition - renderPosition;
            if (correction.sqrMagnitude > 4f)
            {
                _visualOffset = Vector3.zero;
                _visualRotationOffset = Quaternion.identity;
            }
            else
            {
                _visualOffset = correction;
                _visualRotationOffset = oldRotation * Quaternion.Inverse(renderRotation);
            }
        }

        private void SmoothCorrection()
        {
            float factor = 1f - Mathf.Exp(-correctionSharpness * Time.unscaledDeltaTime);
            _visualOffset = Vector3.Lerp(_visualOffset, Vector3.zero, factor);
            _visualRotationOffset = Quaternion.Slerp(_visualRotationOffset, Quaternion.identity, factor);
            GetRenderPose(out Vector3 position, out Quaternion rotation);
            position += _visualOffset;
            rotation = _visualRotationOffset * rotation;
            position = SeparatePresentation(position, rotation);
            if (_presentationRoot != null) _presentationRoot.SetPositionAndRotation(position, rotation);
            if (visualRoot != null && visualRoot != transform)
                visualRoot.SetPositionAndRotation(position + rotation * Vector3.Scale(_visualLocalPosition, transform.lossyScale),
                    rotation * _visualLocalRotation);
        }

        private void InterpolateRemote()
        {
            double renderTime = NetworkManager.ServerTime.Time - remoteInterpolationDelay;
            if (!_remoteSnapshots.TrySample(renderTime, false, out CharacterMotorState state)) return;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
            if (_collisionProxy != null)
                _collisionProxy.transform.SetPositionAndRotation(state.Position, state.Rotation);
            _remoteMovementState = state.ToMovementState();
        }

        private void GetRenderPose(out Vector3 position, out Quaternion rotation)
        {
            float factor = Mathf.Clamp01(_simulationAccumulator / SimulationStep);
            position = Vector3.Lerp(_previousPosition, transform.position, factor);
            rotation = Quaternion.Slerp(_previousRotation, transform.rotation, factor);
        }

        private void CreatePresentation()
        {
            if (IsOwner)
            {
                _presentationRoot = new GameObject("NetworkPresentation").transform;
                _presentationRoot.SetParent(transform, false);
                _presentationCameraTarget = new GameObject("CameraTarget").transform;
                _presentationCameraTarget.SetParent(_presentationRoot, false);
                if (cameraTarget != null)
                {
                    _presentationCameraTarget.localPosition = transform.InverseTransformPoint(cameraTarget.position);
                    _presentationCameraTarget.localRotation = Quaternion.Inverse(transform.rotation) * cameraTarget.rotation;
                }
            }
            else if (!IsServer)
            {
                var proxy = new GameObject("RemoteCharacterCollision");
                proxy.layer = gameObject.layer;
                proxy.transform.SetParent(transform, false);
                _collisionProxy = proxy.AddComponent<CapsuleCollider>();
                _collisionProxy.direction = 1;
                _collisionProxy.center = _controller.center;
                _collisionProxy.radius = _controller.radius;
                _collisionProxy.height = _controller.height;
                _collisionProxy.isTrigger = false;
            }
        }

        private void PrepareCollisionProxies(double time)
        {
            bool changed = false;
            foreach (NetworkObject spawned in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (!spawned.TryGetComponent(out NetworkCharacter other) || other == this ||
                    other._collisionProxy == null || !other._collisionProxy.enabled ||
                    !other._remoteSnapshots.TrySample(time, true, out CharacterMotorState state)) continue;
                Transform target = other._collisionProxy.transform;
                bool saved = false;
                foreach (ProxyPose pose in _savedProxies)
                    if (pose.Target == target) { saved = true; break; }
                if (!saved) _savedProxies.Add(new ProxyPose(target));
                target.SetPositionAndRotation(state.Position, state.Rotation);
                changed = true;
            }
            if (changed) Physics.SyncTransforms();
        }

        private void RestoreCollisionProxies()
        {
            if (_savedProxies.Count == 0) return;
            foreach (ProxyPose pose in _savedProxies)
                if (pose.Target != null) pose.Target.SetPositionAndRotation(pose.Position, pose.Rotation);
            _savedProxies.Clear();
            Physics.SyncTransforms();
        }

        private Vector3 SeparatePresentation(Vector3 position, Quaternion rotation)
        {
            Vector3 scale = transform.lossyScale;
            Vector3 centerOffset = rotation * Vector3.Scale(_controller.center, scale);
            float radius = _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(radius * 2f, _controller.height * Mathf.Abs(scale.y));
            for (int pass = 0; pass < 2; pass++)
            foreach (NetworkObject spawned in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (!spawned.TryGetComponent(out NetworkCharacter other) || other == this ||
                    !other._movementConfigured || other._controller == null ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer) ||
                    Physics.GetIgnoreCollision(_controller, other._collisionProxy != null
                        ? (Collider)other._collisionProxy : other._controller)) continue;
                Transform otherPose = other._presentationRoot != null ? other._presentationRoot : other.transform;
                Vector3 otherScale = other.transform.lossyScale;
                float otherRadius = other._controller.radius * Mathf.Max(Mathf.Abs(otherScale.x), Mathf.Abs(otherScale.z));
                float otherHeight = Mathf.Max(otherRadius * 2f, other._controller.height * Mathf.Abs(otherScale.y));
                Vector3 otherCenter = otherPose.position + otherPose.rotation * Vector3.Scale(other._controller.center, otherScale);
                position = CharacterPresentationCollision.Separate(position, centerOffset, radius, height,
                    otherCenter, otherRadius, otherHeight, NetworkObjectId > other.NetworkObjectId);
            }
            return position;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
