using System;
using TMPro;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Parallax.UI
{
    /// <summary>Scene-owned title screen; only the existing settings menu persists.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplayScene = "StartScene";
        [SerializeField] private TMP_FontAsset primaryFont, secondaryFont;
        private PanelSettings panel;
        private GameSettingsMenu settings;
        private VisualElement home;
        private Button play, returnFocus;
        private Label status;
        private bool loading;

        private void Start()
        {
            settings = GameSettingsMenu.Instance;
            if (settings == null)
                settings = new GameObject("Settings Menu").AddComponent<GameSettingsMenu>();
            settings.Close();
            settings.SetMainMenuContext(true);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var tree = Resources.Load<VisualTreeAsset>("UI/MainMenu");
            var theme = Resources.Load<ThemeStyleSheet>("UI/SettingsTheme");
            if (tree == null || theme == null)
            {
                Debug.LogError("PARALLAX main menu UI resources are missing.", this);
                enabled = false;
                return;
            }
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "PARALLAX Main Menu Panel";
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 0;
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            var root = document.rootVisualElement;
            // UI Toolkit uses TextCore; reuse the source faces of our established TMP assets.
            foreach (var text in root.Query<TextElement>().ToList())
            {
                var font = text is Button || text.name == "game-title" ? primaryFont : secondaryFont;
                if (font != null && font.sourceFontFile != null)
                    text.style.unityFontDefinition = FontDefinition.FromFont(font.sourceFontFile);
            }
            home = root.Q("menu-home");
            status = root.Q<Label>("menu-status");
            play = root.Q<Button>("play");
            play.clicked += Play;
            root.Q<Button>("settings").clicked += () => OpenPanel(false, root.Q<Button>("settings"));
            root.Q<Button>("controls").clicked += () => OpenPanel(true, root.Q<Button>("controls"));
            root.Q<Button>("quit").clicked += Quit;
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var shell = root.Q("main-menu");
                shell.EnableInClassList("narrow", evt.newRect.width < 850);
                shell.EnableInClassList("short", evt.newRect.height < 620);
            });
            settings.OpenChanged += OnSettingsChanged;
            play.schedule.Execute(() => { if (!settings.IsOpen) play.Focus(); });
        }

        private void OpenPanel(bool controls, Button source)
        {
            if (loading) return;
            returnFocus = source;
            if (controls) settings.OpenControls();
            else settings.Open();
        }

        private void OnSettingsChanged(bool open)
        {
            // Removing the home controls also prevents keyboard focus behind the modal.
            home.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            if (!open && !loading)
                (returnFocus ?? play).schedule.Execute(() => (returnFocus ?? play).Focus());
        }

        public void Play()
        {
            if (loading || settings.IsOpen) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                status.text = "Unable to start. The gameplay scene is unavailable.";
                return;
            }
            loading = true;
            home.Query<Button>().ForEach(button => button.SetEnabled(false));
            status.text = "Establishing connection…";
            play.text = "Loading…";
            GameSettings.Save();
            GlobalState.ResetForNewGame();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            // Request pointer lock during the user's gesture, including browser builds.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            try
            {
                ContextIntroFlow.Request(gameplayScene);
                SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ContextIntroFlow.Cancel();
                loading = false;
                settings.SetMainMenuContext(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                home.Query<Button>().ForEach(button => button.SetEnabled(true));
                play.text = "START GAME";
                status.text = "Connection failed. Please try again.";
                Debug.LogException(exception, this);
            }
        }

        public void Quit()
        {
            GameSettings.Save();
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                status.text = "To leave PARALLAX, close this browser tab.";
            else if (Application.isEditor)
                status.text = "Quit is available in the game build. Use Stop to leave Play Mode.";
            else
                Application.Quit();
        }

        private void OnDestroy()
        {
            if (settings != null)
            {
                settings.OpenChanged -= OnSettingsChanged;
                settings.SetMainMenuContext(false);
            }
            if (panel != null) Destroy(panel);
        }
    }
}
