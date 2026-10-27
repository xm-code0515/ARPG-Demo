namespace Game.Network
{
    public static class InputSequence
    {
        public static bool IsNewer(uint candidate, uint reference) => unchecked((int)(candidate - reference)) > 0;
        public static bool IsNext(uint candidate, uint reference) => candidate == unchecked(reference + 1);
    }
}
