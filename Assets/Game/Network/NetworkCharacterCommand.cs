using Game.Character;
using Unity.Netcode;
using UnityEngine;

namespace Game.Network
{
    public struct NetworkCharacterCommand : INetworkSerializable
    {
        public uint Sequence;
        public uint LifeRevision;
        public uint EquipmentRevision;
        public Vector3 MoveDirection;
        public Vector3 LookDirection;
        public byte HeldButtons;
        public byte PressedButtons;

        public NetworkCharacterCommand(uint sequence, CharacterMoveCommand command, uint lifeRevision = 0, uint equipmentRevision = 0)
        {
            Sequence = sequence;
            LifeRevision = lifeRevision;
            EquipmentRevision = equipmentRevision;
            MoveDirection = command.MoveDirection;
            LookDirection = command.LookDirection;
            HeldButtons = (byte)command.HeldButtons;
            PressedButtons = (byte)command.PressedButtons;
        }

        public CharacterMoveCommand ToCommand() => new CharacterMoveCommand(MoveDirection,
            (CharacterHeldButtons)HeldButtons, (CharacterPressedButtons)PressedButtons, LookDirection);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref LifeRevision);
            serializer.SerializeValue(ref EquipmentRevision);
            serializer.SerializeValue(ref MoveDirection);
            serializer.SerializeValue(ref LookDirection);
            serializer.SerializeValue(ref HeldButtons);
            serializer.SerializeValue(ref PressedButtons);
        }
    }
}
