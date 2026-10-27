using System;
using Game.Character.Actions;
using Unity.Netcode;

namespace Game.Network
{
    public struct NetworkCharacterPredictionSnapshot : INetworkSerializable, IEquatable<NetworkCharacterPredictionSnapshot>
    {
        public NetworkMovementSnapshot Movement;
        public float Stamina;
        public float MaxStamina;
        public float MoveSpeed;
        public float RecoveryDelayRemaining;
        public bool SprintExhausted;
        public bool IsDead;

        public NetworkCharacterPredictionSnapshot(NetworkMovementSnapshot movement, CharacterActionState actions)
        {
            Movement = movement;
            Stamina = actions.Stamina;
            MaxStamina = actions.MaxStamina;
            MoveSpeed = actions.MoveSpeed;
            RecoveryDelayRemaining = actions.RecoveryDelayRemaining;
            SprintExhausted = actions.SprintExhausted;
            IsDead = actions.IsDead;
        }

        public CharacterActionState ToActionState() => new CharacterActionState(Stamina, MaxStamina, MoveSpeed,
            RecoveryDelayRemaining, SprintExhausted, IsDead, Movement.Combat.Value);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Movement);
            serializer.SerializeValue(ref Stamina);
            serializer.SerializeValue(ref MaxStamina);
            serializer.SerializeValue(ref MoveSpeed);
            serializer.SerializeValue(ref RecoveryDelayRemaining);
            serializer.SerializeValue(ref SprintExhausted);
            serializer.SerializeValue(ref IsDead);
        }

        public bool Equals(NetworkCharacterPredictionSnapshot other) => Movement.Equals(other.Movement) &&
            Stamina.Equals(other.Stamina) && MaxStamina.Equals(other.MaxStamina) && MoveSpeed.Equals(other.MoveSpeed) &&
            RecoveryDelayRemaining.Equals(other.RecoveryDelayRemaining) && SprintExhausted == other.SprintExhausted && IsDead == other.IsDead;
        public override bool Equals(object obj) => obj is NetworkCharacterPredictionSnapshot other && Equals(other);
        public override int GetHashCode() => Movement.GetHashCode();
    }
}
