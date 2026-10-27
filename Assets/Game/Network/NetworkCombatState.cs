using System;
using Game.Combat;
using Unity.Netcode;

namespace Game.Network
{
    public struct NetworkCombatState : INetworkSerializable, IEquatable<NetworkCombatState>
    {
        public CombatState Value;
        public NetworkCombatState(CombatState state) { Value = state; }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value.Attacking);
            serializer.SerializeValue(ref Value.HitApplied);
            serializer.SerializeValue(ref Value.Buffered);
            serializer.SerializeValue(ref Value.AttackIndex);
            serializer.SerializeValue(ref Value.NextAttackIndex);
            serializer.SerializeValue(ref Value.AttackSequence);
            serializer.SerializeValue(ref Value.Elapsed);
            serializer.SerializeValue(ref Value.HitSequence);
            serializer.SerializeValue(ref Value.HitElapsed);
            serializer.SerializeValue(ref Value.HitStopRemaining);
            serializer.SerializeValue(ref Value.Dodging);
            serializer.SerializeValue(ref Value.DodgeRewarded);
            serializer.SerializeValue(ref Value.DodgeSequence);
            serializer.SerializeValue(ref Value.DodgeElapsed);
            serializer.SerializeValue(ref Value.DodgeCooldownRemaining);
            serializer.SerializeValue(ref Value.DodgeDirectionX);
            serializer.SerializeValue(ref Value.DodgeDirectionZ);
            serializer.SerializeValue(ref Value.DodgeOriginX);
            serializer.SerializeValue(ref Value.DodgeOriginY);
            serializer.SerializeValue(ref Value.DodgeOriginZ);
        }
        public bool Equals(NetworkCombatState other) => Value.Equals(other.Value);
    }
}
