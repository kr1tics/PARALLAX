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
            // These tests exercise puzzle physics; completion navigation has its own integration tests.
            if (cargoRoot != null) { level = Component(cargoRoot, "CargoBayController"); Component(cargoRoot, "TutorialCompletionScreen").enabled = false; }
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
            yield return WalkTo(-10.5f, -11);
            yield return Face(90, -.07f); yield return Keys(.06f, Key.E);
            Assert.That(Held, Is.Not.Null);
            yield return WalkTo(-10.5f, -5);
            yield return Face(90, -.09f); yield return new WaitForSeconds(.8f);
            yield return Keys(.06f, Key.E); yield return new WaitForSeconds(1);
            Assert.That(Flag("FreightAccessGranted"), Is.False, "Past contact alone cannot unlock freight.");
            Capture("past-staging");
            yield return Swap(); yield return new WaitForSeconds(.6f);
            Assert.That(Flag("FreightAccessGranted"), Is.True);
            Assert.That(Flag("CargoDelivered"), Is.False, "Staging is only the first step.");
            yield return Face(90, -.07f); yield return Keys(.06f, Key.E);
            Assert.That(Held, Is.Not.Null, "Carry the Future counterpart while its Past version stays at A.");
            // Wide southern detour: a deliberate alternative to the shorter northern freight aisle.
            yield return WalkTo(-10.5f, -11); yield return WalkTo(10.5f, -11);
            yield return WalkTo(10.5f, 10); yield return WalkTo(11.5f, 10);
            yield return Face(0, -.09f); yield return new WaitForSeconds(.8f);
            yield return Keys(.06f, Key.E); yield return new WaitForSeconds(1);
            Assert.That(Flag("CargoDelivered"), Is.True, "Receiving requires Future cargo and preserved Past staging.");
            Capture("future-delivery");
            yield return WalkTo(10.5f, 10); yield return WalkTo(10.5f, 16);
            Assert.That(Flag("Completed"), Is.True);
            File.WriteAllText("Logs/cargo-normal-controls.txt", "PASS: keyboard/mouse input, pickup, carry, release, time swap, plate, door and exit. Scripted route seconds: " + (Time.time - started));
            var completion = Component(level.gameObject, "TutorialCompletionScreen");
            completion.enabled = true;
            yield return new WaitForSecondsRealtime(2.8f);
            Assert.That((bool)completion.GetType().GetProperty("IsReady").GetValue(completion), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Call(completion, "RestartLevel"); yield return null; yield return null;
            level = Component(GameObject.Find("Level 2 - Cargo Bay"), "CargoBayController");
            Assert.That(Flag("Completed"), Is.False);
            Assert.That(Flag("FreightAccessGranted"), Is.False);
            Assert.That(Flag("CargoDelivered"), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
        }

        [UnityTest] public IEnumerator UnavailableMaintenanceDestinationReturnsToMenu()
        {
            var completion = Component(level.gameObject, "TutorialCompletionScreen");
            Assert.That((string)Get(completion, "nextScene"), Is.EqualTo("Level3_MaintenanceShaft"));
            // Isolate the fallback contract even when another branch later supplies Level 3.
            completion.GetType().GetField("nextScene", Fields).SetValue(completion, "UnavailableSceneForTest");
            level.GetType().GetField("<Completed>k__BackingField", Fields).SetValue(level, true);
            completion.enabled = true;
            yield return new WaitForSecondsRealtime(2.8f);
            Assert.That(GameObject.Find("MAIN MENU"), Is.Not.Null);
            Call(completion, "Continue"); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(GameSettingsMenu.StartupInputBlocked, Is.False);
            Assert.That(GameSettingsMenu.Instance.IsMainMenuContext, Is.True);
        }

        [UnityTest] public IEnumerator FutureCageJetpackAndTimeSwitchCannotBypassDelivery()
        {
            yield return WalkTo(-10.5f, -11);
            yield return Face(180, -.06f); yield return Keys(.06f, Key.E);
            Assert.That(Held, Is.Null, "The nearest cage wall must block picking up the Future crate.");
            yield return Keys(.06f, Key.Q); yield return Keys(2, Key.Space);
            Assert.That(body.position.y, Is.LessThan(4.3f), "Cargo roof must contain a flying player.");
            yield return Swap(); Assert.That(Flag("CargoDelivered"), Is.False);
            // Sample the entire bulkhead, including above the door, in both epochs.
            foreach (float y in new[] { -.5f, .5f, 2.5f, 4.5f })
            for (float x = -17; x <= 17; x += .5f)
                Assert.That(Physics.Raycast(new Vector3(x, y, 12.4f), Vector3.forward, 2, ~0, QueryTriggerInteraction.Ignore), Is.True, "Exit gap at " + x + "," + y);
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
            Assert.That(Vector3.Distance(body.position, new Vector3(0, 0, -15)), Is.LessThan(.2f));
            Assert.That(Vector3.Distance(future.position, new Vector3(-14, -.55f, -11)), Is.LessThan(.2f));
            Assert.That((bool)Get(player, "isInJetpack"), Is.False);
            Assert.That(Held, Is.Null);
            yield return Swap();
            Assert.That(Vector3.Distance(past.position, new Vector3(-10.5f, -.55f, -9)), Is.LessThan(.2f), "Reset must rebase causality as well as restore transforms.");
            body.position = new Vector3(0, -10, 0); yield return new WaitForSeconds(.3f);
            Assert.That(body.position.y, Is.GreaterThan(-.2f), "Automatic fall recovery.");
            Assert.That(Timeline, Is.EqualTo("Future"));
        }

        static void Place(Rigidbody item, Vector3 position)
        {
            item.position = position; item.transform.position = position;
            item.linearVelocity = item.angularVelocity = Vector3.zero;
            item.WakeUp(); Physics.SyncTransforms();
        }

        [UnityTest] public IEnumerator ReceivingCannotSkipStagingOrOverwriteThePastAnchor()
        {
            var past = (Rigidbody)Get(level, "pastCrate"); var future = (Rigidbody)Get(level, "futureCrate");
            Place(future, new Vector3(13.5f, -.2f, 10)); yield return new WaitForSeconds(1);
            Assert.That(Flag("CargoDelivered"), Is.False, "Delivering straight to B cannot skip A.");
            yield return Swap(); Place(past, new Vector3(-10.5f, -.2f, -3)); yield return new WaitForSeconds(1);
            yield return Swap(); yield return new WaitForSeconds(.5f);
            Assert.That(Flag("FreightAccessGranted"), Is.True);
            // Simulate an invalid changed Past position before the Future crate reaches B.
            past.transform.position = new Vector3(-10.5f, -.54f, -9);
            Place(future, new Vector3(13.5f, -.2f, 10)); yield return new WaitForSeconds(1);
            Assert.That(Flag("CargoDelivered"), Is.False, "A must still preserve the Past crate.");
            yield return Swap(); Place(past, new Vector3(-10.5f, -.2f, -3)); yield return new WaitForSeconds(1);
            yield return Swap(); yield return new WaitForSeconds(.5f);
            Place(future, new Vector3(13.5f, -.2f, 10)); yield return new WaitForSeconds(1);
            Assert.That(Flag("CargoDelivered"), Is.True);
            yield return Swap(); yield return Swap();
            Assert.That(Vector3.Distance(future.position, new Vector3(13.5f, -.3f, 10)), Is.LessThan(.2f), "Unchanged Past must preserve the independent Future delivery.");
            yield return Keys(.06f, Key.R); yield return new WaitForSeconds(.2f);
            Assert.That(Flag("FreightAccessGranted"), Is.False); Assert.That(Flag("CargoDelivered"), Is.False);
            foreach (string gate in new[] { "freightDoor", "exitDoor" })
                Assert.That(((Component)Get(level, gate)).GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest] public IEnumerator ExpandedDeckAndAlternateNorthAisleAreTraversable()
        {
            var deck = GameObject.Find("Floor_Cargo backing").GetComponent<Collider>().bounds;
            Assert.That(deck.size.x, Is.EqualTo(36).Within(.01f)); Assert.That(deck.size.z, Is.EqualTo(36).Within(.01f));
            yield return WalkTo(-8.2f, -11); yield return WalkTo(-8.2f, 6.2f); yield return WalkTo(10.5f, 6.2f);
            for (int epoch = 0; epoch < 2; epoch++)
            {
                foreach (float y in new[] { -.5f, .5f, 2.5f, 4.5f })
                for (float x = -17; x <= 17; x += .5f)
                    Assert.That(Physics.Raycast(new Vector3(x, y, 6.3f), Vector3.forward, 2, ~0, QueryTriggerInteraction.Ignore), Is.True, "Freight bulkhead gap.");
                yield return Swap();
            }
        }

        [UnityTest] public IEnumerator RedesignedHallIsUnstretchedAndMezzanineIsJetpackReachable()
        {
            var hall = GameObject.Find("Cargo Bay - modular freight hall").transform;
            Assert.That(hall.lossyScale, Is.EqualTo(Vector3.one), "Modular kit must not be stretched.");
            var architecture = hall.Find("Cargo Bay - architecture");
            Assert.That(architecture.Cast<Transform>().Count(t => t.name == "Floor_01"), Is.EqualTo(36));
            Assert.That(architecture.Cast<Transform>().Count(t => t.name == "Wall_Simple_01"), Is.EqualTo(24));
            Assert.That(Physics.Raycast(new Vector3(-11, 0, 8.8f), Vector3.left, 4, ~0, QueryTriggerInteraction.Ignore), Is.False, "Control booth doorway.");
            Capture("loading");
            yield return WalkTo(-8.2f, -11); yield return WalkTo(-8.2f, -1.2f); yield return WalkTo(-11.4f, -1.2f);
            yield return Face(90); Capture("freight-hall"); yield return Face(180);
            yield return Keys(.06f, Key.Q); yield return Keys(1, Key.Space);
            yield return Keys(1.2f, Key.W); yield return Keys(.06f, Key.Q);
            yield return new WaitForSeconds(1.5f);
            Assert.That(body.position.y, Is.GreaterThan(2.5f), "Landed on the mezzanine deck. position=" + body.position);
            Assert.That(body.position.x, Is.LessThan(-13.2f));
            yield return Face(0); Capture("mezzanine");
        }

        [UnityTest] public IEnumerator TimelineGradesLightingAndSetDressing()
        {
            var past = GameObject.Find("Cargo Bay - timeline set dressing").transform.Find("Past only").gameObject;
            var future = past.transform.parent.Find("Future only").gameObject;
            var light = ((Light[])Get(level, "zoneLights"))[0];
            Assert.That(future.activeSelf, Is.True); Assert.That(past.activeSelf, Is.False);
            float dim = light.intensity;
            yield return Swap();
            Assert.That(past.activeSelf, Is.True); Assert.That(future.activeSelf, Is.False);
            Assert.That(light.intensity, Is.GreaterThan(dim * 1.4f), "The Past bay is brighter.");
            Assert.That(past.GetComponentsInChildren<Collider>(true), Is.Empty, "Set dressing cannot trap a player during a time switch.");
            Assert.That(future.GetComponentsInChildren<Collider>(true), Is.Empty);
            yield return Keys(.06f, Key.R); yield return null; yield return null;
            Assert.That(future.activeSelf, Is.True); Assert.That(light.intensity, Is.EqualTo(dim).Within(.01f));
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
