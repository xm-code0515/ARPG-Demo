using System.Collections.Generic;

namespace Game.Quests
{
    // Team completion never becomes Rewarded: claiming is independent for each participant.
    public sealed class SharedKillQuestProgress
    {
        private readonly KillQuestProgress _team = new KillQuestProgress();
        private readonly HashSet<ulong> _rewarded = new HashSet<ulong>();
        public QuestStatus Status => _team.Status;
        public int Kills => _team.Kills;
        public QuestStatus GetStatus(ulong participant) => _rewarded.Contains(participant) ? QuestStatus.Rewarded : Status;
        public bool Accept() => _team.Accept();
        public bool RecordKill(string target, string killed, int required) => _team.RecordKill(target, killed, required);
        public bool MarkRewarded(ulong participant) => Status == QuestStatus.Ready && _rewarded.Add(participant);
    }
}
