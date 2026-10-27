using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Network
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class GameMainMenu : MonoBehaviour
    {
        [SerializeField] private GameSessionController sessionController;
        [SerializeField] private ModernMenuView menuView;
        private NetworkPlayerItemsUIBinder _itemsUi;
        private bool _showMultiplayer, _pauseOpen, _otherUiWasOpen;
        public bool IsPauseMenuOpen => _pauseOpen;

        private void OnEnable()
        {
            if (menuView == null) return;
            menuView.SingleRequested += StartSingle;
            menuView.MultiplayerRequested += ShowMultiplayer;
            menuView.CreateRequested += CreateRoom;
            menuView.JoinRequested += JoinRoom;
            menuView.BackRequested += Back;
            menuView.ResumeRequested += Resume;
            menuView.ReturnRequested += Return;
            menuView.CopyRequested += CopyCode;
            menuView.QuitRequested += Quit;
        }
        private void OnDisable()
        {
            SetPauseMenuOpen(false);
            if (menuView == null) return;
            menuView.SingleRequested -= StartSingle;
            menuView.MultiplayerRequested -= ShowMultiplayer;
            menuView.CreateRequested -= CreateRoom;
            menuView.JoinRequested -= JoinRoom;
            menuView.BackRequested -= Back;
            menuView.ResumeRequested -= Resume;
            menuView.ReturnRequested -= Return;
            menuView.CopyRequested -= CopyCode;
            menuView.QuitRequested -= Quit;
        }
        private void Update()
        {
            if (sessionController == null || menuView == null) return;
            if (sessionController.State == GameSessionState.Playing)
            {
                if (_itemsUi == null) _itemsUi = FindAnyObjectByType<NetworkPlayerItemsUIBinder>();
                bool otherOpen = _itemsUi != null && _itemsUi.IsOtherUiOpen;
                if (Application.isFocused && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame &&
                    !otherOpen && !_otherUiWasOpen)
                    SetPauseMenuOpen(!_pauseOpen);
                _otherUiWasOpen = otherOpen;
                if (otherOpen) menuView.Hide();
                else menuView.ShowPlaying(_pauseOpen, sessionController.IsOnline,
                    sessionController.Mode == GameSessionMode.OnlineHost, sessionController.RoomCode);
            }
            else
            {
                if (_pauseOpen) SetPauseMenuOpen(false);
                _otherUiWasOpen = false;
                if (sessionController.State == GameSessionState.MainMenu)
                    menuView.ShowMain(_showMultiplayer, sessionController.CanStartSinglePlayer, sessionController.Status);
                else menuView.ShowTransition(sessionController.Status, sessionController.State == GameSessionState.Failed);
            }
        }
        public void SetPauseMenuOpen(bool value)
        {
            if (value && (sessionController == null || sessionController.State != GameSessionState.Playing)) return;
            _pauseOpen = value;
            if (_itemsUi == null) _itemsUi = FindAnyObjectByType<NetworkPlayerItemsUIBinder>();
            if (_itemsUi != null) _itemsUi.SetMenuOpen(value);
        }
        private void StartSingle() { _showMultiplayer = false; sessionController.StartSinglePlayer(); }
        private void ShowMultiplayer() => _showMultiplayer = true;
        private void CreateRoom() => sessionController.CreateOnlineRoom();
        private void JoinRoom(string code) => sessionController.JoinOnlineRoom(code);
        private void Back() => _showMultiplayer = false;
        private void Resume() => SetPauseMenuOpen(false);
        private void Return() { SetPauseMenuOpen(false); _showMultiplayer = false; sessionController.ReturnToMainMenu(); }
        private void CopyCode() => GUIUtility.systemCopyBuffer = sessionController.RoomCode;
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
