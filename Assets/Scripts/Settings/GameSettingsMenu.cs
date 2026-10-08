using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Parallax.Settings
{
    /// <summary>Persistent pause/settings overlay. Player input should check IsOpen before moving or looking.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameSettingsMenu : MonoBehaviour
    {
        public static GameSettingsMenu Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsMainMenuContext { get; private set; }
        private bool controlsOnly;
        // Ignore the click/key that resumes the game until the next frame.
        public static bool StartupInputBlocked { get; set; }
        public static bool GameplayInputBlocked => StartupInputBlocked || (Instance != null &&
            (Instance.IsMainMenuContext || Instance.IsOpen || Instance.lastClosedFrame == Time.frameCount));
        private int lastClosedFrame = -1;
        public event Action<bool> OpenChanged;

        private PanelSettings panelSettings;
        private UIDocument document;
        private VisualElement boundRoot;
        private VisualElement overlay;
        private VisualElement shell;
        private Button launcher;
        private Button closeButton;
        private ScrollView settingsScroll;
        private Slider volume;
        private Slider sensitivity;
        private DropdownField quality;
        private Toggle invert;
        private Toggle fullscreen;
        private Label volumeValue;
        private Label sensitivityValue;
        private Label status;
        private float previousTimeScale;
        private bool previousAudioPause;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private bool lastFullscreen;
        private int lastScrollFrame = -1;
        private float scrollUsedThisFrame;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameSettings.Initialize();
            BuildInterface();
        }

        private void BuildInterface()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/SettingsMenu");
            var theme = Resources.Load<ThemeStyleSheet>("UI/SettingsTheme");
            if (tree == null || theme == null)
            {
                Debug.LogError("PARALLAX settings UI resources are missing.", this);
                enabled = false;
                return;
            }

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "PARALLAX Settings Panel";
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1280, 720);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 100;

            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = tree;
            BindInterface();
            GameSettings.Changed += RefreshValues;
        }

        private void BindInterface()
        {
            var root = document.rootVisualElement;
            boundRoot = root;
            root.pickingMode = PickingMode.Ignore;
            shell = root.Q("settings-root");
            overlay = root.Q("settings-overlay");
            launcher = root.Q<Button>("open-settings");
            closeButton = root.Q<Button>("close-settings");
            settingsScroll = root.Q<ScrollView>("settings-scroll");
            // Use a modest wheel increment instead of the default page-sized jump.
            settingsScroll.mouseWheelScrollSize = 3f;
            settingsScroll.RegisterCallback<WheelEvent>(ScrollSettings, TrickleDown.TrickleDown);
            volume = root.Q<Slider>("volume");
            sensitivity = root.Q<Slider>("sensitivity");
            quality = root.Q<DropdownField>("quality");
            invert = root.Q<Toggle>("invert");
            fullscreen = root.Q<Toggle>("fullscreen");
            if (Application.isEditor)
                fullscreen.tooltip = "Saved preference; fullscreen applies in a player build.";
            volumeValue = root.Q<Label>("volume-value");
            sensitivityValue = root.Q<Label>("sensitivity-value");
            status = root.Q<Label>("settings-status");

            quality.choices = new List<string>(GameSettings.QualityNames);
            sensitivity.lowValue = GameSettings.MinMouseSensitivity;
            sensitivity.highValue = GameSettings.MaxMouseSensitivity;
            launcher.clicked += Open;
            root.Q<Button>("resume").clicked += Close;
            closeButton.clicked += Close;
            root.Q<Button>("defaults").clicked += RestoreDefaults;
            volume.RegisterValueChangedCallback(evt => GameSettings.MasterVolume = evt.newValue);
            sensitivity.RegisterValueChangedCallback(evt => GameSettings.MouseSensitivity = evt.newValue);
            quality.RegisterValueChangedCallback(evt => GameSettings.QualityLevel = quality.index);
            invert.RegisterValueChangedCallback(evt => GameSettings.InvertLook = evt.newValue);
            fullscreen.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetFullscreen(evt.newValue, true);
                string saveMessage = IsMainMenuContext ? "Saved when you return." : "Saved on resume.";
                status.text = Application.platform == RuntimePlatform.WebGLPlayer
                    ? saveMessage + " Fullscreen depends on your browser."
                    : saveMessage;
            });
            root.RegisterCallback<GeometryChangedEvent>(evt =>
                shell.EnableInClassList("compact", evt.newRect.width < 900));
            RefreshValues();
            overlay.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshPresentation();
            lastFullscreen = Screen.fullScreen;
        }

        // The same persistent document serves both the title screen and gameplay.
        public void SetMainMenuContext(bool value)
        {
            if (IsMainMenuContext == value) return;
            Close();
            IsMainMenuContext = value;
            controlsOnly = false;
            if (boundRoot != null) RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            shell.EnableInClassList("controls-only", controlsOnly);
            boundRoot.Q<Label>("settings-title").text = controlsOnly ? "Controls" : "Settings";
            boundRoot.Q("settings-fields").style.display = controlsOnly ? DisplayStyle.None : DisplayStyle.Flex;
            boundRoot.Q<Button>("defaults").style.display = controlsOnly ? DisplayStyle.None : DisplayStyle.Flex;
            boundRoot.Q<Button>("resume").text = IsMainMenuContext ? "Back to main menu" : "Resume";
            closeButton.tooltip = IsMainMenuContext ? "Save and return to main menu" : "Save and resume";
            launcher.style.display = IsOpen || IsMainMenuContext ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ScrollSettings(WheelEvent evt)
        {
            if (Mathf.Abs(evt.delta.y) < 0.001f) return;
            // Wheel and trackpad deltas vary by platform; cap a single event to one small step.
            if (lastScrollFrame != Time.frameCount)
            {
                lastScrollFrame = Time.frameCount;
                scrollUsedThisFrame = 0f;
            }
            float remaining = Mathf.Max(0f, 8f - scrollUsedThisFrame);
            float step = Mathf.Clamp(evt.delta.y, -remaining, remaining);
            scrollUsedThisFrame += Mathf.Abs(step);
            settingsScroll.scrollOffset = new Vector2(0f, Mathf.Clamp(
                settingsScroll.scrollOffset.y + step, 0f, settingsScroll.verticalScroller.highValue));
            evt.StopImmediatePropagation();
        }

        private void EnsureInterface()
        {
            // UIDocument rebuilds its tree when this GameObject is reactivated.
            if (document != null && document.rootVisualElement != null &&
                document.rootVisualElement != boundRoot)
                BindInterface();
        }

        private void Update()
        {
            EnsureInterface();
            if (shell != null) shell.style.display = StartupInputBlocked ? DisplayStyle.None : DisplayStyle.Flex;
            if (StartupInputBlocked) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && (IsOpen || !IsMainMenuContext) &&
                (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
            {
                Toggle();
            }

            // Browsers can leave fullscreen independently via Escape. Reflect the actual display state.
            if (IsOpen && lastFullscreen != Screen.fullScreen)
            {
                fullscreen.SetValueWithoutNotify(Screen.fullScreen);
                lastFullscreen = Screen.fullScreen;
            }
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open() => Open(false);

        public void OpenControls() => Open(true);

        private void Open(bool showControlsOnly)
        {
            EnsureInterface();
            if (StartupInputBlocked || IsOpen || !isActiveAndEnabled || overlay == null) return;

            controlsOnly = showControlsOnly;

            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            IsOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            launcher.style.display = DisplayStyle.None;
            overlay.style.display = DisplayStyle.Flex;
            status.text = IsMainMenuContext ? "Changes are saved when you return." : "Saved on resume.";
            RefreshPresentation();
            RefreshValues();
            settingsScroll.scrollOffset = Vector2.zero;
            closeButton.schedule.Execute(() => { if (IsOpen) closeButton.Focus(); });
            OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            lastClosedFrame = Time.frameCount;
            GameSettings.Save();
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            overlay.style.display = DisplayStyle.None;
            RefreshPresentation();
            // Moving focus also dismisses any quality popup before returning to gameplay.
            if (!IsMainMenuContext) launcher.Focus();
            OpenChanged?.Invoke(false);
        }

        private void RefreshValues()
        {
            if (volume == null) return;
            volume.SetValueWithoutNotify(GameSettings.MasterVolume);
            sensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            quality.SetValueWithoutNotify(quality.choices[GameSettings.QualityLevel]);
            invert.SetValueWithoutNotify(GameSettings.InvertLook);
            fullscreen.SetValueWithoutNotify(Application.isEditor ? GameSettings.Fullscreen : Screen.fullScreen);
            volumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";
            sensitivityValue.text = $"{GameSettings.MouseSensitivity:0.0}x";
            lastFullscreen = Screen.fullScreen;
        }

        private void RestoreDefaults()
        {
            GameSettings.ResetDefaults();
            GameSettings.Save();
            RefreshValues();
            status.text = "Defaults restored.";
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameSettings.Save();
        }

        private void OnApplicationQuit() => GameSettings.Save();

        private void OnEnable()
        {
            if (Instance == this && shell != null) shell.style.display = DisplayStyle.Flex;
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            Close();
            if (shell != null) shell.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Close();
            GameSettings.Changed -= RefreshValues;
            Instance = null;
            if (panelSettings != null) Destroy(panelSettings);
        }
    }
}
