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
        // Ignore the click/key that resumes the game until the next frame.
        public static bool GameplayInputBlocked => Instance != null &&
            (Instance.IsOpen || Instance.lastClosedFrame == Time.frameCount);
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
            settingsScroll.mouseWheelScrollSize = 12f;
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
                status.text = Application.platform == RuntimePlatform.WebGLPlayer
                    ? "Saved on resume. Fullscreen depends on your browser."
                    : "Saved on resume.";
            });
            root.RegisterCallback<GeometryChangedEvent>(evt =>
                shell.EnableInClassList("compact", evt.newRect.width < 900));
            RefreshValues();
            overlay.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            launcher.style.display = IsOpen ? DisplayStyle.None : DisplayStyle.Flex;
            lastFullscreen = Screen.fullScreen;
        }

        private void ScrollSettings(WheelEvent evt)
        {
            if (Mathf.Abs(evt.delta.y) < 0.001f) return;
            // Wheel and trackpad deltas vary by platform; cap a single event to one small step.
            float step = Mathf.Clamp(evt.delta.y * 4f, -36f, 36f);
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
            var keyboard = Keyboard.current;
            if (keyboard != null &&
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

        public void Open()
        {
            EnsureInterface();
            if (IsOpen || !isActiveAndEnabled || overlay == null) return;

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
            status.text = "Saved on resume.";
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
            launcher.style.display = DisplayStyle.Flex;
            // Moving focus also dismisses any quality popup before returning to gameplay.
            launcher.Focus();
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
