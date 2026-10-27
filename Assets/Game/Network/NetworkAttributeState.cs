using System;
using Unity.Netcode;

namespace Game.Network
{
    public struct NetworkAttributePublicState : INetworkSerializable, IEquatable<NetworkAttributePublicState>
    {
        public bool HasState;
        public uint Revision;
        public float Health;
        public float MaxHealth;
        public float AttackPower;
        public float Defense;
        public float MoveSpeed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref HasState);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref AttackPower);
            serializer.SerializeValue(ref Defense);
            serializer.SerializeValue(ref MoveSpeed);
        }

        public bool Equals(NetworkAttributePublicState other) => HasState == other.HasState && Revision == other.Revision &&
            Health.Equals(other.Health) && MaxHealth.Equals(other.MaxHealth) && AttackPower.Equals(other.AttackPower) &&
            Defense.Equals(other.Defense) && MoveSpeed.Equals(other.MoveSpeed);
        public override bool Equals(object obj) => obj is NetworkAttributePublicState other && Equals(other);
        public override int GetHashCode() => Revision.GetHashCode();
    }

    public struct NetworkAttributeOwnerState : INetworkSerializable, IEquatable<NetworkAttributeOwnerState>
    {
        public bool HasState;
        public uint Revision;
        public float Stamina;
        public float MaxStamina;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref HasState);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref Stamina);
            serializer.SerializeValue(ref MaxStamina);
        }

        public bool Equals(NetworkAttributeOwnerState other) => HasState == other.HasState && Revision == other.Revision &&
            Stamina.Equals(other.Stamina) && MaxStamina.Equals(other.MaxStamina);
        public override bool Equals(object obj) => obj is NetworkAttributeOwnerState other && Equals(other);
        public override int GetHashCode() => Revision.GetHashCode();
    }
}
