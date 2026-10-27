using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    /// <summary>
    /// 【新增：阶段 2 / 第三步】只在服务器生成网络玩家。
    /// 等 Playground 的网络加载结束，再为已连接玩家生成；晚加入者同步场景后生成。
    /// 使用 SpawnAsPlayerObject 指定归属，已存在 PlayerObject 时不重复生成。
    ///
    /// 手动配置：挂到 Bootstrap 的同一个顶层 NetworkRoot，拖入 NetworkManager 和
    /// 网络 Player Prefab 的根 NetworkObject；该 Prefab 必须加入 Network Prefabs List。
    /// NetworkManager 的 Player Prefab 继续留空，避免自动生成与本组件重复。
    /// Prefab 的 Dont Destroy With Owner 保持关闭，让 NGO 在断线时自动移除玩家。
    ///
    /// 初始出生与复活共用场景的 PlayerRespawnPoint；位置由服务器生成消息同步。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private string playgroundScenePath = "Assets/Game/Scenes/Playground.unity";
        [SerializeField] private Vector3 spawnOrigin = new Vector3(0f, 0.1f, 0f);
        [SerializeField, Min(1f)] private float spawnSpacing = 3f;

        private NetworkSceneManager _sceneManager;
        private bool _sceneReady;
        private readonly Dictionary<ulong, int> _spawnSlots = new Dictionary<ulong, int>();

        private void OnEnable()
        {
            if (networkManager == null) return;
            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnServerStopped += HandleServerStopped;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
                networkManager.OnServerStopped -= HandleServerStopped;
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            UnbindSceneEvents();
        }

        private void HandleServerStarted()
        {
            _spawnSlots.Clear();
            _sceneReady = SceneManager.GetSceneByPath(playgroundScenePath).isLoaded;
            UnbindSceneEvents();
            _sceneManager = networkManager.SceneManager;
            if (_sceneManager != null) _sceneManager.OnSceneEvent += HandleSceneEvent;
        }

        // 【新增】场景开始切换时关闭生成入口；全体加载流程结束后才生成已有玩家。
        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.Load)
            {
                _sceneReady = false;
            }
            else if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                _sceneReady = SceneManager.GetSceneByPath(playgroundScenePath).isLoaded;
                if (!_sceneReady) return;
                // 只处理完成场景加载的参与者，尚在同步或超时的 Client 不提前生成。
                if (sceneEvent.ClientsThatCompleted != null)
                    foreach (ulong clientId in sceneEvent.ClientsThatCompleted)
                        TrySpawnPlayer(clientId);
            }
            // 晚加入者走 NGO 场景同步，OnClientConnectedCallback 会在同步结束后触发。
        }

        private void HandleClientConnected(ulong clientId)
        {
            TrySpawnPlayer(clientId);
        }

        private void TrySpawnPlayer(ulong clientId)
        {
            if (!networkManager.IsServer || networkManager.ShutdownInProgress || !_sceneReady)
                return;
            if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
                client.PlayerObject != null)
                return;

            // 【新增】错误配置时明确报错，不在运行时偷偷注册资源或修改项目配置。
            if (!ValidateConfiguration(out string error))
            {
                Debug.LogError("[NetworkPlayerSpawner] " + error, this);
                return;
            }

            int slot = 0;
            while (_spawnSlots.ContainsValue(slot)) slot++;
            GetSpawnPose(slot, out Vector3 position, out Quaternion rotation);
            NetworkObject player = Instantiate(playerPrefab, position, rotation);
            SceneManager.MoveGameObjectToScene(player.gameObject,
                SceneManager.GetSceneByPath(playgroundScenePath));
            // NGO 通过此调用设置 PlayerObject、OwnerClientId，并同步生成给客户端。
            player.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            _spawnSlots[clientId] = slot;
        }

        public bool ValidateConfiguration(out string error)
        {
            error = null;
            if (networkManager == null || networkManager.gameObject != gameObject || transform.parent != null ||
                playerPrefab == null || playerPrefab.GetComponent<NetworkCharacter>() == null)
                error = "请在同一个顶层 NetworkRoot 上配置玩家生成器，并设置带 NetworkCharacter 的 Player Prefab。";
            else if (networkManager.NetworkConfig.PlayerPrefab != null ||
                     !networkManager.NetworkConfig.Prefabs.Contains(playerPrefab.gameObject))
                error = "请留空 NetworkManager.Player Prefab，并将网络 Prefab 注册到 Network Prefabs List。";
            else if (playerPrefab.GetComponent<Unity.Netcode.Components.NetworkTransform>() != null)
                error = "客户端预测版本需要移除 NetworkPlayer 上的 NetworkTransform，位置统一由 NetworkCharacter 快照同步。";
            return error == null;
        }

        public bool TryGetRespawnPose(ulong clientId, out Vector3 position, out Quaternion rotation)
        {
            position = spawnOrigin;
            rotation = Quaternion.identity;
            if (!_spawnSlots.TryGetValue(clientId, out int slot)) return false;
            GetSpawnPose(slot, out position, out rotation);
            return true;
        }

        private void GetSpawnPose(int slot, out Vector3 position, out Quaternion rotation)
        {
            PlayerRespawnPoint point = null;
            var scene = SceneManager.GetSceneByPath(playgroundScenePath);
            if (scene.IsValid() && scene.isLoaded)
                foreach (var root in scene.GetRootGameObjects())
                {
                    point = root.GetComponentInChildren<PlayerRespawnPoint>();
                    if (point != null) break;
                }
            if (point != null)
                point.GetPose(slot, out position, out rotation);
            else
            {
                position = spawnOrigin + new Vector3(slot % 4, 0f, slot / 4) * spawnSpacing;
                rotation = Quaternion.identity;
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            // NGO 负责实际 Despawn；这里只释放出生位置，供后来加入的玩家复用。
            _spawnSlots.Remove(clientId);
        }

        private void HandleServerStopped(bool wasHost)
        {
            _sceneReady = false;
            _spawnSlots.Clear();
            UnbindSceneEvents();
        }

        private void UnbindSceneEvents()
        {
            if (_sceneManager != null) _sceneManager.OnSceneEvent -= HandleSceneEvent;
            _sceneManager = null;
        }
    }
}
