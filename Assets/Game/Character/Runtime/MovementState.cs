using UnityEngine;

namespace Game.Character
{
    public readonly struct MovementState
    {
        public Vector3 MoveDirection { get; }
        public Vector3 Velocity { get; }

        public float HorizontalSpeed { get; }
        public float VerticalVelocity { get; }

        public bool IsGrounded { get; }
        public bool IsSprinting { get; }

        public MovementState(
            Vector3 moveDirection,
            Vector3 velocity,
            float horizontalSpeed,
            float verticalVelocity,
            bool isGrounded,
            bool isSprinting)
        {
            MoveDirection = moveDirection;
            Velocity = velocity;

            HorizontalSpeed = horizontalSpeed;
            VerticalVelocity = verticalVelocity;

            IsGrounded = isGrounded;
            IsSprinting = isSprinting;
        }
    }
}