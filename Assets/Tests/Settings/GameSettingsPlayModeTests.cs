using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Parallax.Settings.Tests
{
    public class GameSettingsPlayModeTests
    {
        private const string VolumeKey = "Parallax.Settings.MasterVolume";
        private const string QualityKey = "Parallax.Settings.QualityLevel";
        private const string SensitivityKey = "Parallax.Settings.MouseSensitivity";
        private const string InvertKey = "Parallax.Settings.InvertLook";
        private const string FullscreenKey = "Parallax.Settings.Fullscreen";

        private PreferenceBackup[] preferences;
        private float originalTimeScale;
        private float originalAudioVolume;
        private bool originalAudioPause;
        private bool originalCursorVisible;
        private CursorLockMode originalCursorLock;
        private int originalQuality;
        private GameSettingsMenu menu;
        private VisualElement root;
        private Keyboard testKeyboard;
        private InputSettings.BackgroundBehavior originalBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            preferences = new[]
            {
                new PreferenceBackup(VolumeKey, true),
                new PreferenceBackup(QualityKey, false),
                new PreferenceBackup(SensitivityKey, true),
                new PreferenceBackup(InvertKey, false),
                new PreferenceBackup(FullscreenKey, false)
            };
            originalTimeScale = Time.timeScale;
            originalAudioVolume = AudioListener.volume;
            originalAudioPause = AudioListener.pause;
            originalCursorVisible = Cursor.visible;
            originalCursorLock = Cursor.lockState;
            originalQuality = QualitySettings.GetQualityLevel();
            originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;

            if (GameSettingsMenu.Instance != null)
            {
                UnityEngine.Object.Destroy(GameSettingsMenu.Instance.gameObject);
                yield return null;
            }

            PlayerPrefs.SetFloat(VolumeKey, 1f);
            PlayerPrefs.SetInt(QualityKey, originalQuality);
            PlayerPrefs.SetFloat(SensitivityKey, 1f);
            PlayerPrefs.SetInt(InvertKey, 0);
            PlayerPrefs.SetInt(FullscreenKey, 1);
            GameSettings.Reload();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            yield return SceneManager.LoadSceneAsync("StartScene", LoadSceneMode.Single);
            yield return null;

            menu = GameSettingsMenu.Instance;
            Assert.That(menu, Is.Not.Null, "StartScene must contain a settings menu.");
            UIDocument document = menu.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null, "The scene menu must display its UI document.");
            root = document.rootVisualElement;
            Assert.That(root, Is.Not.Null);
            menu.Close();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (testKeyboard != null)
            {
                InputSystem.RemoveDevice(testKeyboard);
                testKeyboard = null;
            }
            InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;

            if (GameSettingsMenu.Instance != null)
            {
                UnityEngine.Object.Destroy(GameSettingsMenu.Instance.gameObject);
                yield return null;
            }

            GameSettings.Save();
            foreach (PreferenceBackup preference in preferences)
                preference.Restore();
            PlayerPrefs.Save();
            GameSettings.Reload();
            QualitySettings.SetQualityLevel(originalQuality, true);
            Time.timeScale = originalTimeScale;
            AudioListener.volume = originalAudioVolume;
            AudioListener.pause = originalAudioPause;
            Cursor.lockState = originalCursorLock;
            Cursor.visible = originalCursorVisible;
        }

        [UnityTest]
        public IEnumerator SceneButtonsOpenAndResumeTheGame()
        {
            Assert.That(menu.IsOpen, Is.False);
            Submit(Field<Button>("open-settings"));
            yield return null;

            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);

            Submit(Field<Button>("resume"));
            yield return null;
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
        }

        [Test]
        public void EditingControlsAppliesAndPersistsThePlayersChoices()
        {
            menu.Open();
            Field<Slider>("volume").value = 0.32f;
            Field<Slider>("sensitivity").value = 2.1f;
            Field<Toggle>("invert").value = true;
            bool selectedFullscreen = !Field<Toggle>("fullscreen").value;
            Field<Toggle>("fullscreen").value = selectedFullscreen;
            int selectedQuality = GameSettings.QualityNames.Length - 1;
            Field<DropdownField>("quality").index = selectedQuality;

            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(AudioListener.volume, Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(2.1f).Within(0.001f));
            Assert.That(GameSettings.InvertLook, Is.True);
            Assert.That(GameSettings.Fullscreen, Is.EqualTo(selectedFullscreen));
            Assert.That(GameSettings.QualityLevel, Is.EqualTo(selectedQuality));
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(selectedQuality));

            menu.Close();
            Assert.That(PlayerPrefs.GetFloat(VolumeKey), Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(PlayerPrefs.GetFloat(SensitivityKey), Is.EqualTo(2.1f).Within(0.001f));
            Assert.That(PlayerPrefs.GetInt(InvertKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(FullscreenKey), Is.EqualTo(selectedFullscreen ? 1 : 0));
            Assert.That(PlayerPrefs.GetInt(QualityKey), Is.EqualTo(selectedQuality));

            AudioListener.volume = 0f;
            GameSettings.Reload();
            menu.Open();
            Assert.That(AudioListener.volume, Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(Field<Slider>("volume").value, Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(Field<Slider>("sensitivity").value, Is.EqualTo(2.1f).Within(0.001f));
            Assert.That(Field<Toggle>("invert").value, Is.True);
            Assert.That(Field<Toggle>("fullscreen").value,
                Is.EqualTo(Application.isEditor ? GameSettings.Fullscreen : Screen.fullScreen));
            Assert.That(Field<DropdownField>("quality").index, Is.EqualTo(selectedQuality));
        }

        [Test]
        public void DefaultsButtonRestoresSettingsAndRefreshesTheControls()
        {
            menu.Open();
            int defaultQuality = GameSettings.QualityLevel;
            Field<Slider>("volume").value = 0.2f;
            Field<Slider>("sensitivity").value = 2.7f;
            Field<Toggle>("invert").value = true;
            Field<Toggle>("fullscreen").value = !Field<Toggle>("fullscreen").value;
            Field<DropdownField>("quality").index = (defaultQuality + 1) % GameSettings.QualityNames.Length;

            Submit(Field<Button>("defaults"));

            Assert.That(menu.IsOpen, Is.True, "Resetting preferences must keep the menu usable.");
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(GameSettings.DefaultMasterVolume));
            Assert.That(AudioListener.volume, Is.EqualTo(GameSettings.DefaultMasterVolume));
            Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(GameSettings.DefaultMouseSensitivity));
            Assert.That(GameSettings.InvertLook, Is.False);
            Assert.That(GameSettings.Fullscreen, Is.True);
            Assert.That(GameSettings.QualityLevel, Is.EqualTo(defaultQuality));
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(defaultQuality));
            Assert.That(Field<Slider>("volume").value, Is.EqualTo(GameSettings.DefaultMasterVolume));
            Assert.That(Field<Slider>("sensitivity").value, Is.EqualTo(GameSettings.DefaultMouseSensitivity));
            Assert.That(Field<Toggle>("invert").value, Is.False);
            Assert.That(Field<Toggle>("fullscreen").value,
                Is.EqualTo(Application.isEditor ? GameSettings.Fullscreen : Screen.fullScreen));
            GameSettings.Reload();
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(GameSettings.DefaultMasterVolume));
            Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(GameSettings.DefaultMouseSensitivity));
        }

        [Test]
        public void ReloadRecoversFromCorruptOrOutOfRangeSavedPreferences()
        {
            PlayerPrefs.SetFloat(VolumeKey, float.NaN);
            PlayerPrefs.SetFloat(SensitivityKey, float.PositiveInfinity);
            PlayerPrefs.SetInt(QualityKey, int.MaxValue);
            PlayerPrefs.SetInt(InvertKey, -9);
            PlayerPrefs.SetInt(FullscreenKey, 8);

            GameSettings.Reload();
            menu.Open();

            Assert.That(GameSettings.MasterVolume, Is.EqualTo(GameSettings.DefaultMasterVolume));
            Assert.That(AudioListener.volume, Is.EqualTo(GameSettings.DefaultMasterVolume));
            Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(GameSettings.DefaultMouseSensitivity));
            Assert.That(GameSettings.QualityLevel, Is.EqualTo(GameSettings.QualityNames.Length - 1));
            Assert.That(GameSettings.InvertLook, Is.False);
            Assert.That(GameSettings.Fullscreen, Is.True);
            Assert.That(float.IsNaN(Field<Slider>("volume").value), Is.False);
            Assert.That(float.IsInfinity(Field<Slider>("sensitivity").value), Is.False);

            PlayerPrefs.SetFloat(VolumeKey, -50f);
            PlayerPrefs.SetFloat(SensitivityKey, 50f);
            PlayerPrefs.SetInt(QualityKey, int.MinValue);
            GameSettings.Reload();

            Assert.That(GameSettings.MasterVolume, Is.Zero);
            Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(GameSettings.MaxMouseSensitivity));
            Assert.That(GameSettings.QualityLevel, Is.Zero);
            Assert.That(Field<Slider>("volume").value, Is.Zero);
            Assert.That(Field<Slider>("sensitivity").value, Is.EqualTo(GameSettings.MaxMouseSensitivity));
        }

        [Test]
        public void SettingsApiSanitizesInvalidValuesAndPublishesChanges()
        {
            int notifications = 0;
            Action changed = () => notifications++;
            GameSettings.Changed += changed;
            try
            {
                GameSettings.MasterVolume = -1f;
                Assert.That(GameSettings.MasterVolume, Is.Zero);
                Assert.That(AudioListener.volume, Is.Zero);
                GameSettings.MasterVolume = 10f;
                Assert.That(GameSettings.MasterVolume, Is.EqualTo(1f));
                GameSettings.MasterVolume = float.NaN;
                Assert.That(GameSettings.MasterVolume, Is.EqualTo(GameSettings.DefaultMasterVolume));
                GameSettings.MouseSensitivity = -20f;
                Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(GameSettings.MinMouseSensitivity));
                GameSettings.MouseSensitivity = 20f;
                Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(GameSettings.MaxMouseSensitivity));
                GameSettings.MouseSensitivity = float.NegativeInfinity;
                Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(GameSettings.DefaultMouseSensitivity));
                GameSettings.QualityLevel = int.MinValue;
                Assert.That(GameSettings.QualityLevel, Is.Zero);
                GameSettings.QualityLevel = int.MaxValue;
                Assert.That(GameSettings.QualityLevel, Is.EqualTo(GameSettings.QualityNames.Length - 1));
                Assert.That(notifications, Is.GreaterThanOrEqualTo(5));
                Assert.That(PlayerPrefs.GetFloat(VolumeKey), Is.EqualTo(GameSettings.DefaultMasterVolume));
                Assert.That(PlayerPrefs.GetFloat(SensitivityKey), Is.EqualTo(GameSettings.DefaultMouseSensitivity));
            }
            finally
            {
                GameSettings.Changed -= changed;
            }
        }

        [Test]
        public void RepeatedOpenAndCloseRestoresThePreviouslyPausedGameState()
        {
            Time.timeScale = 0.37f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            CursorLockMode priorLock = Cursor.lockState;
            bool priorVisibility = Cursor.visible;

            menu.Open();
            menu.Open();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            menu.Close();
            menu.Close();

            Assert.That(Time.timeScale, Is.EqualTo(0.37f));
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(priorLock));
            Assert.That(Cursor.visible, Is.EqualTo(priorVisibility));
        }

        [UnityTest]
        public IEnumerator DisablingAnOpenMenuRestoresGameplay()
        {
            Time.timeScale = 0.6f;
            AudioListener.pause = false;
            menu.Open();
            menu.enabled = false;
            yield return null;

            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.6f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(root.Q("settings-root").style.display.value, Is.EqualTo(DisplayStyle.None));
            menu.enabled = true;
            yield return null;
            Assert.That(root.Q("settings-root").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            menu.Open();
            Assert.That(menu.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator ReloadingTheSceneKeepsOneMenuAndTheEditedSettings()
        {
            Time.timeScale = 0.42f;
            AudioListener.pause = false;
            menu.Open();
            Field<Slider>("volume").value = 0.46f;
            GameSettingsMenu persistentMenu = menu;

            yield return SceneManager.LoadSceneAsync("StartScene", LoadSceneMode.Single);
            yield return null;

            Assert.That(GameSettingsMenu.Instance, Is.SameAs(persistentMenu));
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSettingsMenu>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1), "Loading another scene must not duplicate the persistent menu.");
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.46f).Within(0.001f));
            Assert.That(PlayerPrefs.GetFloat(VolumeKey), Is.EqualTo(0.46f).Within(0.001f));
            Assert.That(Field<Slider>("volume").value, Is.EqualTo(0.46f).Within(0.001f));

            Submit(Field<Button>("resume"));
            yield return null;
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.42f));
            Assert.That(AudioListener.pause, Is.False);
        }

        [UnityTest]
        public IEnumerator ReactivatingTheMenuGameObjectKeepsTheVisibleControlsWorking()
        {
            Time.timeScale = 0.72f;
            AudioListener.pause = false;
            menu.Open();
            Field<Slider>("volume").value = 0.28f;
            menu.gameObject.SetActive(false);
            yield return null;

            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.72f));
            Assert.That(AudioListener.pause, Is.False);

            menu.gameObject.SetActive(true);
            yield return null;
            // UIDocument can rebuild its tree when the GameObject is reactivated.
            // Exercise the displayed tree rather than retaining the old test references.
            root = menu.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root, Is.Not.Null);
            Assert.That(GameSettingsMenu.Instance, Is.SameAs(menu));
            Submit(Field<Button>("open-settings"));
            yield return null;

            Assert.That(menu.IsOpen, Is.True);
            Assert.That(root.Q("settings-overlay").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Field<Slider>("volume").value, Is.EqualTo(0.28f).Within(0.001f));
            Field<Slider>("volume").value = 0.61f;
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.61f).Within(0.001f));
            Assert.That(AudioListener.volume, Is.EqualTo(0.61f).Within(0.001f));

            Submit(Field<Button>("resume"));
            yield return null;
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.72f));
            Assert.That(AudioListener.pause, Is.False);
        }

        [UnityTest]
        public IEnumerator DestroyingAnOpenMenuRestoresGameplay()
        {
            Time.timeScale = 0.8f;
            AudioListener.pause = false;
            menu.Open();
            UnityEngine.Object.Destroy(menu.gameObject);
            yield return null;

            Assert.That(GameSettingsMenu.Instance, Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            Assert.That(AudioListener.pause, Is.False);
        }

        [UnityTest]
        public IEnumerator EscapeOpensAndClosesTheMenuWhileTheGameIsPaused()
        {
            // Batch mode has no focused Game View. Route the synthetic keyboard into
            // the player's normal update loop, as the Input System's integration tests do.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            testKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            Assert.That(testKeyboard.escapeKey.isPressed, Is.True, "The queued key must reach the player.");
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
        }

        [UnityTest]
        public IEnumerator WheelAndTrackpadScrollingStaySmallAndControlsRemainReachable()
        {
            menu.Open();
            var document = menu.GetComponent<UIDocument>();
            var target = new RenderTexture(960, 540, 24); target.Create();
            document.panelSettings.targetTexture = target;
            for (int i = 0; i < 10; i++) yield return null;
            var scroll = Field<ScrollView>("settings-scroll");
            Assert.That(root.Q("controls-list"), Is.Not.Null);
            Assert.That(root.Query<VisualElement>(className: "control-row").ToList().Count, Is.EqualTo(10));
            foreach (float delta in new[] {3f, 120f, 0.25f})
            {
                scroll.scrollOffset = Vector2.zero;
                yield return null;
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = new Vector2(0f, delta) }))
                {
                    wheel.target = scroll.contentContainer;
                    scroll.contentContainer.SendEvent(wheel);
                }
                yield return null;
                Assert.That(scroll.scrollOffset.y, Is.GreaterThan(0f));
                Assert.That(scroll.scrollOffset.y, Is.LessThanOrEqualTo(8.1f), "One event must not jump a page.");
            }
            scroll.scrollOffset = Vector2.zero;
            yield return null;
            for (int burst = 0; burst < 10; burst++)
            {
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = new Vector2(0f, 120f) }))
                {
                    wheel.target = scroll.contentContainer;
                    scroll.contentContainer.SendEvent(wheel);
                }
            }
            Assert.That(scroll.scrollOffset.y, Is.LessThanOrEqualTo(8.1f), "Trackpad bursts share one frame budget.");
            yield return null;
            for (int tick = 0; tick < 200; tick++)
            {
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = new Vector2(0f, 3f) }))
                {
                    wheel.target = scroll.contentContainer;
                    scroll.contentContainer.SendEvent(wheel);
                }
                yield return null;
            }
            yield return null;
            Assert.That(scroll.scrollOffset.y, Is.EqualTo(scroll.verticalScroller.highValue).Within(0.1f));
            float bottom = scroll.scrollOffset.y;
            using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = new Vector2(0f, -120f) }))
            {
                wheel.target = scroll.contentContainer;
                scroll.contentContainer.SendEvent(wheel);
            }
            yield return null;
            Assert.That(bottom - scroll.scrollOffset.y, Is.InRange(0.1f, 8.1f));
            Assert.That(Field<Button>("resume").worldBound.yMax,
                Is.LessThanOrEqualTo(root.Q(className: "settings-panel").worldBound.yMax + 1f));
            var panelBounds = root.Q(className: "settings-panel").worldBound;
            var scrollbarBounds = scroll.verticalScroller.worldBound;
            Assert.That(scrollbarBounds.xMin, Is.GreaterThan(panelBounds.xMin));
            Assert.That(scrollbarBounds.xMax, Is.LessThan(panelBounds.xMax - 2f), "Scrollbar stays inside the border.");
            Assert.That(scrollbarBounds.yMin, Is.GreaterThan(panelBounds.yMin));
            Assert.That(scrollbarBounds.yMax, Is.LessThan(panelBounds.yMax));
            document.panelSettings.targetTexture = null; UnityEngine.Object.Destroy(target);
        }

        private T Field<T>(string name) where T : VisualElement
        {
            T field = root.Q<T>(name);
            Assert.That(field, Is.Not.Null, $"The settings document needs the '{name}' control.");
            return field;
        }

        private static void Submit(Button button)
        {
            Assert.That(button.panel, Is.Not.Null, "Button events require the real runtime UI panel.");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                // SendEvent forwards to the panel, which otherwise routes navigation
                // to its focused element rather than to this button.
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private readonly struct PreferenceBackup
        {
            private readonly string key;
            private readonly bool isFloat;
            private readonly bool existed;
            private readonly float floatValue;
            private readonly int intValue;

            public PreferenceBackup(string key, bool isFloat)
            {
                this.key = key;
                this.isFloat = isFloat;
                existed = PlayerPrefs.HasKey(key);
                floatValue = isFloat && existed ? PlayerPrefs.GetFloat(key) : 0f;
                intValue = !isFloat && existed ? PlayerPrefs.GetInt(key) : 0;
            }

            public void Restore()
            {
                if (!existed)
                    PlayerPrefs.DeleteKey(key);
                else if (isFloat)
                    PlayerPrefs.SetFloat(key, floatValue);
                else
                    PlayerPrefs.SetInt(key, intValue);
            }
        }
    }
}
