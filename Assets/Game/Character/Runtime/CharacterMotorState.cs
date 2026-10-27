using UnityEngine;

namespace Game.Character
{
    public readonly struct CharacterMotorState
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Velocity;
        public readonly float VerticalVelocity;
        public readonly bool IsGrounded;
        public readonly bool IsSprinting;
        public readonly Vector3 KnockbackVelocity;
        public readonly float KnockbackRemaining;

        public CharacterMotorState(Vector3 position, Quaternion rotation, Vector3 velocity,
            float verticalVelocity, bool isGrounded, bool isSprinting,
            Vector3 knockbackVelocity = default, float knockbackRemaining = 0f)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            VerticalVelocity = verticalVelocity;
            IsGrounded = isGrounded;
            IsSprinting = isSprinting;
            KnockbackVelocity = knockbackVelocity;
            KnockbackRemaining = knockbackRemaining;
        }

        public MovementState ToMovementState()
        {
            Vector3 horizontal = new Vector3(Velocity.x, 0f, Velocity.z);
            float speed = horizontal.magnitude;
            return new MovementState(speed > 0.001f ? horizontal / speed : Vector3.zero,
                Velocity, speed, Velocity.y, IsGrounded, IsSprinting);
        }
    }
}
