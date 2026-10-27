using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Cameras
{
    public sealed class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Input")]
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference zoomAction;

        [Header("Rotation")]
        [SerializeField] private float horizontalSensitivity = 0.12f;
        [SerializeField] private float verticalSensitivity = 0.10f;

        [SerializeField] private float initialPitch = 15f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 65f;

        [SerializeField] private bool invertY = false;

        [Header("Distance")]
        [SerializeField] private float distance = 3.5f;
        [SerializeField] private float minDistance = 2.5f;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private float zoomSensitivity = 0.01f;

        [Header("Follow")]
        [SerializeField] private float followSharpness = 15f;
        [SerializeField] private Vector2 framingOffset = new Vector2(.3f, .47f);

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float collisionPadding = 0.1f;
        [SerializeField] private float minCollisionDistance = 0.5f;

        [SerializeField] private float collisionPullInSharpness = 30f;
        [SerializeField] private float collisionReturnSharpness = 10f;

        [Header("Cursor")]
        [SerializeField] private bool lockCursor = true;

        // 【新增：联机鼠标修复】记录临时释放状态及本相机是否接管鼠标。
        // Esc / 切换窗口释放鼠标，右键恢复；避免每帧强制锁定导致无法操作测试面板。
        private bool _cursorReleased;
        private bool _ownsCursor;
        private bool _gameplayInputBlocked;
        private int _resumeInputFrame = -1;
        public bool GameplayInputBlocked => _gameplayInputBlocked;

        public void SetGameplayInputBlocked(bool value)
        {
            bool wasBlocked = _gameplayInputBlocked;
            _gameplayInputBlocked = value;
            if (wasBlocked && !value) _resumeInputFrame = Time.frameCount;
            if (value)
            {
                ReleaseCursor();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (isActiveAndEnabled && target != null && Application.isFocused && lockCursor)
            { _cursorReleased = false; CaptureCursor(); }
        }

        private float _yaw;
        private float _pitch;

        private float _currentDistance;

        private Vector3 _smoothedTargetPosition;

        private void Awake()
        {
            SetTarget(target);
        }

        // 【新增：联机第四步】允许场景摄像机在网络玩家生成后绑定目标。
        // 同时重置朝向、平滑位置和距离，避免沿用旧角色的跟随状态。
        // 这是通用表现层接口，本类不依赖 Network / NGO。
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target == null) ReleaseCursor();
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _smoothedTargetPosition = target.position;
            }

            _pitch = Mathf.Clamp(
                initialPitch,
                minPitch,
                maxPitch
            );

            distance = Mathf.Clamp(
                distance,
                minDistance,
                maxDistance
            );

            _currentDistance = distance;
        }

        // 【新增：联机鼠标修复】由本地玩家绑定器明确启用鼠标锁定。
        // 场景中为联机测试关闭的 Inspector 设置不会再影响正式操控；不依赖 NGO。
        public void SetCursorLockEnabled(bool enabled)
        {
            lockCursor = enabled;
            _cursorReleased = false;
            if (!enabled) ReleaseCursor();
            else if (isActiveAndEnabled && target != null && Application.isFocused) CaptureCursor();
        }

        private void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _ownsCursor = true;
        }

        private void ReleaseCursor()
        {
            if (!_ownsCursor) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _ownsCursor = false;
        }

        // 【新增：联机鼠标修复】切换 Host / Client 窗口时释放鼠标，回到窗口后
        // 通过右键明确恢复，避免刚切回 Unity 就抢走编辑器或测试面板的鼠标。
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) return;
            _cursorReleased = true;
            ReleaseCursor();
        }

        private void UpdateCursor()
        {
            if (_gameplayInputBlocked || !lockCursor || !Application.isFocused) return;
            if (Time.frameCount != _resumeInputFrame && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _cursorReleased = true;
                ReleaseCursor();
            }
            else if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                _cursorReleased = false;
                CaptureCursor();
            }
            else if (!_cursorReleased)
            {
                CaptureCursor();
            }
        }

        private void OnEnable()
        {
            if (lookAction != null)
            {
                lookAction.action.Enable();
            }

            if (zoomAction != null)
            {
                zoomAction.action.Enable();
            }

            _cursorReleased = false;
            if (lockCursor && target != null && Application.isFocused) CaptureCursor();
        }

        private void OnDisable()
        {
            if (lookAction != null)
            {
                lookAction.action.Disable();
            }

            if (zoomAction != null)
            {
                zoomAction.action.Disable();
            }

            ReleaseCursor();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            UpdateCursor();
            // 【修改：联机鼠标修复】鼠标释放或窗口失焦时停止视角/缩放输入，
            // 点击测试面板不会同时旋转镜头；相机仍正常跟随角色。
            if (!_gameplayInputBlocked && Application.isFocused && (!lockCursor || Cursor.lockState == CursorLockMode.Locked))
            {
                UpdateLook();
                UpdateZoom();
            }
            UpdateTargetFollow();
            UpdateCameraTransform();
        }

        private void UpdateLook()
        {
            if (lookAction == null)
                return;

            Vector2 lookInput =
                lookAction.action.ReadValue<Vector2>();

            _yaw +=
                lookInput.x *
                horizontalSensitivity;

            float verticalDirection =
                invertY ? 1f : -1f;

            _pitch +=
                lookInput.y *
                verticalSensitivity *
                verticalDirection;

            _pitch = Mathf.Clamp(
                _pitch,
                minPitch,
                maxPitch
            );

            _yaw = Mathf.Repeat(
                _yaw,
                360f
            );
        }

        private void UpdateZoom()
        {
            if (zoomAction == null)
                return;

            float scroll =
                zoomAction.action.ReadValue<float>();

            distance -=
                scroll *
                zoomSensitivity;

            distance = Mathf.Clamp(
                distance,
                minDistance,
                maxDistance
            );
        }

        private void UpdateTargetFollow()
        {
            float followFactor =
                1f -
                Mathf.Exp(
                    -followSharpness *
                    Time.deltaTime
                );

            _smoothedTargetPosition =
                Vector3.Lerp(
                    _smoothedTargetPosition,
                    target.position,
                    followFactor
                );
        }

        private void UpdateCameraTransform()
        {
            Quaternion rotation =
                Quaternion.Euler(
                    _pitch,
                    _yaw,
                    0f
                );

            Vector3 backwardDirection =
                -(rotation * Vector3.forward);

            Vector3 cameraPivot = _smoothedTargetPosition +
                rotation * new Vector3(framingOffset.x, framingOffset.y, 0f);

            float safeDistance =
                GetSafeCameraDistance(
                    cameraPivot,
                    backwardDirection
                );

            float collisionSharpness;

            if (safeDistance < _currentDistance)
            {
                collisionSharpness =
                    collisionPullInSharpness;
            }
            else
            {
                collisionSharpness =
                    collisionReturnSharpness;
            }

            float collisionFactor =
                1f -
                Mathf.Exp(
                    -collisionSharpness *
                    Time.deltaTime
                );

            _currentDistance =
                Mathf.Lerp(
                    _currentDistance,
                    safeDistance,
                    collisionFactor
                );

            Vector3 cameraPosition =
                cameraPivot +
                backwardDirection *
                _currentDistance;

            transform.SetPositionAndRotation(
                cameraPosition,
                rotation
            );
        }

        private float GetSafeCameraDistance(Vector3 origin, Vector3 direction)
        {
            if (Physics.SphereCast(
                origin,
                collisionRadius,
                direction,
                out RaycastHit hit,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
            {
                float safeDistance =
                    hit.distance -
                    collisionPadding;

                return Mathf.Clamp(
                    safeDistance,
                    minCollisionDistance,
                    distance
                );
            }

            return distance;
        }
    }
}
