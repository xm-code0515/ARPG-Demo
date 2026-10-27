using UnityEngine;

namespace Game.Character
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour, IMovementStateSource
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedVelocity = -2f;
        [SerializeField] private bool immuneToKnockback;

        private CharacterController _controller;

        private Vector2 _moveInput;

        private Vector3 _worldMoveDirection;
        private bool _useWorldMoveDirection;

        private bool _sprintInput;
        private bool _jumpRequested;

        private float _verticalVelocity;
        private bool _isGrounded;
        private Vector3 _knockbackVelocity;
        private float _knockbackRemaining;

        public bool IsGrounded => _isGrounded;
        public bool ManualSimulation { get; set; }
        public bool IsKnockedBack => _knockbackRemaining > 0f;
        public bool ImmuneToKnockback => immuneToKnockback;

        public void ApplyKnockback(Vector3 direction, float distance, float duration)
        {
            if (immuneToKnockback || distance <= 0f) return;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < .0001f) return;
            _knockbackRemaining = Mathf.Max(.01f, duration);
            _knockbackVelocity = direction.normalized * (2f * distance / _knockbackRemaining);
        }

        public void ClearKnockback()
        {
            _knockbackVelocity = Vector3.zero;
            _knockbackRemaining = 0f;
        }

        public void PauseMovement()
        {
            _jumpRequested = _sprintInput = false;
            CurrentMovementState = new MovementState(Vector3.zero, Vector3.zero, 0f, 0f, _isGrounded, false);
        }

        public bool IsSprinting =>
            _sprintInput &&
            (_useWorldMoveDirection ? _worldMoveDirection.sqrMagnitude : _moveInput.sqrMagnitude) > 0.01f &&
            IsGrounded;

        public MovementState CurrentMovementState { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public void SetMoveInput(Vector2 input)
        {
            _useWorldMoveDirection = false;
            _moveInput = Vector2.ClampMagnitude(input, 1f);
        }


        public void ApplyMoveCommand(CharacterMoveCommand command)
        {
            _useWorldMoveDirection = true;
            _worldMoveDirection = command.MoveDirection;
            _sprintInput = command.SprintHeld;
            if (command.JumpPressed) RequestJump();
        }

        public void ResetMovement()
        {
            _moveInput = Vector2.zero;
            _worldMoveDirection = Vector3.zero;
            _useWorldMoveDirection = false;
            _sprintInput = false;
            _jumpRequested = false;
            _verticalVelocity = 0f;
            _isGrounded = false;
            ClearKnockback();
            CurrentMovementState = default;
        }

        public CharacterMotorState CaptureState() => new CharacterMotorState(transform.position,
            transform.rotation, CurrentMovementState.Velocity, _verticalVelocity, _isGrounded,
            CurrentMovementState.IsSprinting, _knockbackVelocity, _knockbackRemaining);

        public void RestoreState(CharacterMotorState state)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
            _controller.enabled = wasEnabled;
            _verticalVelocity = state.VerticalVelocity;
            _isGrounded = state.IsGrounded;
            _knockbackVelocity = state.KnockbackVelocity;
            _knockbackRemaining = state.KnockbackRemaining;
            _jumpRequested = false;
            CurrentMovementState = state.ToMovementState();
        }

        public void Simulate(CharacterMoveCommand command, float deltaTime)
        {
            Simulate(command, deltaTime, jumpHeight, walkSpeed, walkSpeed > 0f ? sprintSpeed / walkSpeed : 1f);
        }

        public void Simulate(CharacterMoveCommand command, float deltaTime, float requestedJumpHeight,
            float requestedMoveSpeed, float sprintMultiplier, Vector3 attackDisplacement = default)
        {
            if (deltaTime <= 0f) return;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (!_controller.enabled) return;
            ApplyMoveCommand(command);
            UpdateMovement(deltaTime, requestedJumpHeight, requestedMoveSpeed, requestedMoveSpeed * sprintMultiplier,
                Vector3.ProjectOnPlane(attackDisplacement, Vector3.up));
        }

        public void SetSprintInput(bool isPressed)
        {
            _sprintInput = isPressed;
        }

        public void RequestJump()
        {
            _jumpRequested = true;
        }

        private void Update()
        {
            if (!ManualSimulation) UpdateMovement(Time.deltaTime);
        }

        private void UpdateMovement(float deltaTime)
        {
            UpdateMovement(deltaTime, jumpHeight, walkSpeed, sprintSpeed);
        }

        private void UpdateMovement(float deltaTime, float requestedJumpHeight, float requestedWalkSpeed, float requestedSprintSpeed,
            Vector3 attackDisplacement = default)
        {
            if (deltaTime <= 0f) return;
            Vector3 previousPosition = transform.position;
            UpdateGroundedState();
            HandleJump(requestedJumpHeight);
            ApplyGravity(deltaTime);

            Vector3 moveDirection = CalculateMoveDirection();

            RotateCharacter(moveDirection, deltaTime);
            MoveCharacter(moveDirection, deltaTime, requestedWalkSpeed, requestedSprintSpeed, attackDisplacement);
            _isGrounded = _controller.isGrounded;
            UpdateMovementState((transform.position - previousPosition) / deltaTime);

            _jumpRequested = false;
        }

        private void UpdateGroundedState()
        {
            if (IsGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedVelocity;
            }
        }

        private void HandleJump(float requestedJumpHeight)
        {
            if (!_jumpRequested)
                return;

            if (!IsGrounded)
                return;

            _verticalVelocity =
                Mathf.Sqrt(requestedJumpHeight * -2f * gravity);
        }

        private void ApplyGravity(float deltaTime)
        {
            _verticalVelocity += gravity * deltaTime;
        }

        private Vector3 CalculateMoveDirection()
        {
            if (_useWorldMoveDirection)
                return _worldMoveDirection;

            if (cameraTransform == null)
                return Vector3.zero;

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 direction =
                forward * _moveInput.y +
                right * _moveInput.x;

            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return direction;
        }

        private void RotateCharacter(Vector3 moveDirection, float deltaTime)
        {
            if (moveDirection.sqrMagnitude < 0.001f)
                return;

            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * deltaTime
            );
        }

        private void MoveCharacter(Vector3 moveDirection, float deltaTime, float requestedWalkSpeed, float requestedSprintSpeed,
            Vector3 attackDisplacement)
        {
            float moveSpeed =
                IsSprinting ? requestedSprintSpeed : requestedWalkSpeed;

            Vector3 velocity =
                moveDirection * moveSpeed;

            velocity.y = _verticalVelocity;

            Vector3 knockback = Vector3.zero;
            if (IsKnockedBack)
            {
                float step = Mathf.Min(deltaTime, _knockbackRemaining);
                float fraction = step / _knockbackRemaining;
                knockback = _knockbackVelocity * (step * (1f - fraction * .5f));
                _knockbackVelocity *= 1f - fraction;
                _knockbackRemaining = Mathf.Max(0f, _knockbackRemaining - step);
                if (_knockbackRemaining < .00001f) ClearKnockback();
            }
            _controller.Move(velocity * deltaTime + attackDisplacement + knockback);
        }

        private void UpdateMovementState(Vector3 velocity)
        {
            Vector3 horizontalVelocity =
                new Vector3(
                    velocity.x,
                    0f,
                    velocity.z
                );

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            Vector3 moveDirection =
                horizontalSpeed > 0.001f
                    ? horizontalVelocity.normalized
                    : Vector3.zero;

            CurrentMovementState =
                new MovementState(
                    moveDirection,
                    velocity,
                    horizontalSpeed,
                    velocity.y,
                    IsGrounded,
                    IsSprinting
                );
        }
    }
}
