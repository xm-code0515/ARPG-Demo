using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Game.Network
{
    [DisallowMultipleComponent]
    public sealed class OnlineRoomService : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField, Range(2, 8)] private int maxPlayers = 4;
        [SerializeField] private string authenticationProfile = "";

        private ISession _room;
        private bool _busy;
        private bool _closing;

        public bool HasRoom => _room != null;
        public string JoinCode => _room?.Code ?? string.Empty;
        public string FailureReason { get; private set; }

        public bool ValidateConfiguration(NetworkManager expectedManager, out string error)
        {
            error = null;
            if (networkManager == null || networkManager != expectedManager || networkManager.gameObject != gameObject)
                error = "请将 Online Room Service 放到 NetworkRoot，并引用同一个 NetworkManager。";
            else if (NetworkManager.Singleton != networkManager)
                error = "当前网络管理器不一致，请检查是否存在重复 NetworkRoot。";
            else if (networkManager.NetworkConfig.ConnectionApproval)
                error = "互联网房间暂不使用自定义审批，请关闭 NetworkManager 的 Connection Approval。";
            else if (_busy || _room != null)
                error = "上一个房间尚未结束，请先退出。";
            return error == null;
        }

        public async Task CreateRoomAsync()
        {
            if (_busy || _room != null) throw new InvalidOperationException("已有房间或操作正在进行。");
            _busy = true;
            FailureReason = null;
            try
            {
                await AuthenticateAsync();
                var options = new SessionOptions
                {
                    Name = "英灵 Demo",
                    MaxPlayers = Mathf.Clamp(maxPlayers, 2, 8),
                    IsPrivate = true,
                    IsLocked = false
                }.WithRelayNetwork();
                _room = await MultiplayerService.Instance.CreateSessionAsync(options);
                SubscribeRoom();
            }
            finally
            {
                _busy = false;
            }
        }

        public async Task JoinRoomAsync(string code)
        {
            if (_busy || _room != null) throw new InvalidOperationException("已有房间或操作正在进行。");
            _busy = true;
            FailureReason = null;
            try
            {
                await AuthenticateAsync();
                _room = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                SubscribeRoom();
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task AuthenticateAsync()
        {
            string profile = string.IsNullOrWhiteSpace(authenticationProfile)
                ? (Application.isEditor ? "demo_editor" : "demo_player")
                : authenticationProfile.Trim();
            await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                if (AuthenticationService.Instance.Profile != profile)
                    AuthenticationService.Instance.SwitchProfile(profile);
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public async Task CloseRoomAsync()
        {
            if (_room == null) return;
            _closing = true;
            ISession room = _room;
            UnsubscribeRoom();
            try
            {
                if (room.State != SessionState.Deleted)
                {
                    if (room.IsHost) await room.AsHost().DeleteAsync();
                    else if (room.State == SessionState.Connected) await room.LeaveAsync();
                }
            }
            finally
            {
                _room = null;
                _closing = false;
                FailureReason = null;
            }
        }

        private void SubscribeRoom()
        {
            _room.Deleted += HandleRoomDeleted;
            _room.RemovedFromSession += HandleRemoved;
            _room.SessionHostChanged += HandleHostChanged;
        }

        private void UnsubscribeRoom()
        {
            if (_room == null) return;
            _room.Deleted -= HandleRoomDeleted;
            _room.RemovedFromSession -= HandleRemoved;
            _room.SessionHostChanged -= HandleHostChanged;
        }

        private void HandleRoomDeleted()
        {
            if (!_closing) FailureReason = "房间已结束，请返回主界面。";
        }

        private void HandleRemoved()
        {
            if (!_closing) FailureReason = "已离开房间，请返回主界面。";
        }

        private void HandleHostChanged(string hostId)
        {
            if (!_closing) FailureReason = "房主已离开，本次游戏结束，请返回主界面。";
        }

        private void OnDestroy() => UnsubscribeRoom();

        public static string DescribeError(Exception exception)
        {
            if (exception is AggregateException aggregate) exception = aggregate.GetBaseException();
            if (exception is SessionException session)
            {
                switch (session.Error)
                {
                    case SessionError.SessionNotFound:
                    case SessionError.SessionDeleted:
                        return "房间不存在或已结束，请核对加入码。";
                    case SessionError.NotAuthorized:
                    case SessionError.Forbidden:
                        return "云服务拒绝访问，请检查 Unity 云项目的权限和服务配置。";
                    case SessionError.RateLimitExceeded:
                        return "操作过于频繁，请稍后再试。";
                    case SessionError.InvalidParameter:
                    case SessionError.InvalidSessionIdentifier:
                        return "房间信息无效，请核对加入码。";
                }
            }
            return "互联网房间操作失败：" + exception.Message;
        }
    }
}
