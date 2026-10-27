using Game.Presentation.Cameras;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// 【新增：联机第四步】挂到 Playground 的 Main Camera，显式引用摄像机控制器。
    /// 等本机 Owner 角色注册后绑定 CameraTarget，并为该角色启用指令输入。
    /// 不找远程玩家、不把摄像机放入玩家 Prefab，不让表现层反向依赖 NGO。
    /// 玩家退出或本组件禁用时解除输入和相机绑定，后加入者不会抢走本机摄像机。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class NetworkLocalPlayerBinder : MonoBehaviour
    {
        [SerializeField] private ThirdPersonCameraController cameraController;
        private NetworkCharacter _boundPlayer;

        private void Awake()
        {
            if (cameraController == null)
            {
                Debug.LogError("[NetworkLocalPlayerBinder] 请拖入 Main Camera 的摄像机控制器。", this);
                enabled = false;
                return;
            }
            cameraController.enabled = false;
            // 【新增：联机鼠标修复】联机测试曾关闭场景的 Lock Cursor。
            // 本地角色绑定后应恢复隐藏并锁定鼠标，由相机管理 Esc / 焦点 / 解绑释放。
            cameraController.SetCursorLockEnabled(true);
            cameraController.SetTarget(null);
        }

        private void Update()
        {
            NetworkCharacter current = NetworkCharacter.LocalPlayer;
            if (current != null && (!current.IsSpawned || !current.IsOwner)) current = null;
            // Unity 会把已销毁对象比较为 null；这里按托管引用比较，确保销毁后仍清理相机。
            if (ReferenceEquals(_boundPlayer, current)) return;
            Unbind();
            if (current == null || cameraController == null) return;
            _boundPlayer = current;
            cameraController.SetTarget(current.CameraTarget);
            cameraController.enabled = true;
            current.BindLocalInput(cameraController.transform);
        }

        private void OnDisable() => Unbind();

        private void Unbind()
        {
            if (_boundPlayer != null) _boundPlayer.ReleaseLocalInput();
            _boundPlayer = null;
            if (cameraController != null)
            {
                cameraController.enabled = false;
                cameraController.SetTarget(null);
            }
        }
    }
}
