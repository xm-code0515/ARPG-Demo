using System;
using UnityEngine;

namespace Game.Character
{
    [Flags]
    public enum CharacterHeldButtons : byte
    {
        None = 0,
        Sprint = 1,
        Defend = 2,
        Attack = 4
    }

    [Flags]
    public enum CharacterPressedButtons : byte
    {
        None = 0,
        Jump = 1,
        Attack = 2,
        Dodge = 4
    }

    public readonly struct CharacterMoveCommand
    {
        public Vector3 MoveDirection { get; }
        public Vector3 LookDirection { get; }
        public CharacterHeldButtons HeldButtons { get; }
        public CharacterPressedButtons PressedButtons { get; }
        public bool SprintHeld => (HeldButtons & CharacterHeldButtons.Sprint) != 0;
        public bool JumpPressed => (PressedButtons & CharacterPressedButtons.Jump) != 0;
        public bool AttackHeld => (HeldButtons & CharacterHeldButtons.Attack) != 0;
        public bool AttackPressed => (PressedButtons & CharacterPressedButtons.Attack) != 0;
        public bool DodgePressed => (PressedButtons & CharacterPressedButtons.Dodge) != 0;

        public CharacterMoveCommand(Vector3 moveDirection, bool sprintHeld, bool jumpPressed)
            : this(moveDirection, sprintHeld ? CharacterHeldButtons.Sprint : CharacterHeldButtons.None,
                jumpPressed ? CharacterPressedButtons.Jump : CharacterPressedButtons.None) { }

        public CharacterMoveCommand(Vector3 moveDirection, CharacterHeldButtons heldButtons,
            CharacterPressedButtons pressedButtons, Vector3 lookDirection = default)
        {
            MoveDirection = Vector3.ClampMagnitude(new Vector3(moveDirection.x, 0f, moveDirection.z), 1f);
            HeldButtons = heldButtons;
            PressedButtons = pressedButtons;
            LookDirection = Vector3.ClampMagnitude(new Vector3(lookDirection.x, 0f, lookDirection.z), 1f);
        }
    }

    /// <summary>
    /// 【新增】输入模块通过稳定接口提交意图，不反向依赖 NetworkCharacter / NGO。
    /// 【第五步】NetworkCharacter 把意图发送给服务器，核心接口仍不包含网络类型。
    /// </summary>
    public interface ICharacterCommandReceiver
    {
        void ReceiveCommand(CharacterMoveCommand command);
    }
}
