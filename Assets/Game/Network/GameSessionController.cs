using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    public enum GameSessionState
    {
        MainMenu,
        Starting,
        Loading,
        Playing,
        Returning,
        Failed
    }

    public enum GameSessionMode
    {
        SinglePlayer,
        OnlineHost,
        OnlineClient
    }

    [DisallowMultipleComponent]
    public sealed class GameSessionController : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private NetworkSceneLoader sceneLoader;
        [SerializeField] private NetworkPlayerSpawner playerSpawner;
        [SerializeField] private OnlineRoomService onlineRoomService;
        [SerializeField] private string mainMenuScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        [SerializeField, Min(5f)] private float startupTimeoutSeconds = 45f;

        private Coroutine _transition;
        private bool _singleConfigurationApplied;
        private bool _originalConnectionApproval;
        private UnityTransport.ConnectionAddressData _originalConnectionData;
        private string _transportFailure;

        public GameSessionState State { get; private set; } = GameSessionState.MainMenu;
        public string Status { get; private set; } = string.Empty;
        public GameSessionMode Mode { get; private set; }
        public bool IsOnline => Mode != GameSessionMode.SinglePlayer;
        public string RoomCode => IsOnline && onlineRoomService != null ? onlineRoomService.JoinCode : string.Empty;
        public bool CanStartSinglePlayer => State == GameSessionState.MainMenu && _transition == null &&
            networkManager != null && !networkManager.IsListening && !networkManager.ShutdownInProgress;
        public bool CanStartOnline => CanStartSinglePlayer;

        private void OnEnable()
        {
            if (networkManager != null) networkManager.OnTransportFailure += HandleTransportFailure;
        }

        private void OnDisable()
        {
            if (networkManager != null) networkManager.OnTransportFailure -= HandleTransportFailure;
        }

        private void Update()
        {
            if (State == GameSessionState.Playing && _transition == null &&
                (networkManager == null || !networkManager.IsListening || _transportFailure != null || RoomFailure != null))
                _transition = StartCoroutine(FailSession(RoomFailure ?? _transportFailure ?? "游戏会话意外结束，请返回主界面重新开始。"));
        }

        public void StartSinglePlayer()
        {
            if (State != GameSessionState.MainMenu || _transition != null) return;
            if (!ValidateConfiguration(out string error))
            {
                Status = error;
                return;
            }
            if (!CanStartSinglePlayer) return;
            Mode = GameSessionMode.SinglePlayer;
            _transition = StartCoroutine(StartSinglePlayerRoutine());
        }

        private string RoomFailure => IsOnline && onlineRoomService != null ? onlineRoomService.FailureReason : null;

        public void CreateOnlineRoom() => StartOnlineRoom(true, null);

        public void JoinOnlineRoom(string code)
        {
            string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.Length == 0 || normalized.Length > 16)
            {
                Status = "请输入有效的房间加入码。";
                return;
            }
            foreach (char character in normalized)
            {
                if ((character < 'A' || character > 'Z') && (character < '0' || character > '9'))
                {
                    Status = "加入码只能包含英文字母和数字。";
                    return;
                }
            }
            StartOnlineRoom(false, normalized);
        }

        private void StartOnlineRoom(bool create, string code)
        {
            if (State != GameSessionState.MainMenu || _transition != null) return;
            if (!ValidateConfiguration(out string error))
            {
                Status = error;
                return;
            }
            if (onlineRoomService == null || !onlineRoomService.isActiveAndEnabled || onlineRoomService.gameObject != gameObject)
            {
                Status = "请在 NetworkRoot 添加 Online Room Service，并设置会话控制器的引用。";
                return;
            }
            if (!onlineRoomService.ValidateConfiguration(networkManager, out error))
            {
                Status = error;
                return;
            }
            if (!CanStartOnline) return;
            Mode = create ? GameSessionMode.OnlineHost : GameSessionMode.OnlineClient;
            _transition = StartCoroutine(StartOnlineRoomRoutine(create, code));
        }

        private IEnumerator StartOnlineRoomRoutine(bool create, string code)
        {
            State = GameSessionState.Starting;
            Status = create ? "正在登录并创建互联网房间……" : "正在登录并加入房间……";
            _transportFailure = null;
            yield return null;

            Task operation = create ? onlineRoomService.CreateRoomAsync() : onlineRoomService.JoinRoomAsync(code);
            while (!operation.IsCompleted) yield return null;
            if (operation.IsFaulted || operation.IsCanceled)
            {
                string error = operation.Exception != null
                    ? OnlineRoomService.DescribeError(operation.Exception)
                    : "房间操作已取消。";
                if (operation.Exception != null) Debug.LogException(operation.Exception.GetBaseException(), this);
                yield return FailSession(error);
                yield break;
            }

            if (!networkManager.IsConnectedClient || (create && !networkManager.IsHost))
            {
                yield return FailSession("房间已创建或加入，但游戏连接未成功，请查看 Console。");
                yield break;
            }
            yield return EnterWorld(create);
        }

        private bool ValidateConfiguration(out string error)
        {
            error = null;
            if (networkManager == null || transport == null || sceneLoader == null || playerSpawner == null)
                error = "请设置 Game Session Controller 的四个组件引用。";
            else if (transform.parent != null || networkManager.gameObject != gameObject ||
                     transport.gameObject != gameObject || sceneLoader.gameObject != gameObject ||
                     playerSpawner.gameObject != gameObject)
                error = "会话组件必须位于同一个顶层 NetworkRoot。";
            else if (!networkManager.isActiveAndEnabled || !transport.isActiveAndEnabled ||
                     !sceneLoader.isActiveAndEnabled || !playerSpawner.isActiveAndEnabled)
                error = "请启用 NetworkRoot 上的网络、场景加载和玩家生成组件。";
            else if (networkManager.NetworkConfig == null ||
                     networkManager.NetworkConfig.NetworkTransport != transport || networkManager.DistributedAuthorityMode)
                error = "请使用 Client-Server 拓扑，并将 Network Transport 设置为此 UnityTransport。";
            else if (networkManager.ConnectionApprovalCallback != null)
                error = "连接审批已被其他组件占用，无法启动单人会话。";
            else if (!Application.CanStreamedLevelBeLoaded(mainMenuScenePath))
                error = "Bootstrap 不在启用的构建场景列表中，请检查 Build Profiles。";
            else if (!SceneManager.GetSceneByPath(mainMenuScenePath).isLoaded)
                error = "请从 Bootstrap 主界面启动游戏。";
            else if (!sceneLoader.ValidateConfiguration(out error))
                return false;
            else if (!playerSpawner.ValidateConfiguration(out error))
                return false;
            return error == null;
        }

        private IEnumerator StartSinglePlayerRoutine()
        {
            State = GameSessionState.Starting;
            Status = "正在启动单人游戏……";
            _transportFailure = null;
            yield return null;

            if (!TryStartLocalHost(out string error))
            {
                yield return FailSession(error);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + startupTimeoutSeconds;
            while (!networkManager.IsHost || !networkManager.IsConnectedClient)
            {
                if (_transportFailure != null || Time.realtimeSinceStartup >= deadline)
                {
                    yield return FailSession(_transportFailure ?? "单人游戏启动超时。请查看 Console。");
                    yield break;
                }
                yield return null;
            }

            yield return EnterWorld(true);
        }

        private IEnumerator EnterWorld(bool loadAsHost)
        {
            State = GameSessionState.Loading;
            Status = "正在进入世界……";
            if (loadAsHost && !sceneLoader.TryLoadPlayground())
            {
                yield return FailSession(sceneLoader.Status);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + startupTimeoutSeconds;
            while (true)
            {
                if (_transportFailure != null || RoomFailure != null || !networkManager.IsListening || networkManager.ShutdownInProgress)
                {
                    yield return FailSession(RoomFailure ?? _transportFailure ?? "进入世界时游戏会话已停止。");
                    yield break;
                }
                NetworkCharacter local = NetworkCharacter.LocalPlayer;
                if (sceneLoader.IsPlaygroundLoaded && local != null && local.IsSpawned && local.IsOwner)
                {
                    State = GameSessionState.Playing;
                    Status = string.Empty;
                    _transition = null;
                    yield break;
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return FailSession("进入世界超时，请检查 Console 中的场景和角色生成配置。");
                    yield break;
                }
                yield return null;
            }
        }

        private bool TryStartLocalHost(out string error)
        {
            error = null;
            _originalConnectionApproval = networkManager.NetworkConfig.ConnectionApproval;
            _originalConnectionData = transport.ConnectionData;
            _singleConfigurationApplied = true;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveSinglePlayer;
            transport.SetConnectionData(true, "127.0.0.1", 0, "127.0.0.1");
            try
            {
                if (networkManager.StartHost()) return true;
                error = "单人游戏启动失败，请查看 Console。";
            }
            catch (Exception exception)
            {
                error = "单人游戏启动失败：" + exception.Message;
                Debug.LogException(exception, this);
            }
            return false;
        }

        private void ApproveSinglePlayer(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = request.ClientNetworkId == NetworkManager.ServerClientId;
            response.CreatePlayerObject = false;
            response.Reason = response.Approved ? string.Empty : "这是单人游戏，不能加入。";
            response.Pending = false;
        }

        public void ReturnToMainMenu()
        {
            if (_transition != null || State == GameSessionState.MainMenu) return;
            _transition = StartCoroutine(ReturnToMainMenuRoutine());
        }

        private IEnumerator StopSession()
        {
            NetworkCharacter.LocalPlayer?.ReleaseLocalInput();
            if (IsOnline && onlineRoomService != null && onlineRoomService.HasRoom)
            {
                Task leave = onlineRoomService.CloseRoomAsync();
                while (!leave.IsCompleted) yield return null;
                if (leave.IsFaulted) Debug.LogWarning("退出云房间失败，本地会话仍将关闭：" + leave.Exception.GetBaseException().Message, this);
            }
            if (networkManager != null)
            {
                if (!networkManager.ShutdownInProgress && networkManager.IsListening) networkManager.Shutdown();
                while (networkManager != null && (networkManager.IsListening || networkManager.ShutdownInProgress))
                    yield return null;
                if (_singleConfigurationApplied && networkManager != null)
                {
                    if (networkManager.ConnectionApprovalCallback == ApproveSinglePlayer)
                        networkManager.ConnectionApprovalCallback = null;
                    networkManager.NetworkConfig.ConnectionApproval = _originalConnectionApproval;
                    transport.ConnectionData = _originalConnectionData;
                    _singleConfigurationApplied = false;
                }
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private IEnumerator FailSession(string error)
        {
            State = GameSessionState.Returning;
            Status = "正在关闭游戏……";
            yield return null;
            yield return StopSession();
            State = GameSessionState.Failed;
            Status = error;
            _transition = null;
            Debug.LogError("[GameSessionController] " + error, this);
        }

        private IEnumerator ReturnToMainMenuRoutine()
        {
            State = GameSessionState.Returning;
            Status = "正在返回主界面……";
            yield return null;
            yield return StopSession();

            if (SceneManager.GetSceneByPath(mainMenuScenePath).isLoaded)
            {
                State = GameSessionState.MainMenu;
                Status = string.Empty;
                _transition = null;
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(mainMenuScenePath))
            {
                State = GameSessionState.Failed;
                Status = "无法返回主界面，请将 Bootstrap 加入构建场景列表。";
                _transition = null;
                yield break;
            }

            Scene currentScene = SceneManager.GetActiveScene();
            AsyncOperation load = null;
            try
            {
                SceneManager.MoveGameObjectToScene(gameObject, currentScene);
                load = SceneManager.LoadSceneAsync(mainMenuScenePath, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            if (load == null)
            {
                DontDestroyOnLoad(gameObject);
                State = GameSessionState.Failed;
                Status = "返回主界面失败，请查看 Console 后重试。";
                _transition = null;
            }
        }

        private void HandleTransportFailure()
        {
            _transportFailure = "游戏连接发生错误，请查看 Console。";
        }
    }
}
