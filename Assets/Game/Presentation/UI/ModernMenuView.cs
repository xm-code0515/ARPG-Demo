using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.UI
{
    // The layout and SlimUI artwork live in the prefab; this component only presents state.
    public sealed class ModernMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject mainScreen, homePage, multiplayerPage, transitionPage, pauseScreen;
        [SerializeField] private Text statusText, transitionText, pauseDetails, roomCodeText, playHint;
        [SerializeField] private RectTransform pausePanel;
        [SerializeField] private InputField joinCode;
        [SerializeField] private Button singleButton, multiplayerButton, createButton, joinButton, backButton;
        [SerializeField] private Button quitButton, resumeButton, returnButton, copyButton, retryButton;
        private Font _font;
        public event Action SingleRequested, MultiplayerRequested, CreateRequested, BackRequested;
        public event Action ResumeRequested, ReturnRequested, CopyRequested, QuitRequested;
        public event Action<string> JoinRequested;
        public bool PauseVisible => pauseScreen != null && pauseScreen.activeSelf;

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 32);
            foreach (var text in GetComponentsInChildren<Text>(true)) text.font = _font;
            singleButton.onClick.AddListener(() => SingleRequested?.Invoke());
            multiplayerButton.onClick.AddListener(() => MultiplayerRequested?.Invoke());
            createButton.onClick.AddListener(() => CreateRequested?.Invoke());
            joinButton.onClick.AddListener(() => JoinRequested?.Invoke(joinCode.text));
            backButton.onClick.AddListener(() => BackRequested?.Invoke());
            resumeButton.onClick.AddListener(() => ResumeRequested?.Invoke());
            returnButton.onClick.AddListener(() => ReturnRequested?.Invoke());
            retryButton.onClick.AddListener(() => ReturnRequested?.Invoke());
            copyButton.onClick.AddListener(() => CopyRequested?.Invoke());
            quitButton.onClick.AddListener(() => QuitRequested?.Invoke());
        }
        public void ShowMain(bool multiplayer, bool canStart, string status)
        {
            mainScreen.SetActive(true); pauseScreen.SetActive(false); playHint.gameObject.SetActive(false);
            homePage.SetActive(!multiplayer); multiplayerPage.SetActive(multiplayer); transitionPage.SetActive(false);
            singleButton.interactable = multiplayerButton.interactable = createButton.interactable = canStart;
            joinButton.interactable = canStart && !string.IsNullOrWhiteSpace(joinCode.text);
            if (statusText.text != status) statusText.text = status;
        }
        public void ShowTransition(string status, bool failed)
        {
            mainScreen.SetActive(true); pauseScreen.SetActive(false); playHint.gameObject.SetActive(false);
            homePage.SetActive(false); multiplayerPage.SetActive(false); transitionPage.SetActive(true);
            transitionText.text = status; statusText.text = "";
            retryButton.gameObject.SetActive(failed);
        }
        public void ShowPlaying(bool menuOpen, bool online, bool host, string code)
        {
            mainScreen.SetActive(false); pauseScreen.SetActive(menuOpen); playHint.gameObject.SetActive(!menuOpen);
            playHint.text = online ? "Esc：游戏菜单    房间加入码：" + code : "Esc：游戏菜单";
            copyButton.gameObject.SetActive(online); roomCodeText.gameObject.SetActive(online);
            pausePanel.sizeDelta = new Vector2(600, online ? 650 : 540);
            pauseDetails.rectTransform.anchoredPosition = new Vector2(60, online ? -560 : -450);
            roomCodeText.text = online ? "房间加入码：" + code : "";
            pauseDetails.text = host && online ? "返回主界面会结束当前房间。" : "返回主界面将结束当前游戏会话。";
        }
        public void Hide()
        {
            mainScreen.SetActive(false); pauseScreen.SetActive(false); playHint.gameObject.SetActive(false);
        }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
    }
}
