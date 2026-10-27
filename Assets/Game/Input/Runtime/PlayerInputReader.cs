using Game.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        // 【修改：联机第四步】保留原有单机 Motor 路径；绑定接收器后，改为提交
        // CharacterMoveCommand，不直接驱动 Motor。输入模块仍然不知道 NGO。
        [Header("Input Actions")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference sprintAction;

        [Header("Target")]
        [SerializeField] private CharacterMotor characterMotor;

        [Header("Attack Aim")]
        [SerializeField, Min(1f)] private float attackAimDistance = 100f;
        [SerializeField] private LayerMask attackAimLayers = Physics.DefaultRaycastLayers;

        private ICharacterCommandReceiver _commandReceiver;
        private Transform _commandCamera;
        private Camera _aimCamera;
        private readonly RaycastHit[] _aimHits = new RaycastHit[32];
        private InputAction _move;
        private InputAction _jump;
        private InputAction _sprint;
        public event System.Action InteractPressed;
        public event System.Action InventoryPressed;
        public event System.Action<int> HotbarPressed;
        public bool GameplayInputBlocked { get; private set; }
        private bool _waitForAttackRelease;

        public void SetGameplayInputBlocked(bool value)
        {
            GameplayInputBlocked = value;
            _waitForAttackRelease = true;
            if (value) _commandReceiver?.ReceiveCommand(default);
        }

        public void SetCommandReceiver(ICharacterCommandReceiver receiver, Transform commandCamera)
        {
            _commandReceiver = receiver;
            _commandCamera = commandCamera;
            _aimCamera = commandCamera != null ? commandCamera.GetComponent<Camera>() : null;
        }

        private void OnEnable()
        {
            if (moveAction == null || jumpAction == null || sprintAction == null)
            {
                Debug.LogError("[PlayerInputReader] 请设置 Move / Jump / Sprint InputAction 引用。", this);
                enabled = false;
                return;
            }
            // 【修改】每个输入读取器使用自己的 Action 副本；远程角色禁用时
            // 不会 Disable 共用资源里的 Action，从而意外关闭本地角色输入。
            _move = moveAction.action.Clone();
            _jump = jumpAction.action.Clone();
            _sprint = sprintAction.action.Clone();
            _move.Enable();
            _jump.Enable();
            _sprint.Enable();
        }

        private void OnDisable()
        {
            _commandReceiver?.ReceiveCommand(default);
            if (_commandReceiver == null && characterMotor != null)
            {
                characterMotor.SetMoveInput(Vector2.zero);
                characterMotor.SetSprintInput(false);
            }
            _move?.Dispose();
            _jump?.Dispose();
            _sprint?.Dispose();
            _move = _jump = _sprint = null;
        }

        private void Update()
        {
            if (_move == null) return;
            // 【新增】失去窗口焦点时发送零输入，避免两个游戏窗口同时响应键盘。
            bool focused = Application.isFocused;
            if (focused && Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame) InteractPressed?.Invoke();
                if (Keyboard.current.tabKey.wasPressedThisFrame) InventoryPressed?.Invoke();
                else if (GameplayInputBlocked && Keyboard.current.escapeKey.wasPressedThisFrame) InventoryPressed?.Invoke();
            }
            focused &= !GameplayInputBlocked;
            if (focused && Keyboard.current != null)
                for (int i = 0; i < 8; i++)
                    if (Keyboard.current[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) HotbarPressed?.Invoke(i);
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed) _waitForAttackRelease = false;
            Vector2 move = focused ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            bool sprint = focused && _sprint.IsPressed();
            bool jump = focused && _jump.WasPressedThisFrame();
            bool attackHeld = focused && !_waitForAttackRelease && Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool attackPressed = focused && !_waitForAttackRelease && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (_commandReceiver != null)
            {
                Vector3 direction = Vector3.zero;
                if (_commandCamera != null)
                {
                    // 仅传世界空间水平意图，不传摄像机对象。服务器不需要本地摄像机。
                    Quaternion yaw = Quaternion.Euler(0f, _commandCamera.eulerAngles.y, 0f);
                    direction = yaw * new Vector3(move.x, 0f, move.y);
                }
                CharacterHeldButtons held = sprint ? CharacterHeldButtons.Sprint : CharacterHeldButtons.None;
                CharacterPressedButtons pressed = jump ? CharacterPressedButtons.Jump : CharacterPressedButtons.None;
                if (attackHeld) held |= CharacterHeldButtons.Attack;
                if (attackPressed) pressed |= CharacterPressedButtons.Attack;
                if (focused && Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
                    pressed |= CharacterPressedButtons.Dodge;
                Vector3 look = focused ? CalculateAttackDirection() : Vector3.zero;
                _commandReceiver.ReceiveCommand(new CharacterMoveCommand(direction, held, pressed, look));
                return;
            }

            // 【保留】普通单机 Player 未绑定接收器时，仍使用原来的相机相对 Motor。
            if (characterMotor != null)
            {
                characterMotor.SetMoveInput(move);
                characterMotor.SetSprintInput(sprint);
                if (jump) characterMotor.RequestJump();
            }
        }

        private Vector3 CalculateAttackDirection()
        {
            Transform character = characterMotor != null ? characterMotor.transform : transform;
            Vector3 fallback = _commandCamera != null ? Vector3.ProjectOnPlane(_commandCamera.forward, Vector3.up) : character.forward;
            if (fallback.sqrMagnitude < .0001f) fallback = character.forward;
            if (_aimCamera == null) return fallback.normalized;

            Ray ray = _aimCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
            float distance = Mathf.Max(1f, attackAimDistance);
            Vector3 point = ray.GetPoint(distance);
            int count = Physics.RaycastNonAlloc(ray, _aimHits, distance, attackAimLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = count < _aimHits.Length ? _aimHits
                : Physics.RaycastAll(ray, distance, attackAimLayers, QueryTriggerInteraction.Ignore);
            if (hits != _aimHits) count = hits.Length;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(character) || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
                point = hits[i].point;
            }
            Vector3 direction = Vector3.ProjectOnPlane(point - character.position, Vector3.up);
            return direction.sqrMagnitude > .0001f ? direction.normalized : fallback.normalized;
        }
    }
}
