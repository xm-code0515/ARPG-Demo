using System;
using Game.Quests;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    public struct NetworkQuestState : INetworkSerializable, IEquatable<NetworkQuestState>
    {
        public QuestStatus Status;
        public int Kills;
        public NetworkQuestState(KillQuestProgress progress) { Status = progress.Status; Kills = progress.Kills; }
        public NetworkQuestState(QuestStatus status, int kills) { Status = status; Kills = kills; }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref Status); serializer.SerializeValue(ref Kills); }
        public bool Equals(NetworkQuestState other) => Status == other.Status && Kills == other.Kills;
    }

    [RequireComponent(typeof(NetworkInventory))]
    public sealed class NetworkPlayerQuests : NetworkBehaviour
    {
        [SerializeField] private QuestCatalog catalog;
        private NetworkList<NetworkQuestState> _states;
        private SharedQuestSession _session;
        private float _nextRequest;
        public QuestCatalog Catalog => catalog;
        public event Action Changed;
        public event Action<string, bool> OperationCompleted;
        private void Awake() => _states = new NetworkList<NetworkQuestState>(null,
            NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        public override void OnNetworkSpawn()
        {
            _states.OnListChanged += OnChanged;
            if (IsServer)
            {
                _nextRequest = 0f;
                _states.Clear();
                _session = SharedQuestSession.Register(this);
            }
            Changed?.Invoke();
        }
        private void OnChanged(NetworkListEvent<NetworkQuestState> change) => Changed?.Invoke();
        public override void OnNetworkDespawn()
        { _states.OnListChanged -= OnChanged; _session?.Unregister(this); _session = null; Changed?.Invoke(); }
        public NetworkQuestState GetState(QuestDefinition quest)
        {
            int index = catalog != null ? catalog.IndexOf(quest) : -1;
            return IsSpawned && (IsOwner || IsServer) && index >= 0 && index < _states.Count ? _states[index] : new NetworkQuestState(QuestStatus.Locked, 0);
        }
        internal void PublishSharedStates(SharedQuestSession session)
        {
            if (!IsSpawned || !IsServer || catalog == null) return;
            for (int i = 0; i < catalog.Count; i++)
            {
                var state = session.GetState(catalog.Get(i), OwnerClientId);
                if (i >= _states.Count) _states.Add(state);
                else if (!_states[i].Equals(state)) _states[i] = state;
            }
        }
        public void Request(QuestNpc npc, bool claim)
        {
            if (npc != null) Request(npc, npc.GetQuest(this), claim);
        }
        public void Request(QuestNpc npc, QuestDefinition quest, bool claim)
        {
            if (IsSpawned && IsOwner && npc != null && quest != null)
                QuestRequestRpc(new FixedString64Bytes(npc.NpcId), new FixedString64Bytes(quest.QuestId), claim);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void QuestRequestRpc(FixedString64Bytes npcId, FixedString64Bytes questId, bool claim, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || Time.unscaledTime < _nextRequest) return;
            _nextRequest = Time.unscaledTime + .2f;
            var npc = QuestNpc.Find(npcId.ToString(), gameObject.scene);
            var quest = catalog != null ? catalog.Find(questId.ToString()) : null;
            var before = GetState(quest).Status;
            string message = TryInteract(npc, quest, claim);
            bool success = quest != null && GetState(quest).Status != before;
            ResultRpc(message, success);
        }
        // The same validation runs for Host and remote owner requests.
        public string TryInteract(QuestNpc npc, bool claim)
            => TryInteract(npc, npc != null ? npc.GetQuest(this) : null, claim);
        public string TryInteract(QuestNpc npc, QuestDefinition quest, bool claim)
        {
            if (!IsSpawned || !IsServer || NetworkManager.ShutdownInProgress || _session == null || npc == null || !npc.CanInteract(gameObject))
                return "请靠近任务 NPC 后再操作";
            if (quest == null || catalog.IndexOf(quest) < 0 || !npc.Offers(quest)) return "任务配置不可用";
            if (!claim)
            {
                if (GetState(quest).Status == QuestStatus.Locked) return "请先完成前置任务";
                if (!_session.Accept(quest)) return "队伍已经接受或完成这个任务";
                return "全队已接受任务：" + quest.Title;
            }
            if (GetState(quest).Status != QuestStatus.Ready) return "任务尚未完成或奖励已经领取";
            if (!GetComponent<NetworkInventory>().TryGiveRewards(quest.Rewards)) return "背包空间不足，请整理后再领取全部奖励";
            _session.MarkRewarded(quest, OwnerClientId);
            return "任务完成，奖励已放入背包";
        }
        public void RecordKill(string enemyId)
        {
            if (!IsSpawned || !IsServer || NetworkManager.ShutdownInProgress) return;
            _session?.RecordKill(enemyId);
        }
        [Rpc(SendTo.Owner)] private void ResultRpc(string message, bool success) => OperationCompleted?.Invoke(message, success);
    }
}
