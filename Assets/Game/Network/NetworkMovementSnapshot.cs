using System;
using Game.Character;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    public struct NetworkMovementSnapshot : INetworkSerializable, IEquatable<NetworkMovementSnapshot>
    {
        public bool HasState;
        public uint AcknowledgedSequence;
        public double ServerTime;
        public double SimulationTime;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float MotorVerticalVelocity;
        public bool IsGrounded;
        public bool IsSprinting;
        public Vector3 KnockbackVelocity;
        public float KnockbackRemaining;
        public NetworkCombatState Combat;
        public uint LifeRevision;
        public uint EquipmentRevision;

        public NetworkMovementSnapshot(CharacterMotorState state, uint acknowledgedSequence, double serverTime,
            double simulationTime, NetworkCombatState combat = default, uint lifeRevision = 0, uint equipmentRevision = 0)
        {
            HasState = true;
            AcknowledgedSequence = acknowledgedSequence;
            ServerTime = serverTime;
            SimulationTime = simulationTime;
            Position = state.Position;
            Rotation = state.Rotation;
            Velocity = state.Velocity;
            MotorVerticalVelocity = state.VerticalVelocity;
            IsGrounded = state.IsGrounded;
            IsSprinting = state.IsSprinting;
            KnockbackVelocity = state.KnockbackVelocity;
            KnockbackRemaining = state.KnockbackRemaining;
            Combat = combat;
            LifeRevision = lifeRevision;
            EquipmentRevision = equipmentRevision;
        }

        public CharacterMotorState ToMotorState() => new CharacterMotorState(Position, Rotation,
            Velocity, MotorVerticalVelocity, IsGrounded, IsSprinting, KnockbackVelocity, KnockbackRemaining);

        public MovementState ToMovementState() => ToMotorState().ToMovementState();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref HasState);
            serializer.SerializeValue(ref AcknowledgedSequence);
            serializer.SerializeValue(ref ServerTime);
            serializer.SerializeValue(ref SimulationTime);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref MotorVerticalVelocity);
            serializer.SerializeValue(ref IsGrounded);
            serializer.SerializeValue(ref IsSprinting);
            serializer.SerializeValue(ref KnockbackVelocity);
            serializer.SerializeValue(ref KnockbackRemaining);
            serializer.SerializeValue(ref Combat);
            serializer.SerializeValue(ref LifeRevision);
            serializer.SerializeValue(ref EquipmentRevision);
        }

        public bool Equals(NetworkMovementSnapshot other) => HasState == other.HasState &&
            AcknowledgedSequence == other.AcknowledgedSequence && ServerTime.Equals(other.ServerTime) &&
            SimulationTime.Equals(other.SimulationTime) &&
            Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Velocity.Equals(other.Velocity) &&
            MotorVerticalVelocity.Equals(other.MotorVerticalVelocity) &&
            IsGrounded == other.IsGrounded && IsSprinting == other.IsSprinting &&
            KnockbackVelocity.Equals(other.KnockbackVelocity) && KnockbackRemaining.Equals(other.KnockbackRemaining) &&
            Combat.Equals(other.Combat) && LifeRevision == other.LifeRevision && EquipmentRevision == other.EquipmentRevision;

        public override bool Equals(object obj) => obj is NetworkMovementSnapshot other && Equals(other);
        public override int GetHashCode() => Position.GetHashCode() ^ AcknowledgedSequence.GetHashCode();
    }
}
