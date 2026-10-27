using System.Collections.Generic;
using Game.Character;
using Game.Character.Actions;

namespace Game.Network
{
    public sealed class CharacterPredictionBuffer
    {
        public const int Capacity = 256;

        public readonly struct Entry
        {
            public readonly NetworkCharacterCommand Frame;
            public readonly CharacterMotorState Result;
            public readonly CharacterActionState Actions;

            public Entry(NetworkCharacterCommand frame, CharacterMotorState result, CharacterActionState actions = default)
            {
                Frame = frame;
                Result = result;
                Actions = actions;
            }
        }

        private readonly List<Entry> _entries = new List<Entry>(Capacity);
        public int Count => _entries.Count;
        public Entry this[int index] => _entries[index];

        public bool TryAppend(NetworkCharacterCommand frame, CharacterMotorState result, CharacterActionState actions = default)
        {
            if (_entries.Count >= Capacity) return false;
            if (_entries.Count > 0 && !InputSequence.IsNext(frame.Sequence, _entries[_entries.Count - 1].Frame.Sequence))
                return false;
            _entries.Add(new Entry(frame, result, actions));
            return true;
        }

        public bool Acknowledge(uint sequence, out CharacterMotorState predictedResult)
        {
            return Acknowledge(sequence, out predictedResult, out _);
        }

        public bool Acknowledge(uint sequence, out CharacterMotorState predictedResult, out CharacterActionState actions)
        {
            predictedResult = default;
            actions = default;
            bool found = false;
            int removeCount = 0;
            foreach (Entry entry in _entries)
            {
                if (InputSequence.IsNewer(entry.Frame.Sequence, sequence)) break;
                removeCount++;
                if (entry.Frame.Sequence == sequence)
                {
                    predictedResult = entry.Result;
                    actions = entry.Actions;
                    found = true;
                }
            }
            if (removeCount > 0) _entries.RemoveRange(0, removeCount);
            return found;
        }

        public void ReplaceResult(int index, CharacterMotorState result, CharacterActionState actions = default)
        {
            _entries[index] = new Entry(_entries[index].Frame, result, actions);
        }

        public void Clear() => _entries.Clear();
    }
}
