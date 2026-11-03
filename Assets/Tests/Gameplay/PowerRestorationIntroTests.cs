using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public sealed class PowerRestorationIntroTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static MonoBehaviour Find(string name) => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == name);
        static void Set(object value, string name, object data) => value.GetType().GetField(name, Fields).SetValue(value, data);
        static void Call(object value, string name) => value.GetType().GetMethod(name).Invoke(value, null);
        static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        static TMP_Text Text(string name) => GameObject.Find(name).GetComponent<TMP_Text>();
        static MonoBehaviour Narrative => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetType().Name == "MemoryRecoveryCinematic");
        static int Page => Narrative == null ? -1 : (int)Property(Narrative, "PageIndex");
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        [UnitySetUp] public IEnumerator Setup()
        {
            background = InputSystem.settings.backgroundBehavior; editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keyboard.MakeCurrent(); mouse.MakeCurrent();
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = GameSettingsMenu.NarrativeInputBlocked = false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Narrative != null) { Object.Destroy(Narrative.gameObject); yield return null; }
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.timeScale = 1; AudioListener.pause = false;
            GameSettingsMenu.StartupInputBlocked = GameSettingsMenu.NarrativeInputBlocked = false;
        }
        static IEnumerator Until(Func<bool> condition, float seconds = 8)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for narrative progression.");
        }
        IEnumerator ContinueLevel()
        {
            var completion = Find("TutorialCompletionScreen");
            Call(completion, "ShowCompletion");
            yield return Until(() => (bool)Property(completion, "IsReady"));
            GameObject.Find("CONTINUE").GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
        }
        IEnumerator EnterSignal()
        {
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null; yield return null;
            var level = Find("Level1Controller"); Set(level, "<IntroSolved>k__BackingField", true); Set(level, "<MainSolved>k__BackingField", true);
            Call(level, "TryComplete"); yield return Until(() => (bool)Property(Find("TutorialCompletionScreen"), "IsReady"));
            GameObject.Find("CONTINUE").GetComponent<Button>().onClick.Invoke(); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1_PowerRestoration"));
            Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.True);
        }
        IEnumerator Skip()
        {
            Keys(); yield return new WaitForSecondsRealtime(.35f); Keys(Key.Space); yield return null; yield return null;
            Keys(); yield return null;
        }
        static void Capture(string name, int width = 1280, int height = 720)
        {
            Directory.CreateDirectory(".utmp/memory-recovery");
            var canvas = GameObject.Find("Memory Recovery Canvas")?.GetComponent<Canvas>() ?? GameObject.Find("Cooling System Test Interface").GetComponent<Canvas>();
            var cameraObject = new GameObject("Narrative capture camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << 5; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var otherCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c != canvas && c.enabled).ToArray();
            foreach (var other in otherCanvases) other.enabled = false;
            var layers = canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
            foreach (var item in layers) item.Key.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>())
            { label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False, name + ": " + label.name); }
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(".utmp/memory-recovery/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
            foreach (var item in layers) item.Key.gameObject.layer = item.Value;
            foreach (var other in otherCanvases) other.enabled = true;
            camera.targetTexture = null; Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(texture);
        }
        static void Palette(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var expected);
            Assert.That(GameObject.Find("Narrative background").GetComponent<Image>().color, Is.EqualTo(expected));
        }
        [UnityTest] public IEnumerator CompleteTemporaryStoryFlowAndCaptureAllThemes()
        {
            yield return EnterSignal(); yield return new WaitForSecondsRealtime(1);
            Palette("#061016"); Assert.That(Text("Terminal message").text, Is.EqualTo("Emergency interface... online."));
            Capture("01-cyan");
            yield return Until(() => Page == 1); yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Text("Terminal message").text, Is.EqualTo("Unidentified memory signature detected."));
            Assert.That(Text("Terminal detail").text, Is.EqualTo("Source unknown."));
            yield return Until(() => Page == 2);
            Assert.That(Text("Terminal message").text, Is.EqualTo("Restore main power to access the station's final transmission."));
            yield return Until(() => Narrative == null, 10);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            yield return ContinueLevel();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1_PowerRestoration"));
            Palette("#100F20"); yield return new WaitForSecondsRealtime(1); Capture("02-violet");
            yield return Until(() => Page == 1);
            Assert.That(Text("Terminal message").text, Is.EqualTo("Memory signature match found."));
            yield return Until(() => Page == 2); yield return Skip();
            yield return Until(() => SceneManager.GetActiveScene().name == "Level2_CargoBay"); yield return null; yield return null;
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            yield return ContinueLevel(); Palette("#1C130D");
            yield return new WaitForSecondsRealtime(1); Capture("03-amber");
            yield return Until(() => Page == 1); Assert.That(Text("Terminal detail").text, Is.EqualTo("Current memory signature: identical."));
            yield return Until(() => Page == 2); Assert.That(Text("Terminal message").text, Is.EqualTo("Core archive overheating."));
            yield return Until(() => SceneManager.GetActiveScene().name == "Level3_ColdStorage", 10); yield return null; yield return null;
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            yield return ContinueLevel();
            Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.True); Palette("#17121B");
            yield return Until(() => Page == 1); Assert.That(Text("Terminal message").text, Is.EqualTo("Research personnel evacuated."));
            yield return Until(() => Page == 2); yield return new WaitForSecondsRealtime(1); Capture("05-identity-mismatch");
            Assert.That(Text("Terminal detail").text, Is.EqualTo("Current host: Synthetic."));
            yield return Until(() => Page == 3); Assert.That(Text("Terminal message").text, Is.EqualTo("You were never the researcher."));
            yield return Until(() => Page == 4); yield return new WaitForSecondsRealtime(4); Capture("06-station-remembers");
            yield return Until(() => Narrative != null && (bool)Property(Narrative, "EndCardReady"));
            yield return new WaitForSecondsRealtime(2.5f); Capture("07-parallax-end");
            GameObject.Find("Return to Main Menu").GetComponent<Button>().onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu"); yield return null;
            Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SkipPauseInputAndReplayAreSafe()
        {
            yield return EnterSignal(); yield return new WaitForSecondsRealtime(1);
            var player = Find("PlayerController"); var body = player.GetComponent<Rigidbody>(); var position = body.position;
            Keys(Key.W, Key.Q, Key.T, Key.E); yield return new WaitForSecondsRealtime(.4f); Keys(); yield return null;
            Assert.That(body.position, Is.EqualTo(position)); Assert.That((bool)player.GetType().GetField("isInJetpack", Fields).GetValue(player), Is.False);
            Keys(Key.P); yield return null; yield return null; Keys(); yield return null;
            Assert.That(GameSettingsMenu.Instance.IsOpen, Is.True); int page = Page;
            yield return new WaitForSecondsRealtime(4.5f); Assert.That(Page, Is.EqualTo(page));
            Keys(Key.P); yield return null; yield return null; Keys(); yield return null;
            Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False);
            yield return Skip(); yield return Until(() => Narrative == null);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(Cursor.visible, Is.False);
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null; yield return null;
            Assert.That(Narrative, Is.Null);
            yield return ContinueLevel(); yield return Skip();
            yield return Until(() => SceneManager.GetActiveScene().name == "Level2_CargoBay"); yield return null; yield return null;
            yield return ContinueLevel(); yield return Skip();
            yield return Until(() => SceneManager.GetActiveScene().name == "Level3_ColdStorage"); yield return null; yield return null;
            yield return ContinueLevel(); yield return Skip();
            yield return Until(() => Narrative != null && (bool)Property(Narrative, "EndCardReady")); yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level3_ColdStorage"));
            Assert.That(Cursor.visible, Is.True);
            GameObject.Find("Return to Main Menu").GetComponent<Button>().onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu");
            yield return EnterSignal(); Assert.That(Page, Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator AllStoryPagesAndEndCardFitCommonAspectRatios()
        {
            yield return EnterSignal();
            var type = Narrative.GetType();
            var stageType = type.Assembly.GetType("MemoryRecoveryStage");
            foreach (var stage in new[] { "Signal", "Trace", "Fragment", "Restored" })
            {
                Object.Destroy(Narrative.gameObject); yield return null;
                type.GetMethod("Play").Invoke(null, new object[] { Enum.Parse(stageType, stage), null });
                int pages = stage == "Restored" ? 5 : 3;
                for (int page = 0; page < pages; page++)
                {
                    type.GetMethod("ShowPage", Fields).Invoke(Narrative, new object[] { page });
                    yield return new WaitForSecondsRealtime(.85f);
                    foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1024, 768), new Vector2Int(1920, 1080) })
                        Capture(stage + "-page-" + page + "-" + size.x, size.x, size.y);
                }
            }
            type.GetMethod("ShowEndCard", Fields).Invoke(Narrative, null);
            yield return new WaitForSecondsRealtime(2.5f);
            foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1024, 768), new Vector2Int(1920, 1080) })
                Capture("ending-" + size.x, size.x, size.y);
            Assert.That(GameObject.Find("Return to Main Menu").GetComponent<Button>().interactable, Is.True);
        }

        static void AssertDisplayCamera()
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c.isActiveAndEnabled && c.targetTexture == null && c.targetDisplay == 0 && c.rect == new Rect(0, 0, 1, 1)).ToArray();
            Assert.That(cameras, Has.Length.EqualTo(1), "A real scene camera must render Display 1 before any capture camera is created.");
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled), Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator CenteredTracePurpleCargoHudAndRealDisplayCamera()
        {
            yield return EnterSignal(); yield return Skip(); yield return Until(() => Narrative == null);
            yield return ContinueLevel(); yield return new WaitForSecondsRealtime(.8f);
            foreach (string label in new[] { "Terminal status", "Terminal message", "Terminal detail" })
                Assert.That(Text(label).alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Capture("fixed-trace-centered");
            yield return Skip(); yield return Until(() => SceneManager.GetActiveScene().name == "Level2_CargoBay"); yield return null; yield return null;
            var hud = GameObject.Find("Cargo Bay Objective UI");
            var labels = hud.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(labels.Single(x => x.name == "Objective number").color, Is.EqualTo((Color)new Color32(181, 160, 255, 255)));
            Assert.That(hud.transform.Find("Objective panel").GetComponent<Image>().color, Is.EqualTo((Color)new Color32(16, 15, 32, 225)));
            Assert.That(labels.Single(x => x.name == "Objective title").text, Is.EqualTo("Open Freight Access"));
            yield return ContinueLevel(); yield return Skip();
            yield return Until(() => SceneManager.GetActiveScene().name == "Level3_ColdStorage"); yield return null; yield return null;
            AssertDisplayCamera();
            yield return ContinueLevel(); yield return new WaitForSecondsRealtime(.8f);
            AssertDisplayCamera(); Capture("fixed-final-camera");
            yield return Skip(); yield return Until(() => (bool)Property(Narrative, "EndCardReady"));
            yield return new WaitForSecondsRealtime(2.5f); AssertDisplayCamera(); Capture("fixed-final-ending-camera");
            GameObject.Find("Return to Main Menu").GetComponent<Button>().onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu");
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null; yield return null;
            var powerHud = GameObject.Find("Power Restoration Objective UI");
            Assert.That(powerHud.GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "Objective number").color, Is.EqualTo(new Color(.38f, .76f, .83f)));
        }

        [UnityTest] public IEnumerator NarrativeLayoutsFitCommonWebGLResolutions()
        {
            yield return EnterSignal(); yield return new WaitForSecondsRealtime(.8f);
            foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1024, 768), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
                Capture("cyan-" + size.x, size.x, size.y);
            yield return Skip(); yield return Until(() => Narrative == null);
            yield return ContinueLevel(); yield return new WaitForSecondsRealtime(.8f);
            foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1024, 768), new Vector2Int(1920, 1080) })
                Capture("violet-" + size.x, size.x, size.y);
            yield return Skip(); yield return Until(() => SceneManager.GetActiveScene().name == "Level2_CargoBay"); yield return null; yield return null;
            yield return ContinueLevel(); yield return new WaitForSecondsRealtime(.8f);
            foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1024, 768), new Vector2Int(1920, 1080) })
                Capture("amber-" + size.x, size.x, size.y);
        }
    }
}
