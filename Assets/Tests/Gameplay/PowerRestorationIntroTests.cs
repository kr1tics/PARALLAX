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
        static object Field(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
        static void Set(object value, string name, object data) => value.GetType().GetField(name, Fields).SetValue(value, data);
        static MonoBehaviour Find(string name) => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == name);
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
            foreach (var item in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(x => x.GetType().Name == "PowerRestorationIntro")) item.enabled = false;
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.timeScale = 1; AudioListener.pause = false; GameSettingsMenu.StartupInputBlocked = GameSettingsMenu.NarrativeInputBlocked = false;
            yield return null;
        }

        IEnumerator ContinueFromTutorial()
        {
            Keys(); yield return SceneManager.LoadSceneAsync("StartScene"); yield return null; yield return null;
            var level = Find("Level1Controller"); Set(level, "<IntroSolved>k__BackingField", true); Set(level, "<MainSolved>k__BackingField", true);
            level.GetType().GetMethod("TryComplete").Invoke(level, null);
            var completion = Find("TutorialCompletionScreen"); float deadline = Time.realtimeSinceStartup + 5;
            while (!(bool)completion.GetType().GetProperty("IsReady").GetValue(completion) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            GameObject.Find("CONTINUE").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1_PowerRestoration"));
            Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.True);
            Assert.That(GameSettingsMenu.StartupInputBlocked, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(GameObject.Find("Power Restoration Objective UI").GetComponent<CanvasGroup>().alpha, Is.Zero);
        }

        static IEnumerator Until(Func<bool> condition, float seconds = 6)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for the narrative state.");
        }

        [UnityTest] public IEnumerator CompleteNarrativeRestoresCachesStateBasedHud()
        {
            yield return ContinueFromTutorial();
            yield return new WaitForSecondsRealtime(1);
            Assert.That(GameObject.Find("Terminal message").GetComponent<TMP_Text>().text, Is.EqualTo("Emergency interface… online."));
            Capture("black-interface", 1280, 720);
            yield return Until(() => GameObject.Find("Terminal status").GetComponent<TMP_Text>().text.Contains("READ FAILED"), 12);
            yield return Until(() => GameObject.Find("Terminal message").GetComponent<TMP_Text>().text.Contains("final transmission"));
            Assert.That(GameObject.Find("Station reveal blackout").GetComponent<UnityEngine.UI.Image>().color.a, Is.EqualTo(1));
            yield return Until(() => !GameSettingsMenu.NarrativeInputBlocked, 12);
            Assert.That(GameObject.Find("Terminal status").GetComponent<TMP_Text>().text, Is.EqualTo("FIRST OBJECTIVE — RESTORE POWER"));
            Assert.That(GameObject.Find("Power Restoration Objective UI").GetComponent<CanvasGroup>().alpha, Is.Zero);
            yield return new WaitForSecondsRealtime(.8f);
            foreach (var size in new[] { new Vector2Int(960,540), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(1920,1080), new Vector2Int(2560,1080) }) Capture("objective-" + size.x, size.x, size.y);
            yield return Until(() => GameObject.Find("Power Restoration Opening") == null);
            Assert.That(GameObject.Find("Power Restoration Objective UI").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(GameObject.Find("Objective title").GetComponent<TMP_Text>().text, Is.EqualTo("Restore Power Modules"));
            Assert.That(Find("PowerRestorationPresentation").GetType().GetProperty("ReactorReady").GetValue(Find("PowerRestorationPresentation")), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
        }

        [UnityTest] public IEnumerator SkippingSettingsAndRestartKeepCachesGameplayAndProgression()
        {
            foreach (float delay in new[] { .5f, 9f, 17f })
            {
                yield return ContinueFromTutorial();
                var player = GameObject.Find("Player").GetComponent<Rigidbody>(); var position = player.position;
                var controller = player.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "PlayerController");
                var state = controller.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
                var timeline = Field(state, "now"); var flight = Field(controller, "isInJetpack");
                Keys(Key.W, Key.T, Key.Q, Key.E); yield return new WaitForSecondsRealtime(delay); Keys(); yield return null;
                Assert.That(player.position, Is.EqualTo(position));
                Assert.That(Field(state, "now"), Is.EqualTo(timeline));
                Assert.That(Field(controller, "isInJetpack"), Is.EqualTo(flight));
                var intro = Find("PowerRestorationIntro"); var view = Field(intro, "view");
                var ageField = view.GetType().GetField("age", Fields);
                GameSettingsMenu.Instance.Open(); yield return null; float age = (float)ageField.GetValue(view);
                Assert.That(GameSettingsMenu.Instance.IsOpen, Is.True); Assert.That(Cursor.visible, Is.True);
                Keys(Key.Space); yield return new WaitForSecondsRealtime(.2f); Keys(); yield return null;
                Assert.That((float)ageField.GetValue(view), Is.EqualTo(age));
                GameSettingsMenu.Instance.Close(); yield return null; yield return null;
                Keys(Key.Space); yield return null; yield return null;
                Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.True);
                Keys(); yield return Until(() => !GameSettingsMenu.NarrativeInputBlocked);
                Assert.That(player.linearVelocity.y, Is.LessThan(1));
                yield return Until(() => GameObject.Find("Power Restoration Opening") == null);
                Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
                Assert.That(Cursor.lockState, Is.EqualTo((CursorLockMode)Field(view, "previousCursorLock")));
                Assert.That(Cursor.visible, Is.EqualTo((bool)Field(view, "previousCursorVisible")));
                Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(AudioListener.pause, Is.False);
                Assert.That(Find("PowerModuleA").GetType().GetProperty("IsRepaired").GetValue(Find("PowerModuleA")), Is.False);
                Assert.That(Find("PowerModuleB").GetType().GetProperty("IsRepaired").GetValue(Find("PowerModuleB")), Is.False);
                if (delay < 1)
                {
                    var reactor = Find("PowerReactor"); Set(reactor, "<IsOnline>k__BackingField", true);
                    var completion = Find("TutorialCompletionScreen"); completion.GetType().GetMethod("TryShow").Invoke(completion, null);
                    yield return new WaitForSecondsRealtime(2.7f);
                    GameObject.Find("RESTART LEVEL").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; yield return null;
                }
                else { yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null; }
                Assert.That(GameObject.Find("Power Restoration Opening"), Is.Null);
                Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            }
        }

        [UnityTest] public IEnumerator TutorialEffectSettingDoesNotChangeOtherLevels()
        {
            foreach (string scene in new[] { "StartScene", "Level1_PowerRestoration", "Level2_CargoBay" })
            {
                yield return SceneManager.LoadSceneAsync(scene); yield return null; yield return null;
                var player = GameObject.Find("Player");
                var interaction = player.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Interact");
                Assert.That(Field(interaction, "automaticInteractionEffects"), Is.EqualTo(scene != "StartScene"));
                Assert.That(GameObject.Find("Power Restoration Opening"), Is.Null);
                Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.False);
            }
        }

        static void Capture(string name, int width, int height)
        {
            Directory.CreateDirectory(".utmp/selective-intro");
            var canvas = GameObject.Find("Power Restoration Opening").GetComponent<Canvas>();
            var backdrop = GameObject.Find("Power Restoration Backdrop").GetComponent<Canvas>();
            var camera = Camera.main; var rt = new RenderTexture(width,height,24);
            camera.targetTexture = rt;
            canvas.renderMode = backdrop.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = backdrop.worldCamera = camera; canvas.planeDistance = .8f; backdrop.planeDistance = .9f;
            Canvas.ForceUpdateCanvases(); camera.Render();
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.name); }
            var old = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            File.WriteAllBytes(".utmp/selective-intro/"+name+".png",image.EncodeToPNG());
            camera.targetTexture = null; canvas.renderMode = backdrop.renderMode = RenderMode.ScreenSpaceOverlay; RenderTexture.active = old;
            Object.Destroy(image); Object.Destroy(rt);
        }
    }
}
