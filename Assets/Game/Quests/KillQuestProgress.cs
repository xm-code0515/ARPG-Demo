namespace Game.Quests
{
    public enum QuestStatus : byte { Available, Active, Ready, Rewarded, Locked }

    // No Unity or networking dependencies: progression rules shared by local and cooperative quests.
    public sealed class KillQuestProgress
    {
        public QuestStatus Status { get; private set; }
        public int Kills { get; private set; }
        public bool Accept()
        {
            if (Status != QuestStatus.Available) return false;
            Status = QuestStatus.Active;
            return true;
        }
        public bool RecordKill(string target, string killed, int required)
        {
            if (Status != QuestStatus.Active || target != killed || required < 1) return false;
            Kills = System.Math.Min(required, Kills + 1);
            if (Kills == required) Status = QuestStatus.Ready;
            return true;
        }
        public bool MarkRewarded()
        {
            if (Status != QuestStatus.Ready) return false;
            Status = QuestStatus.Rewarded;
            return true;
        }
    }
}
