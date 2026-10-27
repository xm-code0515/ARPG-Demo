#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using Game.Character;
using Game.Input;
using Game.Quests;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Network.Development
{
    // Only used by the isolated cooperative-quest validation scene, never the gameplay scene.
    public sealed class QuestCoopSmokeRunner : MonoBehaviour
    {
        [SerializeField] private NetworkManager manager;
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private QuestNpc npc;
        private string _report, _control;
        private int _lastOperation;
        private float _nextReport;
        private bool _prepared;
        private NetworkPlayerQuests _local;
        private string _result;
        private bool _success;

        private void Start()
        {
            Application.runInBackground = true;
            if (!HasArgument("--quest-smoke-client")) return;
            _report = Argument("--quest-report"); _control = Argument("--quest-control");
            Prepare();
            manager.GetComponent<UnityTransport>().SetConnectionData(true, "127.0.0.1", ushort.Parse(Argument("--quest-port") ?? "7793"), "127.0.0.1");
            manager.StartClient();
        }

        public void Prepare()
        {
            if (_prepared) return;
            _prepared = true;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.PlayerPrefab = null;
            manager.AddNetworkPrefab(playerPrefab.gameObject);
            manager.OnClientConnectedCallback += SpawnPlayer;
        }

        private void SpawnPlayer(ulong owner)
        {
            if (!manager.IsServer || manager.ConnectedClients[owner].PlayerObject != null) return;
            float angle = owner * Mathf.PI * 2f / 5f;
            var position = npc.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2.7f + Vector3.up * .05f;
            var instance = Instantiate(playerPrefab, position, Quaternion.identity);
            instance.SpawnAsPlayerObject(owner);
        }

        private void Update()
        {
            if (!manager.IsListening || Time.unscaledTime < _nextReport) return;
            _nextReport = Time.unscaledTime + .2f;
            var player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
            var quests = player != null ? player.GetComponent<NetworkPlayerQuests>() : null;
            if (_local != quests)
            {
                if (_local != null) _local.OperationCompleted -= OnResult;
                _local = quests;
                if (_local != null)
                {
                    _local.OperationCompleted += OnResult;
                    var input = _local.GetComponent<PlayerInputReader>(); if (input != null) input.enabled = false;
                }
            }
            if (_local != null && !string.IsNullOrEmpty(_control) && File.Exists(_control))
            {
                try
                {
                    var operation = JsonUtility.FromJson<Control>(File.ReadAllText(_control));
                    if (operation.sequence > _lastOperation)
                    {
                        _lastOperation = operation.sequence;
                        var quest = _local.Catalog.Find(operation.questId);
                        _local.Request(npc, quest, operation.claim);
                    }
                }
                catch (IOException) { }
            }
            if (_local == null || string.IsNullOrEmpty(_report)) return;
            var report = new Report { connected = manager.IsConnectedClient, owner = _local.OwnerClientId, operation = _lastOperation,
                result = _result, success = _success, states = new List<State>(), items = new List<Item>() };
            for (int i = 0; i < _local.Catalog.Count; i++)
            {
                var quest = _local.Catalog.Get(i); var state = _local.GetState(quest);
                report.states.Add(new State { id = quest.QuestId, status = state.Status.ToString(), kills = state.Kills });
            }
            var inventory = _local.GetComponent<NetworkInventory>();
            for (int i = 0; i < inventory.SlotCount + 8; i++)
            {
                int address = i < inventory.SlotCount ? i : 100 + i - inventory.SlotCount;
                var stack = inventory.GetSlot(address);
                if (stack.IsEmpty) continue;
                report.items.Add(new Item { id = inventory.Catalog.Get(stack.ItemIndex).ItemId, quantity = stack.Quantity });
            }
            File.WriteAllText(_report, JsonUtility.ToJson(report, true));
        }

        private void OnResult(string message, bool success) { _result = message; _success = success; }
        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        private void OnDestroy()
        {
            if (manager != null) manager.OnClientConnectedCallback -= SpawnPlayer;
            if (_local != null) _local.OperationCompleted -= OnResult;
        }
        [Serializable] private sealed class Control { public int sequence; public string questId; public bool claim; }
        [Serializable] private sealed class State { public string id, status; public int kills; }
        [Serializable] private sealed class Item { public string id; public int quantity; }
        [Serializable] private sealed class Report
        {
            public bool connected, success; public ulong owner; public int operation; public string result;
            public List<State> states; public List<Item> items;
        }
    }
}
#endif
