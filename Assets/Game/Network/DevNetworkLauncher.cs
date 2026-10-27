using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// 【新增：阶段 2 / 第一步】开发用联机启动入口。
    /// 提供 Host、Client、断开连接按钮，以及实际连接状态和连接事件提示。
    /// 使用 OnGUI 绘制临时测试面板，不需要创建 Canvas 或额外 UI 资源。
    ///
    /// 手动配置：挂到 Bootstrap 场景的 NetworkRoot，将同一对象上的
    /// NetworkManager 和 UnityTransport 拖入下方引用；NetworkManager 的
    /// Network Transport 也必须指向该 UnityTransport，Player Prefab 暂时留空。
    ///
    /// 【修改：第二步】增加场景状态和 Load Playground 按钮，场景加载委托给
    /// NetworkSceneLoader；本类仍只负责开发面板和会话入口，不生成玩家、不驱动 Motor。
    /// 【修改：第三步】显示网络玩家数量与 OwnerClientId，方便核对生成和归属。
    /// 【修改：第四步】增加本机移动意图和跳跃次数，验证只有 Owner 接收到输入。
    /// 【修改：第五步】显示服务器权威移动的配置状态。
    /// 【修改：第六步】显示同步后的速度、落地状态和动画接入状态，方便一起测试。
    /// Game.Network.asmdef 同步新增 Unity.Netcode.Runtime 引用，因为本文件使用 NGO；
    /// UnityTransport 本身也位于该程序集内，不需要让 Character 模块依赖 NGO。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevNetworkLauncher : MonoBehaviour
    {
        [Header("手动拖入 NetworkRoot 上的组件")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;

        // 【新增：第二步】拖入同一个 NetworkRoot 上的 NetworkSceneLoader。
        // 引用可暂时留空，第一步的 Host / Client 连接功能仍可使用。
        [SerializeField] private NetworkSceneLoader sceneLoader;

        [Header("开发连接配置")]
        [Tooltip("同机测试使用 127.0.0.1；局域网 Client 填 Host 电脑的 IPv4 地址。")]
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField, Range(1, 65535)] private int port = 7777;

        private string _portText;
        private string _lastEvent = "尚未启动网络会话。";

        // 【新增】订阅实际网络事件；StartClient 返回成功只代表开始连接，
        // 只有本地 Client 的连接回调触发，才能确认真正加入了服务器。
        private void OnEnable()
        {
            _portText = port.ToString();

            if (networkManager == null)
                return;

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnTransportFailure += HandleTransportFailure;
            networkManager.OnServerStopped += HandleServerStopped;
            networkManager.OnClientStopped += HandleClientStopped;
        }

        private void OnDisable()
        {
            if (networkManager == null)
                return;

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnTransportFailure -= HandleTransportFailure;
            networkManager.OnServerStopped -= HandleServerStopped;
            networkManager.OnClientStopped -= HandleClientStopped;
        }

        // 【新增】临时开发面板。连接期间禁止修改地址或重复启动；
        // 断开是异步过程，等待 NGO 完成关闭后才允许再次启动。
        private void OnGUI()
        {
            // 【修改：第二步】加高面板，容纳当前场景、加载状态和场景切换按钮。
            GUILayout.BeginArea(new Rect(16f, 16f, 460f, 650f), GUI.skin.box);
            GUILayout.Label("联机地基测试 / Dev Network Launcher");
            GUILayout.Label("状态：" + GetSessionStatus());

            bool previousEnabled = GUI.enabled;
            bool idle = networkManager != null &&
                        !networkManager.IsListening &&
                        !networkManager.ShutdownInProgress;

            GUI.enabled = previousEnabled && idle;
            GUILayout.Label("Host 的 IPv4 地址（同机：127.0.0.1）");
            serverAddress = GUILayout.TextField(serverAddress ?? string.Empty);
            GUILayout.Label("端口");
            _portText = GUILayout.TextField(_portText ?? port.ToString());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Host"))
                StartSession(true);
            if (GUILayout.Button("Start Client"))
                StartSession(false);
            GUILayout.EndHorizontal();

            // 【新增：第二步】只允许 Host / Server 发起 NGO 场景加载。
            GUI.enabled = previousEnabled && sceneLoader != null && sceneLoader.CanLoad;
            if (GUILayout.Button("Load Playground / 进入测试场景"))
                sceneLoader.LoadPlayground();

            GUI.enabled = previousEnabled && networkManager != null &&
                          networkManager.IsListening &&
                          !networkManager.ShutdownInProgress;
            if (GUILayout.Button("Disconnect / Stop"))
            {
                _lastEvent = "正在关闭网络会话……";
                networkManager.Shutdown();
            }

            GUI.enabled = previousEnabled;
            if (networkManager != null && networkManager.IsServer)
                GUILayout.Label("已连接玩家数（包含 Host）：" +
                                networkManager.ConnectedClientsIds.Count);
            GUILayout.Label("最近事件：" + _lastEvent);
            GUILayout.Label("当前场景：" +
                            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            GUILayout.Label("场景同步：" + (sceneLoader != null
                ? sceneLoader.Status
                : "未配置 NetworkSceneLoader；仍可测试连接。"));
            DrawPlayerOwnership();
            // 【新增：第四步】只显示本机指令；不把这个开发面板当成游戏状态拥有者。
            NetworkCharacter local = NetworkCharacter.LocalPlayer;
            if (local != null)
            {
                GUILayout.Label("本机移动意图：" + local.LatestCommand.MoveDirection.ToString("F2"));
                GUILayout.Label("冲刺输入：" + local.LatestCommand.SprintHeld +
                                " / 跳跃请求次数：" + local.JumpRequests);
                GUILayout.Label("移动链路：" + local.MovementStatus);
                GUILayout.Label("动画链路：" + local.AnimationStatus);
                GUILayout.Label("服务器状态：速度=" + local.CurrentMovementState.HorizontalSpeed.ToString("F2") +
                                " / 落地=" + local.CurrentMovementState.IsGrounded +
                                " / 垂直速度=" + local.CurrentMovementState.VerticalVelocity.ToString("F2"));
            }
            GUILayout.Label("服务器权威 + 本机预测 / 输入重放 + 远程快照插值。");
            GUILayout.EndArea();
        }

        // 【新增：第三步】只读取 NGO 已生成对象，不搜索或修改角色。
        private void DrawPlayerOwnership()
        {
            if (networkManager == null || !networkManager.IsListening ||
                networkManager.SpawnManager == null) return;

            int count = 0;
            foreach (NetworkObject player in networkManager.SpawnManager.SpawnedObjectsList)
            {
                if (!player.IsPlayerObject) continue;
                count++;
                if (count <= 8)
                    GUILayout.Label("Player / ClientId=" + player.OwnerClientId +
                                    " / " + (player.IsOwner ? "本机拥有" : "远程角色"));
            }
            GUILayout.Label("本机可见的网络玩家数：" + count);
        }

        // 【新增】显式引用和配置检查，避免依赖 NetworkManager.Singleton
        // 或在场景里搜索组件，也避免不小心操作另一个 NetworkManager。
        private void StartSession(bool startAsHost)
        {
            if (networkManager == null || transport == null)
            {
                _lastEvent = "请在 Inspector 中设置 NetworkManager 和 Transport 引用。";
                return;
            }

            if (networkManager.IsListening || networkManager.ShutdownInProgress)
                return;

            if (networkManager.NetworkConfig == null ||
                networkManager.NetworkConfig.NetworkTransport != transport)
            {
                _lastEvent = "NetworkManager 的 Network Transport 必须指向这里的 Transport。";
                return;
            }

            if (!ushort.TryParse(_portText, out ushort parsedPort) || parsedPort == 0)
            {
                _lastEvent = "端口必须是 1～65535 的整数。";
                return;
            }

            string address = (serverAddress ?? string.Empty).Trim();
            if (!IPAddress.TryParse(address, out IPAddress parsedAddress) ||
                parsedAddress.AddressFamily != AddressFamily.InterNetwork ||
                address == "0.0.0.0" || address == "255.255.255.255")
            {
                _lastEvent = "请填写 Host 的有效 IPv4 地址，同机测试使用 127.0.0.1。";
                return;
            }

            port = parsedPort;
            serverAddress = parsedAddress.ToString();

            // Host 监听所有本机 IPv4 网卡，供同机或局域网 Client 访问。
            // Client 连接上方指定的 Host 地址。本步采用直接 IP，不配置 Relay。
            transport.SetConnectionData(serverAddress, parsedPort,
                startAsHost ? "0.0.0.0" : null);

            _lastEvent = startAsHost ? "正在启动 Host……" : "正在连接 Host……";
            bool started = startAsHost
                ? networkManager.StartHost()
                : networkManager.StartClient();

            if (!started)
                _lastEvent = "启动失败，请查看 Unity Console 中的 NGO / Transport 错误。";
        }

        // 【新增】状态从 NGO 读取，不用自建 bool 假定连接已经成功。
        private string GetSessionStatus()
        {
            if (networkManager == null)
                return "缺少 NetworkManager 引用";
            if (networkManager.ShutdownInProgress)
                return "正在关闭";
            if (!networkManager.IsListening)
                return "未启动";
            if (networkManager.IsHost)
                return "Host 已启动";
            if (networkManager.IsConnectedClient)
                return "Client 已连接";
            return networkManager.IsServer ? "Server 已启动" : "Client 正在连接";
        }

        private void HandleClientConnected(ulong clientId)
        {
            _lastEvent = networkManager.IsServer
                ? "玩家已连接，ClientId = " + clientId
                : "已成功加入 Host，ClientId = " + clientId;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            string reason = networkManager.DisconnectReason;
            _lastEvent = "玩家连接已断开，ClientId = " + clientId +
                         (string.IsNullOrEmpty(reason) ? string.Empty : "；原因：" + reason);
        }

        private void HandleTransportFailure()
        {
            _lastEvent = "传输失败，请检查端口占用、地址和 Unity Console。";
        }

        private void HandleServerStopped(bool wasHost)
        {
            _lastEvent += wasHost ? " / Host 已停止。" : " / Server 已停止。";
        }

        private void HandleClientStopped(bool wasHost)
        {
            if (!wasHost)
                _lastEvent += " / Client 已停止。";
        }
    }
}
