using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class CargoBayPlayModeTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        protected MonoBehaviour player, level;
        protected Rigidbody body;
        protected static object Get(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
        protected static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        protected static MonoBehaviour Component(GameObject obj, string name) => obj.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == name);

        [UnitySetUp] public IEnumerator Load()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level2_CargoBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Level2_CargoBay");
#endif
            yield return null;
            player = Component(GameObject.Find("Player"), "PlayerController"); body = player.GetComponent<Rigidbody>();
            var cargoRoot = GameObject.Find("Level 2 - Cargo Bay");
            if (cargoRoot != null) level = Component(cargoRoot, "CargoBayController");
            Call(Component(Camera.main.gameObject, "Look"), "Rotate", new Vector2(90, 0));
            yield return new WaitForSeconds(.5f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            GameSettingsMenu.StartupInputBlocked = false; Time.timeScale = 1; yield return null;
        }

        protected IEnumerator Keys(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        bool Flag(string name) => (bool)level.GetType().GetProperty(name).GetValue(level);
        object State => player.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
        MonoBehaviour Interaction => Component(player.gameObject, "Interact");
        object Held => Interaction.GetType().GetProperty("HeldObject").GetValue(Interaction);
        string Timeline => Get(State, "now").ToString();

        IEnumerator Face(float yaw, float pitch = 0)
        {
            var look = Component(Camera.main.gameObject, "Look");
            var old = (Vector2)Call(look, "currentAngles");
            float sensitivity = .2f * GameSettings.MouseSensitivity;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(-Mathf.DeltaAngle(old.x, yaw) / sensitivity,
                (pitch - old.y) / (sensitivity * .005f) * (GameSettings.InvertLook ? -1 : 1)));
            yield return null; yield return null;
        }

        IEnumerator WalkTo(float x, float z)
        {
            var delta = new Vector3(x, 0, z) - body.position;
            yield return Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            float deadline = Time.time + 7;
            while (Vector2.Distance(new Vector2(body.position.x, body.position.z), new Vector2(x, z)) > .3f && Time.time < deadline)
                yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector2.Distance(new Vector2(body.position.x, body.position.z), new Vector2(x, z)), Is.LessThan(.7f), "Walking route blocked at " + body.position + " towards " + x + "," + z);
        }

        IEnumerator Swap()
        {
            yield return new WaitForSeconds(1.05f); yield return Keys(.06f, Key.T);
            yield return new WaitForSeconds(.65f);
        }

        void Capture(string name)
        {
            Directory.CreateDirectory(".utmp/cargo");
            var camera = Camera.main; var target = new RenderTexture(1280, 720, 24);
            var canvas = GameObject.Find("Cargo HUD").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/cargo/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(image); Object.Destroy(target);
        }

        [UnityTest] public IEnumerator NormalControlsCarryPastCargoIntoFutureAndReachExit()
        {
            float started = Time.time;
            Assert.That(Flag("CargoDelivered"), Is.False); Capture("arrival");
            yield return Swap(); Assert.That(Timeline, Is.EqualTo("Past"));
            yield return WalkTo(-7, -8);
            yield return Face(90, -.07f); yield return Keys(.06f, Key.E);
            Assert.That(Held, Is.Not.Null, "E must pick up the Past crate through the real interaction ray.");
            yield return new WaitForSeconds(.5f);
            yield return WalkTo(-5.5f, -8);
            yield return WalkTo(-5.5f, 6.5f);
            yield return WalkTo(5.2f, 6.5f);
            yield return Face(0, -.09f); yield return new WaitForSeconds(.8f);
            Capture("past-placement");
            yield return Keys(.06f, Key.E); yield return new WaitForSeconds(1);
            Assert.That(Held, Is.Null); Assert.That(Flag("CargoDelivered"), Is.False, "Past contact cannot open the Future exit.");
            yield return Swap(); yield return new WaitForSeconds(1);
            var future = (Rigidbody)Get(level, "futureCrate");
            Assert.That(Flag("CargoDelivered"), Is.True, "Future contact must result from the carried Past position: " + future.position);
            Capture("future-delivery");
            yield return WalkTo(5.2f, 8);
            yield return WalkTo(7, 8);
            yield return WalkTo(7, 10.5f);
            Assert.That(Flag("Completed"), Is.True);
            File.WriteAllText("Logs/cargo-normal-controls.txt", "PASS: keyboard/mouse input, pickup, carry, release, time swap, plate, door and exit. Scripted route seconds: " + (Time.time - started));
        }

        [UnityTest] public IEnumerator FutureCageJetpackAndTimeSwitchCannotBypassDelivery()
        {
            yield return WalkTo(-5.5f, -8); yield return WalkTo(-5.5f, 5);
            yield return Face(180, -.06f); yield return Keys(.06f, Key.E);
            Assert.That(Held, Is.Null, "The nearest cage wall must block picking up the Future crate.");
            yield return Keys(.06f, Key.Q); yield return Keys(2, Key.Space);
            Assert.That(body.position.y, Is.LessThan(4.3f), "Cargo roof must contain a flying player.");
            yield return Swap(); Assert.That(Flag("CargoDelivered"), Is.False);
            // Sample the entire bulkhead, including above the door, in both epochs.
            foreach (float y in new[] { -.5f, .5f, 2.5f, 4.5f })
            for (float x = -11; x <= 11; x += .5f)
                Assert.That(Physics.Raycast(new Vector3(x, y, 8), Vector3.forward, 2, ~0, QueryTriggerInteraction.Ignore), Is.True, "Exit gap at " + x + "," + y);
            yield return Swap();
            Assert.That(Flag("Completed"), Is.False);
            Assert.That(((Component)Get(level, "exitDoor")).GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest] public IEnumerator ResetRecoversLostCargoPlayerJetpackAndPendingTimeSwap()
        {
            yield return Swap();
            var past = (Rigidbody)Get(level, "pastCrate"); var future = (Rigidbody)Get(level, "futureCrate");
            past.position = new Vector3(0, -30, 0); future.position = new Vector3(0, -40, 0);
            yield return Keys(.06f, Key.Q);
            yield return new WaitForSeconds(1.05f); yield return Keys(.06f, Key.T);
            yield return Keys(.06f, Key.R); yield return new WaitForSeconds(.8f);
            Assert.That(Timeline, Is.EqualTo("Future")); Assert.That(Flag("CargoDelivered"), Is.False);
            Assert.That(Vector3.Distance(body.position, new Vector3(0, 0, -10)), Is.LessThan(.2f));
            Assert.That(Vector3.Distance(future.position, new Vector3(-8, -.55f, 5)), Is.LessThan(.2f));
            Assert.That((bool)Get(player, "isInJetpack"), Is.False);
            Assert.That(Held, Is.Null);
            yield return Swap();
            Assert.That(Vector3.Distance(past.position, new Vector3(-7, -.55f, -6)), Is.LessThan(.2f), "Reset must rebase causality as well as restore transforms.");
            body.position = new Vector3(0, -10, 0); yield return new WaitForSeconds(.3f);
            Assert.That(body.position.y, Is.GreaterThan(-.2f), "Automatic fall recovery.");
            Assert.That(Timeline, Is.EqualTo("Future"));
        }

        [UnityTest] public IEnumerator LayoutOpensAndNormalMovementStaysOnDeck()
        {
            Assert.That(body.position.y, Is.InRange(-.2f, .3f));
            Vector3 before = body.position;
            yield return Keys(.45f, Key.W);
            Assert.That(body.position.z, Is.GreaterThan(before.z + 1), "W must move the real player. position=" + body.position + " velocity=" + body.linearVelocity + " blocked=" + GameSettingsMenu.GameplayInputBlocked + " intent=" + Get(player, "movementIntent") + " camera=" + Camera.main.transform.forward + " feet=" + ((Transform)Get(player, "feet")).position);
            Assert.That(body.position.y, Is.InRange(-.2f, .3f));
            Assert.That(Physics.Raycast(body.position, Vector3.up, 6), Is.True, "Roof prevents a jetpack bypass.");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(transform.GetComponents<MonoBehaviour>().Any(c => c == null), Is.False, transform.name);
        }
    }
}
