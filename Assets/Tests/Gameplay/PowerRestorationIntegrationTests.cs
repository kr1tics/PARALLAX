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
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;

namespace Parallax.Gameplay.Tests
{
    public class PowerRestorationIntegrationTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        GameSettingsMenu menu;
        Rigidbody body;
        object state;
        static object Field(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
        static MonoBehaviour Component(GameObject go, string name) => go.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == name);
        static TMP_Text Label(string name) => GameObject.Find(name).GetComponent<TMP_Text>();
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        VisualElement Root => menu.GetComponent<UIDocument>().rootVisualElement;
        string Timeline => Field(state, "now").ToString();

        [UnitySetUp] public IEnumerator Setup()
        {
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false;
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration");
            yield return new WaitForSeconds(.4f);
            menu = GameSettingsMenu.Instance;
            body = GameObject.Find("Player").GetComponent<Rigidbody>();
            state = Component(body.gameObject, "PlayerController").GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            Assert.That(menu, Is.Not.Null);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.timeScale = 1; AudioListener.pause = false; GameSettingsMenu.StartupInputBlocked = false;
            yield return null;
        }

        static void Submit(Button button)
        {
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }

        [UnityTest] public IEnumerator EscapeLauncherAndResumeRestoreInputAcrossRepeatedCycles()
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // A batch Editor has no focused Game View and may reject physical pointer lock.
                // Assert restoration against the supported state, as the shared Settings tests do.
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                var priorLock = Cursor.lockState;
                if (Application.isBatchMode) Debug.Log("Batch cursor lock supported state: " + priorLock);
                if (cycle == 1) Submit(Root.Q<Button>("open-settings")); else Keys(Key.Escape);
                yield return null; yield return null;
                Assert.That(menu.IsOpen, Is.True); Assert.That(Time.timeScale, Is.Zero);
                Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.True);
                var position = body.position; var rotation = Camera.main.transform.rotation;
                Keys(Key.W, Key.Q, Key.F, Key.G);
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(100, 30));
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(body.position, Is.EqualTo(position));
                Assert.That(Camera.main.transform.rotation, Is.EqualTo(rotation));
                Assert.That(Timeline, Is.EqualTo("Future"));
                Assert.That((bool)Field(Component(body.gameObject, "ArtificialGravity"), "effective"), Is.True);
                Keys(); yield return null;
                if (cycle == 0) Keys(Key.Escape); else Submit(Root.Q<Button>("resume"));
                yield return null; yield return null;
                Assert.That(menu.IsOpen, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
                Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
                Assert.That(Cursor.lockState, Is.EqualTo(priorLock)); Assert.That(Cursor.visible, Is.False);
                Keys(); yield return null;
            }
            Assert.That(Object.FindObjectsByType<GameSettingsMenu>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            var before = body.position; Keys(Key.W);
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(20, 0));
            var oldRotation = Camera.main.transform.rotation;
            yield return new WaitForSeconds(.3f); Keys();
            Assert.That(Vector3.Distance(before, body.position), Is.GreaterThan(1));
            Assert.That(Quaternion.Angle(oldRotation, Camera.main.transform.rotation), Is.GreaterThan(1));
        }

        [UnityTest] public IEnumerator ManualDistortionIsDisabledWhileTimelineAndGravityStillWork()
        {
            Assert.That(GameObject.Find("MAINTENANCE / G SCAN"), Is.Null);
            Assert.That(Root.Q("manual-distortion-control").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(Root.Q("checkpoint-control").style.display.value, Is.EqualTo(DisplayStyle.None));
            // Level 1's scene has no distortable target (nor did it on main), so check the player's own switch and that
            // pressing G changes nothing it could act on.
            Assert.That((bool)Field(Component(body.gameObject, "PlayerController"), "allowManualDistortion"), Is.False);
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x.GetType().Name == "DistortableObject"), Is.False);
            Keys(Key.G); yield return new WaitForSeconds(.25f);
            Keys(); yield return null;
            Keys(Key.F); yield return null; yield return null;
            Assert.That(((Material)Field(state, "shader")).GetFloat("_StartTime"), Is.GreaterThanOrEqualTo(0), "Automatic timeline wipe still starts.");
            Keys(); yield return new WaitForSeconds(.7f);
            Assert.That(Timeline, Is.EqualTo("Past"));
            Assert.That(Label("Timeline status").text, Does.Contain("2026 / PAST"));
            yield return new WaitForSeconds(.4f);
            Keys(Key.T); yield return null; Keys(); yield return new WaitForSeconds(.7f);
            Assert.That(Timeline, Is.EqualTo("Future"));
            Keys(Key.Q); yield return null; Keys(); yield return null;
            Assert.That(Label("Timeline status").text, Does.Contain("GRAVITY OFF"));
            var before = body.position; Keys(Key.Space); yield return new WaitForSeconds(.6f); Keys();
            Assert.That(body.position.y, Is.GreaterThan(before.y + .5f));
            yield return null; Keys(Key.Q); yield return null; Keys(); yield return null;
            Assert.That(Label("Timeline status").text, Does.Contain("GRAVITY ON"));
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            Assert.That(GameSettingsMenu.Instance, Is.SameAs(menu));
            Assert.That(Root.Q("manual-distortion-control").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Root.Q("checkpoint-control").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That((bool)Field(Component(GameObject.Find("Player"), "PlayerController"), "allowManualDistortion"), Is.True);
        }

        [UnityTest] public IEnumerator SharedHudAndSettingsFitCommonResolutions()
        {
            var hud = GameObject.Find("Power Restoration Objective UI").GetComponent<Canvas>();
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(c => c.name.Contains("Objective UI")), Is.EqualTo(1));
            Assert.That(hud.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(Label("Objective title").text, Is.EqualTo("Restore Power Modules"));
            Assert.That(GameObject.Find("A / INSTALLATION SLOT"), Is.Not.Null);
            Assert.That(GameObject.Find("B / REPAIR"), Is.Not.Null);
            Assert.That(GameObject.Find("REACTOR / OFFLINE"), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x.GetType().Name == "Level1Controller"), Is.False);
            var document = menu.GetComponent<UIDocument>();
            Directory.CreateDirectory(".utmp/stage1-integration");
            foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                var rt = new RenderTexture(size.x, size.y, 24); rt.Create();
                var camera = Camera.main;
                camera.targetTexture = rt; hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = camera; hud.planeDistance = .8f;
                document.panelSettings.targetTexture = rt;
                for (int i = 0; i < 12; i++) yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in hud.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False, label.name);
                    Assert.That(label.font.name, Is.EqualTo(label.name.StartsWith("Objective") ? "Oxanium-Regular SDF" : "SpaceMono-Regular SDF"));
                    Assert.That(label.text, Does.Not.Contain("Distortion").And.Not.Contain("Maintenance"));
                }
                var corners = new Vector3[4];
                Label("Objective title").rectTransform.GetWorldCorners(corners);
                float objectiveRight = camera.WorldToScreenPoint(corners[2]).x;
                Label("Timeline status").rectTransform.GetWorldCorners(corners);
                Assert.That(camera.WorldToScreenPoint(corners[0]).x, Is.GreaterThan(objectiveRight));
                Assert.That(Root.Q<Button>("open-settings").style.bottom.value.value, Is.EqualTo(24));
                Capture(rt, "hud-" + size.x);
                menu.Open();
                for (int i = 0; i < 8; i++) yield return null;
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(Root.Q("manual-distortion-control").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(Root.Q<Button>("resume").worldBound.yMax, Is.LessThanOrEqualTo(Root.layout.height));
                Assert.That(Root.Q<Label>("settings-title").resolvedStyle.unityFontDefinition.font.name, Is.EqualTo("Oxanium-Regular"));
                Capture(rt, "settings-" + size.x);
                var scroll = Root.Q<ScrollView>("settings-scroll");
                scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
                for (int i = 0; i < 5; i++) yield return null;
                Capture(rt, "controls-" + size.x);
                menu.Close(); yield return null; yield return null;
                hud.renderMode = RenderMode.ScreenSpaceOverlay; camera.targetTexture = null;
                document.panelSettings.targetTexture = null; Object.Destroy(rt);
            }
            var presenter = Component(GameObject.Find("08 / Scene presentation"), "PowerRestorationPresentation");
            presenter.GetType().GetMethod("SetObjective").Invoke(presenter, new object[] { "OBJECTIVE 02", "Objective update", "Shared presentation remains available." });
            yield return null;
            Assert.That(Label("Objective number").text, Is.EqualTo("OBJECTIVE 02"));
        }

        static void Capture(RenderTexture rt, string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/stage1-integration/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; Object.Destroy(image);
        }
    }
}
