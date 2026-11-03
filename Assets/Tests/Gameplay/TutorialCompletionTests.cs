using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class TutorialCompletionTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        MonoBehaviour level, completion;
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        static bool Flag(object obj, string name) => (bool)obj.GetType().GetProperty(name).GetValue(obj);
        static void Call(object obj, string name) => obj.GetType().GetMethod(name).Invoke(obj, null);
        [UnitySetUp] public IEnumerator Setup()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false;
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            var root = GameObject.Find("Level 1 - Engineering Access");
            level = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Level1Controller");
            completion = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "TutorialCompletionScreen");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (completion != null) completion.enabled = false;
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false; yield return null;
        }
        IEnumerator Complete()
        {
            Set(level, "<IntroSolved>k__BackingField", true); Set(level, "<MainSolved>k__BackingField", true);
            Call(level, "TryComplete"); yield return null;
            Assert.That(Flag(completion, "IsOpen"), Is.True);
            Assert.That(Time.timeScale, Is.Zero); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.True);
            Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(GameObject.Find("Tutorial Completion Screen"), Is.Null, "Wait before revealing.");
            yield return new WaitForSecondsRealtime(2.7f);
            Assert.That(Flag(completion, "IsReady"), Is.True);
        }
        [UnityTest] public IEnumerator RequiresFinalObjectiveAndShowsExactCopyWithoutDuplicateOrSettings()
        {
            Call(level, "TryComplete"); yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(Flag(completion, "IsOpen"), Is.False);
            Set(level, "<IntroSolved>k__BackingField", true); Call(level, "TryComplete"); yield return null;
            Assert.That(Flag(completion, "IsOpen"), Is.False);
            yield return Complete();
            var screen = GameObject.Find("Tutorial Completion Screen");
            var text = screen.GetComponentsInChildren<TMP_Text>();
            CollectionAssert.AreEquivalent(new[] { "TUTORIAL COMPLETE", "You've learned the basics of time manipulation and gravity control.", "The investigation begins now.", "REPLAY TUTORIAL", "CONTINUE" }, text.Select(x => x.text).ToArray());
            foreach (var label in text)
            {
                Assert.That(label.font.name, Is.EqualTo(label.name.Contains("description") || label.name.Contains("final message") ? "SpaceMono-Regular SDF" : "Oxanium-Regular SDF"));
                label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False, label.name);
            }
            GameSettingsMenu.Instance.Open(); Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False);
            Call(level, "TryComplete"); yield return null;
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(x => x.name == "Tutorial Completion Screen"), Is.EqualTo(1));
            foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) }) Capture(screen.GetComponent<Canvas>(), size);
        }
        [UnityTest] public IEnumerator ReplayReloadsInitialFutureStateWithoutIntro()
        {
            yield return Complete();
            Call(completion, "ReplayTutorial"); yield return null; yield return null;
            var root = GameObject.Find("Level 1 - Engineering Access");
            var fresh = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Level1Controller");
            foreach (var name in new[] { "IntroSolved", "MainSolved", "StorageAccessGranted", "Completed" }) Assert.That(Flag(fresh, name), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            var state = fresh.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            Assert.That(state.GetType().GetField("now").GetValue(state).ToString(), Is.EqualTo("Future"));
            Assert.That((float)state.GetType().GetField("gravity").GetValue(state), Is.EqualTo(14.715f));
            Assert.That(GameObject.Find("Context Intro"), Is.Null);
            Assert.That(Vector3.Distance(GameObject.Find("Player").transform.position, new Vector3(3, .02f, 9)), Is.LessThan(.2f));
            level = fresh; completion = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "TutorialCompletionScreen");
            yield return Complete(); // New scene supports a second completion cycle.
        }
        [UnityTest] public IEnumerator ShiftKShortcutHonoursInputLockAndDoesNotSolvePuzzles()
        {
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            try
            {
                GameSettingsMenu.StartupInputBlocked = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.K));
                yield return null; Assert.That(Flag(completion, "IsOpen"), Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
                GameSettingsMenu.StartupInputBlocked = false;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.K));
                yield return null; yield return null; Assert.That(Flag(completion, "IsOpen"), Is.True);
                Assert.That(Flag(level, "Completed"), Is.False); Assert.That(Flag(level, "IntroSolved"), Is.False);
                yield return new WaitForSecondsRealtime(2.7f); Assert.That(Flag(completion, "IsReady"), Is.True);
            }
            finally { InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput; }
        }
        static IEnumerator WaitForNarrativeHandoff()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (GameSettingsMenu.NarrativeInputBlocked && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; yield return null;
            Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.False);
        }
        [UnityTest] public IEnumerator DevelopmentSkipWorksAcrossTutorialAndAllPuzzleScenes()
        {
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            IEnumerator Press(params Key[] keys)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                yield return new WaitForSecondsRealtime(.06f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            }
            try
            {
                foreach (var scene in new[] { "StartScene", "Level1_PowerRestoration", "Level2_CargoBay", "Puzzle3_Placeholder" })
                {
                    yield return SceneManager.LoadSceneAsync(scene); yield return null; yield return null;
                    bool placeholder = scene == "Puzzle3_Placeholder";
                    completion = placeholder ? null : Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "TutorialCompletionScreen");
                    var target = placeholder ? Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "Puzzle3Placeholder") : completion;
                    bool Open() => placeholder ? GameSettingsMenu.NarrativeInputBlocked : Flag(target, "IsOpen");
                    Assert.That(Open(), Is.False);
                    GameSettingsMenu.NarrativeInputBlocked = true;
                    yield return Press(Key.LeftShift, Key.K);
                    if (!placeholder) Assert.That(Open(), Is.False);
                    else Assert.That(GameObject.Find("Memory Recovery Narrative"), Is.Null);
                    GameSettingsMenu.NarrativeInputBlocked = false;
                    GameSettingsMenu.Instance.Open();
                    yield return Press(Key.LeftShift, Key.K); Assert.That(Open(), Is.False);
                    GameSettingsMenu.Instance.Close(); yield return null; yield return null;
                    yield return Press(Key.K); Assert.That(Open(), Is.False);
                    yield return Press(Key.LeftShift, Key.K); Assert.That(Open(), Is.True);
                    if (!placeholder)
                    {
                        if (scene == "Level1_PowerRestoration")
                            Assert.That(Flag(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerReactor"), "IsOnline"), Is.False);
                        if (scene == "Level2_CargoBay")
                            Assert.That(Flag(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "CargoBayController"), "Completed"), Is.False);
                        yield return new WaitForSecondsRealtime(2.7f);
                        yield return Press(Key.LeftShift, Key.K);
                        Assert.That(Flag(completion, "IsReady"), Is.True);
                        Call(completion, "Continue"); yield return null; yield return null;
                    }
                    var narrative = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "MemoryRecoveryCinematic");
                    string expected = scene == "StartScene" ? "Signal" : scene == "Level1_PowerRestoration" ? "Trace" : scene == "Level2_CargoBay" ? "Fragment" : "Restored";
                    Assert.That(narrative.GetType().GetProperty("Stage").GetValue(narrative).ToString(), Is.EqualTo(expected));
                    Object.Destroy(narrative.gameObject); yield return null;
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                GameSettingsMenu.NarrativeInputBlocked = false;
            }
        }
        [UnityTest] public IEnumerator ContinueVisitsLevel1ThenCargoBayAndCargoCompletionFallsBackToMenu()
        {
            yield return Complete(); Call(completion, "Continue"); yield return null; yield return null;
            yield return WaitForNarrativeHandoff();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1_PowerRestoration"));
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(GameObject.Find("Context Intro"), Is.Null);
            var reactor = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerReactor");
            Assert.That(Flag(reactor, "IsOnline"), Is.False);
            completion = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "TutorialCompletionScreen");
            // Module tests cover solving; this test isolates the shared navigation contract.
            Set(reactor, "<IsOnline>k__BackingField", true);
            Call(completion, "TryShow"); yield return new WaitForSecondsRealtime(2.7f);
            Assert.That(Flag(completion, "IsReady"), Is.True);
            Call(completion, "Continue"); yield return null; yield return null;
            yield return WaitForNarrativeHandoff();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level2_CargoBay"));
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(GameObject.Find("Context Intro"), Is.Null);
            var root = GameObject.Find("Level 2 - Cargo Bay");
            var cargo = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "CargoBayController");
            completion = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "TutorialCompletionScreen");
            Assert.That(Flag(cargo, "CargoDelivered"), Is.False);
            Assert.That(completion.GetType().GetField("nextScene", Flags).GetValue(completion), Is.EqualTo("Level3_MaintenanceShaft"));
            // Drive the real exit check after supplying the already-tested delivery prerequisite.
            Set(cargo, "<CargoDelivered>k__BackingField", true);
            // The expanded bay's maintenance exit zone is centred at (10.5, 0.25, 16).
            GameObject.Find("Player").GetComponent<Rigidbody>().position = new Vector3(10.5f, 0, 16);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Flag(cargo, "Completed"), Is.True);
            yield return new WaitForSecondsRealtime(2.7f);
            Assert.That(Flag(completion, "IsReady"), Is.True);
            Call(completion, "ReplayTutorial"); yield return null; yield return null;
            root = GameObject.Find("Level 2 - Cargo Bay");
            cargo = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "CargoBayController");
            completion = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "TutorialCompletionScreen");
            Assert.That(Flag(cargo, "Completed"), Is.False); Assert.That(Flag(cargo, "CargoDelivered"), Is.False);
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            // A missing destination is safe, independent of whether Level 3 later joins the build.
            Set(completion, "nextScene", "MissingSceneForTest");
            Call(completion, "ShowCompletion"); yield return new WaitForSecondsRealtime(2.7f);
            Call(completion, "Continue"); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(GameSettingsMenu.StartupInputBlocked, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        }
        // From main: tutorial -> Power Restoration keeps one player and the persistent settings menu.
        [UnityTest] public IEnumerator ContinueLoadsPowerRestorationWithOnePlayerAndPersistentSettings()
        {
            var menu = GameSettingsMenu.Instance;
            yield return Complete();
            GameObject.Find("CONTINUE").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1_PowerRestoration"));
            Assert.That(GameObject.Find("Memory Recovery Narrative"), Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 35;
            while (GameObject.Find("Memory Recovery Narrative") != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GameObject.Find("Memory Recovery Narrative"), Is.Null);
            Assert.That(GameSettingsMenu.StartupInputBlocked, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(GameSettingsMenu.Instance, Is.SameAs(menu));
            Assert.That(Object.FindObjectsByType<GameSettingsMenu>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Count(x => x.GetType().Name == "PlayerController"), Is.EqualTo(1));
            Assert.That(GameObject.Find("Tutorial Completion Screen"), Is.Null);
            Assert.That(GameObject.Find("Scene 01 Objective UI"), Is.Null);
            Assert.That(GameObject.Find("Power Restoration Objective UI"), Is.Not.Null);
            Assert.That(Vector3.Distance(GameObject.Find("Player").transform.position, new Vector3(0, 1.05f, -8.2f)), Is.LessThan(.25f));
            Assert.That(Camera.main.transform.forward.z, Is.GreaterThan(.9f));
            menu.Open(); Assert.That(menu.IsOpen, Is.True); menu.Close();
        }
        [UnityTest] public IEnumerator UnavailableDestinationStillReturnsToMainMenu()
        {
            Set(completion, "nextScene", "");
            yield return Complete(); Call(completion, "Continue"); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(GameSettingsMenu.StartupInputBlocked, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        }
        static void Capture(Canvas canvas, Vector2Int size)
        {
            Directory.CreateDirectory(".utmp/completion");
            var camera = Camera.main; var rt = new RenderTexture(size.x, size.y, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .8f;
            camera.targetTexture = rt; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt; var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/completion/" + size.x + "x" + size.y + ".png", image.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; camera.targetTexture = null; RenderTexture.active = null;
            Object.Destroy(image); Object.Destroy(rt);
        }
    }
}
