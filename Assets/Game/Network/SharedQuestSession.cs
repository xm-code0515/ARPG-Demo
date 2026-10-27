using System.Collections.Generic;
using Game.Quests;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    // One authoritative record per running NetworkManager. Player lists are owner-readable replicas.
    internal sealed class SharedQuestSession
    {
        private static readonly Dictionary<NetworkManager, SharedQuestSession> Sessions = new Dictionary<NetworkManager, SharedQuestSession>();
        private readonly NetworkManager _manager;
        private readonly QuestCatalog _catalog;
        private readonly SharedKillQuestProgress[] _progress;
        private readonly HashSet<NetworkPlayerQuests> _players = new HashSet<NetworkPlayerQuests>();

        private SharedQuestSession(NetworkManager manager, QuestCatalog catalog)
        {
            _manager = manager;
            _catalog = catalog;
            _progress = new SharedKillQuestProgress[catalog.Count];
            for (int i = 0; i < _progress.Length; i++) _progress[i] = new SharedKillQuestProgress();
            manager.OnServerStopped += OnStopped;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessions()
        {
            foreach (var session in Sessions.Values) session._manager.OnServerStopped -= session.OnStopped;
            Sessions.Clear();
        }

        public static SharedQuestSession Register(NetworkPlayerQuests player)
        {
            var manager = player.NetworkManager;
            if (manager == null || !manager.IsServer || player.Catalog == null) return null;
            if (!Sessions.TryGetValue(manager, out var session))
            {
                session = new SharedQuestSession(manager, player.Catalog);
                Sessions.Add(manager, session);
            }
            if (session._catalog != player.Catalog)
            { Debug.LogError("[SharedQuestSession] All players must use the same QuestCatalog.", player); return null; }
            session._players.Add(player);
            player.PublishSharedStates(session);
            return session;
        }

        public void Unregister(NetworkPlayerQuests player) => _players.Remove(player);
        private void OnStopped(bool wasHost)
        {
            _manager.OnServerStopped -= OnStopped;
            _players.Clear();
            Sessions.Remove(_manager);
        }

        private bool IsUnlocked(QuestDefinition quest)
        {
            if (quest.Prerequisite == null) return true;
            int index = _catalog.IndexOf(quest.Prerequisite);
            return index >= 0 && _progress[index].Status == QuestStatus.Ready;
        }

        public NetworkQuestState GetState(QuestDefinition quest, ulong participant)
        {
            if (quest == null) return new NetworkQuestState(QuestStatus.Locked, 0);
            int index = _catalog.IndexOf(quest);
            if (index < 0) return new NetworkQuestState(QuestStatus.Locked, 0);
            return new NetworkQuestState(IsUnlocked(quest) ? _progress[index].GetStatus(participant) : QuestStatus.Locked, _progress[index].Kills);
        }

        public bool Accept(QuestDefinition quest)
        {
            int index = _catalog.IndexOf(quest);
            if (index < 0 || !IsUnlocked(quest) || !_progress[index].Accept()) return false;
            Publish();
            return true;
        }

        public bool MarkRewarded(QuestDefinition quest, ulong participant)
        {
            int index = _catalog.IndexOf(quest);
            if (index < 0 || !_progress[index].MarkRewarded(participant)) return false;
            Publish();
            return true;
        }

        public void RecordKill(string enemyId)
        {
            bool changed = false;
            for (int i = 0; i < _progress.Length; i++)
            {
                var quest = _catalog.Get(i);
                if (quest != null && _progress[i].RecordKill(quest.TargetEnemyId, enemyId, quest.RequiredKills)) changed = true;
            }
            if (changed) Publish();
        }

        private void Publish()
        {
            foreach (var player in _players)
                if (player != null && player.IsSpawned && player.IsServer) player.PublishSharedStates(this);
        }
    }
}
