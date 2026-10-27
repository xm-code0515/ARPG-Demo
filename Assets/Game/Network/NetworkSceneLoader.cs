using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    /// <summary>
    /// 【新增：阶段 2 / 第二步】网络测试场景加载适配器。
    /// Host 发起 NGO 场景加载，已连接 Client 跟随切换；后来加入的 Client
    /// 使用 NGO 的场景同步机制进入服务器当前场景，不自行调用 Unity LoadScene。
    ///
    /// 手动配置：与 NetworkManager、DevNetworkLauncher 挂在同一个顶层 NetworkRoot；
    /// 拖入 NetworkManager 引用，启用其 Enable Scene Management；构建场景列表中
    /// 同时启用 Bootstrap 和 Playground。Playground 不再放另一个 NetworkRoot。
    ///
    /// 使用 Single 模式卸载 Bootstrap，避免保留两套 Camera / AudioListener。
    /// NGO 会保留 NetworkManager 所在对象，因此同对象上的启动面板与本组件也保留。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSceneLoader : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private string playgroundScenePath = "Assets/Game/Scenes/Playground.unity";

        private NetworkSceneManager _subscribedSceneManager;
        private bool _loadPending;

        public string Status { get; private set; } = "等待 Host 启动并加载 Playground。";
        public bool IsPlaygroundLoaded => SceneManager.GetSceneByPath(playgroundScenePath).isLoaded;

        // 【新增】只有服务器能发起加载；已加载或正在加载时禁止重复请求。
        public bool CanLoad => networkManager != null && networkManager.IsServer &&
                               networkManager.IsListening && !networkManager.ShutdownInProgress &&
                               !_loadPending && !SceneManager.GetSceneByPath(playgroundScenePath).isLoaded;

        private void OnEnable()
        {
            if (networkManager == null)
                return;

            networkManager.OnServerStarted += BindSceneEvents;
            networkManager.OnClientStarted += BindSceneEvents;
            networkManager.OnServerStopped += HandleSessionStopped;
            networkManager.OnClientStopped += HandleSessionStopped;
            BindSceneEvents();
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= BindSceneEvents;
                networkManager.OnClientStarted -= BindSceneEvents;
                networkManager.OnServerStopped -= HandleSessionStopped;
                networkManager.OnClientStopped -= HandleSessionStopped;
            }
            UnbindSceneEvents();
        }

        // 【新增】NetworkSceneManager 在会话启动时创建，不能在 Awake 中假定它存在。
        // 重新启动会话时重新绑定，避免继续监听上一次已经关闭的 SceneManager。
        private void BindSceneEvents()
        {
            NetworkSceneManager current = networkManager != null ? networkManager.SceneManager : null;
            if (current == _subscribedSceneManager)
                return;

            UnbindSceneEvents();
            _subscribedSceneManager = current;
            if (current != null)
                current.OnSceneEvent += HandleSceneEvent;
        }

        private void UnbindSceneEvents()
        {
            if (_subscribedSceneManager != null)
                _subscribedSceneManager.OnSceneEvent -= HandleSceneEvent;
            _subscribedSceneManager = null;
        }

        // 【新增】面板的按钮只委托这一个入口；不把场景职责塞进 GameBootstrap。
        public void LoadPlayground()
        {
            TryLoadPlayground();
        }

        public bool ValidateConfiguration(out string error)
        {
            error = null;
            if (networkManager == null || networkManager.gameObject != gameObject || transform.parent != null)
                error = "请将场景加载器与 NetworkManager 挂到同一个顶层 NetworkRoot，并设置引用。";
            else if (!networkManager.NetworkConfig.EnableSceneManagement)
                error = "请启用 NetworkManager 的 Enable Scene Management。";
            else if (!Application.CanStreamedLevelBeLoaded(playgroundScenePath))
                error = "Playground 不在启用的构建场景列表中，请检查 Build Profiles。";
            return error == null;
        }

        public bool TryLoadPlayground()
        {
            if (!CanLoad)
                return false;

            if (!ValidateConfiguration(out string error))
            {
                Status = error;
                return false;
            }
            if (networkManager.SceneManager == null)
            {
                Status = "网络场景管理器尚未就绪。";
                return false;
            }

            BindSceneEvents();
            _loadPending = true;
            Status = "正在同步加载 Playground……";
            SceneEventProgressStatus result = networkManager.SceneManager.LoadScene(
                playgroundScenePath, LoadSceneMode.Single);

            // Started 仅表示请求被接受，完成情况必须由 NGO 场景事件确认。
            if (result != SceneEventProgressStatus.Started)
            {
                _loadPending = false;
                Status = "加载请求未启动：" + result;
            }
            return result == SceneEventProgressStatus.Started;
        }

        // 【新增】区分本机加载结束、全体加载结束，以及晚加入 Client 同步完成。
        // 回调只记录状态，不在场景事件回调内发起另一次场景加载。
        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                    Status = "正在加载场景：" + sceneEvent.SceneName;
                    break;
                case SceneEventType.LoadComplete:
                    if (sceneEvent.ClientId == networkManager.LocalClientId)
                        Status = "本机场景已加载，等待网络场景同步完成。";
                    break;
                case SceneEventType.LoadEventCompleted:
                    _loadPending = false;
                    int timeoutCount = sceneEvent.ClientsThatTimedOut?.Count ?? 0;
                    Status = timeoutCount == 0
                        ? "场景加载已完成：" + sceneEvent.SceneName
                        : "场景加载结束，但有 " + timeoutCount + " 个客户端超时，请查看 Console。";
                    break;
                case SceneEventType.SynchronizeComplete:
                    Status = networkManager.IsServer
                        ? "Client " + sceneEvent.ClientId + " 已完成场景同步。"
                        : "已与 Host 完成场景同步。";
                    break;
            }
        }

        private void HandleSessionStopped(bool wasHost)
        {
            _loadPending = false;
            UnbindSceneEvents();
            Status = "网络会话已停止。";
        }
    }
}
