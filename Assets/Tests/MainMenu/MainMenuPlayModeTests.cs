using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;

namespace Parallax.MainMenu.Tests
{
    public class MainMenuPlayModeTests
    {
        private MonoBehaviour controller;
        private UIDocument document;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private float volume;
        private static readonly BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            volume = GameSettings.MasterVolume;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            controller = GameObject.Find("Main Menu").GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().Name == "MainMenuController");
            document = controller.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSettings.MasterVolume = volume; GameSettings.Save();
            if (GameSettingsMenu.Instance != null) GameSettingsMenu.Instance.Close();
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            // Remove scene controllers before their persistent UI dependencies.
            if (controller != null) Object.Destroy(controller.gameObject);
            yield return null;
            if (GameSettingsMenu.Instance != null) Object.Destroy(GameSettingsMenu.Instance.gameObject);
            Time.timeScale = 1f; AudioListener.pause = false;
            yield return null;
        }

        private Button Button(string name) => document.rootVisualElement.Q<Button>(name);
        private static void Submit(Button button)
        {
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }

        [UnityTest]
        public IEnumerator LaunchSceneHasFourActionsAndBlocksGameplayInput()
        {
            Assert.That(Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(0)), Is.EqualTo("MainMenu"));
            Assert.That(Application.CanStreamedLevelBeLoaded("StartScene"), Is.True);
            Assert.That(GameObject.Find("Player"), Is.Null);
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.True);
            Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            foreach (string action in new[] { "play", "settings", "controls", "quit" })
                Assert.That(Button(action).enabledInHierarchy, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.P));
            yield return null; yield return null;
            Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False, "Pause shortcuts must not open a second menu from the title.");
        }

        [UnityTest]
        public IEnumerator KeyboardNavigatesAndMouseReturnsFromSettings()
        {
            Button("play").Focus();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(document.rootVisualElement.focusController.focusedElement, Is.SameAs(Button("settings")));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(GameSettingsMenu.Instance.IsOpen, Is.True);
            var root = GameSettingsMenu.Instance.GetComponent<UIDocument>().rootVisualElement;
            Vector2 center = root.Q<Button>("resume").worldBound.center;
            Vector2 screen = new Vector2(center.x * Screen.width / root.worldBound.width,
                Screen.height - center.y * Screen.height / root.worldBound.height);
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null; yield return null;
            Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False);
            Assert.That(Cursor.visible, Is.True);
        }

        [UnityTest]
        public IEnumerator SettingsAndControlsShareThePersistentDocumentAndReturnFocus()
        {
            var settings = GameSettingsMenu.Instance;
            Submit(Button("settings")); yield return null;
            var root = settings.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(root.Q<Button>("resume").text, Is.EqualTo("Back to main menu"));
            Assert.That(document.rootVisualElement.Q("menu-home").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            root.Q<Slider>("volume").value = 0.42f;
            Submit(root.Q<Button>("resume")); yield return null; yield return null;
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(PlayerPrefs.GetFloat("Parallax.Settings.MasterVolume"), Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(document.rootVisualElement.focusController.focusedElement, Is.SameAs(Button("settings")));
            Submit(Button("controls")); yield return null;
            Assert.That(GameSettingsMenu.Instance, Is.SameAs(settings));
            Assert.That(root.Q<Label>("settings-title").text, Is.EqualTo("Controls"));
            Assert.That(root.Q("settings-fields").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Query<VisualElement>(className: "control-row").ToList().Count, Is.EqualTo(9));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(Cursor.visible, Is.True);
            Assert.That(document.rootVisualElement.focusController.focusedElement, Is.SameAs(Button("controls")));
        }

        [UnityTest]
        public IEnumerator PlayResetsDirtyStateKeepsPreferencesAndPreservesPauseMenu()
        {
            var stateType = controller.GetType().Assembly.GetType("GlobalState");
            var oldState = stateType.GetMethod("get").Invoke(null, null);
            stateType.GetField("gravity").SetValue(oldState, 0f);
            var timeField = stateType.GetField("now");
            timeField.SetValue(oldState, Enum.Parse(timeField.FieldType, "Past"));
            stateType.GetField("lastSwap", Fields).SetValue(oldState, 10000f);
            stateType.GetField("actuallySwapped", Fields).SetValue(oldState, false);
            GameSettings.MasterVolume = 0.37f;
            var persistent = GameSettingsMenu.Instance;
            Time.timeScale = 0f; AudioListener.pause = true;
            Submit(Button("play"));
            float deadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().name != "StartScene" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartScene"));
            var state = stateType.GetMethod("get").Invoke(null, null);
            Assert.That(state, Is.Not.SameAs(oldState));
            Assert.That(timeField.GetValue(state).ToString(), Is.EqualTo("Future"));
            Assert.That(stateType.GetField("gravity").GetValue(state), Is.EqualTo(14.715f));
            Assert.That(stateType.GetField("lastSwap", Fields).GetValue(state), Is.EqualTo(-999f));
            Assert.That(GameSettingsMenu.Instance, Is.SameAs(persistent));
            Assert.That(Object.FindObjectsByType<GameSettingsMenu>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var intro = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(component => component.GetType().Name == "ContextIntro");
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.True, "Story must lock controls before the tutorial begins.");
            intro.GetType().GetMethod("Skip").Invoke(intro, null);
            yield return new WaitForSecondsRealtime(2.1f);
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(GameObject.Find("Player"), Is.Not.Null);
            stateType.GetMethod("SwapTime").Invoke(state, null);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(timeField.GetValue(state).ToString(), Is.EqualTo("Past"), "Old cooldown must not block a new game.");
            persistent.Open(); yield return null;
            var ui = persistent.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(ui.Q<Button>("resume").text, Is.EqualTo("Resume"));
            Assert.That(ui.Q("settings-fields").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Time.timeScale, Is.Zero);
            persistent.Close(); yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator MissingGameplaySceneLeavesAUsableMenu()
        {
            controller.GetType().GetField("gameplayScene", Fields).SetValue(controller, "MissingGameplayScene");
            Submit(Button("play")); yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(Button("play").enabledInHierarchy, Is.True);
            Assert.That(document.rootVisualElement.Q<Label>("menu-status").text, Does.Contain("unavailable"));
            Assert.That(Cursor.visible, Is.True);
        }

        [UnityTest]
        public IEnumerator QuitInEditorExplainsHowToLeaveWithoutClosingTheEditor()
        {
            if (!Application.isEditor) Assert.Ignore("Editor-only quit policy; standalone Quit exits the process.");
            Submit(Button("quit")); yield return null;
            Assert.That(document.rootVisualElement.Q<Label>("menu-status").text, Does.Contain("Stop"));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator MenusFitCommonAspectRatiosAndControlsScrollToTheEnd()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(1024, 768), new Vector2Int(2560, 1080), new Vector2Int(640, 360), new Vector2Int(480, 800) })
            {
                yield return RenderAndCheck(document, size, "main", false);
                Submit(Button("controls")); yield return null;
                yield return RenderAndCheck(GameSettingsMenu.Instance.GetComponent<UIDocument>(), size, "controls", true);
                GameSettingsMenu.Instance.Close(); yield return null;
            }
        }

        private IEnumerator RenderAndCheck(UIDocument ui, Vector2Int size, string label, bool controls)
        {
            var panel = ui.panelSettings;
            var previous = panel.targetTexture;
            var texture = new RenderTexture(size.x, size.y, 24);
            texture.Create(); panel.targetTexture = texture;
            try
            {
                for (int frame = 0; frame < 8; frame++) yield return null;
                var root = ui.rootVisualElement;
                var action = root.Q<Button>(controls ? "resume" : "play");
                Assert.That(action.worldBound.xMin, Is.GreaterThanOrEqualTo(-1), label + size);
                Assert.That(action.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1), label + size);
                Assert.That(action.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1), label + size);
                if (controls)
                {
                    var scroll = root.Q<ScrollView>("settings-scroll");
                    scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
                    yield return null;
                    var hint = root.Q(className: "controls-hint");
                    Assert.That(hint.worldBound.yMax, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.yMax + 2), "Last controls must be reachable.");
                }
                else
                {
                    var title = root.Q("game-title");
                    Assert.That(title.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1));
                    var scroll = root.Q<ScrollView>("home-scroll");
                    scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
                    yield return null;
                    Assert.That(root.Q<Button>("quit").worldBound.yMax,
                        Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.yMax + 2));
                    scroll.scrollOffset = Vector2.zero; yield return null;
                }
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-mainMenuArtifacts");
                if (index >= 0 && index + 1 < args.Length)
                {
                    Directory.CreateDirectory(args[index + 1]);
                    var oldActive = RenderTexture.active;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = texture;
                        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(args[index + 1], label + "-" + size.x + "x" + size.y + ".png"), image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = oldActive; Object.Destroy(image); }
                }
            }
            finally { panel.targetTexture = previous; texture.Release(); Object.Destroy(texture); }
            yield return null;
        }
    }
}
