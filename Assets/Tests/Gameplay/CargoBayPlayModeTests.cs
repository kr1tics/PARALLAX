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
        Rigidbody Rigid(string name) => (Rigidbody)Get(level, name);
        bool Pressed(string plate) { var button = Get(level, plate); return (bool)button.GetType().GetProperty("IsPressed").GetValue(button); }
        // Drop points just above: A staging, P counterweight, R1 and R2 receiving scales.
        static readonly Vector3 A = new Vector3(-10.5f, -.2f, -3), P = new Vector3(-10.8f, -.2f, 2.4f),
            R1 = new Vector3(13.5f, -.2f, 10), R2 = new Vector3(-14.4f, -.2f, 9.3f), Cradle = new Vector3(-14.6f, 2.46f, 2.6f);
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
            var canvas = GameObject.Find("Cargo Bay Objective UI").GetComponent<Canvas>();
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
            // Stage B. The jetpack climb to the mezzanine has its own input test; here the Past crates are placed directly.
            var c1Past = Rigid("pastCrate"); var c2Past = Rigid("rackPast");
            Assert.That(Rigid("rackFuture").isKinematic, Is.True, "The 2176 rack clamp has seized.");
            yield return Swap();
            Assert.That(c2Past.isKinematic, Is.True, "An empty counterweight pad keeps C2 clamped.");
            Place(c1Past, P); yield return new WaitForSeconds(1);
            Assert.That(c2Past.isKinematic, Is.False, "Past C1 on the counterweight opens the clamp.");
            Place(c2Past, R2); yield return new WaitForSeconds(1);
            Assert.That(Flag("RackReleased"), Is.True);
            yield return Swap(); yield return new WaitForSeconds(.6f);
            Assert.That(Pressed("rackPlate"), Is.True, "Moving C2 in 2026 loads the booth scale that is sealed in 2176.");
            Assert.That(Flag("CargoDelivered"), Is.False, "C2 alone cannot satisfy receiving.");
            // Stage C: the Future C1 followed its Past copy onto P; carry it to R1 with normal controls.
            yield return WalkTo(-8.8f, 2.4f);
            yield return Face(180, -.07f); yield return Keys(.06f, Key.E);
            Assert.That(Held, Is.Not.Null, "Future C1 now rests on the counterweight pad.");
            // Wide southern detour: a deliberate alternative to the shorter northern freight aisle.
            yield return WalkTo(-8.2f, -11); yield return WalkTo(10.5f, -11);
            yield return WalkTo(10.5f, 10); yield return WalkTo(11.5f, 10);
            yield return Face(0, -.09f); yield return new WaitForSeconds(.8f);
            yield return Keys(.06f, Key.E); yield return new WaitForSeconds(1);
            Assert.That(Flag("CargoDelivered"), Is.True, "Receiving requires both Future manifests on their scales together.");
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
            Rigid("rackPast").position = new Vector3(5, -30, 0); Rigid("rackFuture").position = new Vector3(5, -40, 0);
            yield return Keys(.06f, Key.Q);
            yield return new WaitForSeconds(1.05f); yield return Keys(.06f, Key.T);
            yield return Keys(.06f, Key.R); yield return new WaitForSeconds(.8f);
            Assert.That(Timeline, Is.EqualTo("Future")); Assert.That(Flag("CargoDelivered"), Is.False);
            Assert.That(Vector3.Distance(body.position, new Vector3(0, 0, -15)), Is.LessThan(.2f));
            Assert.That(Vector3.Distance(future.position, new Vector3(-14, -.55f, -11)), Is.LessThan(.2f));
            Assert.That((bool)Get(player, "isInJetpack"), Is.False);
            Assert.That(Held, Is.Null);
            Assert.That(Vector3.Distance(Rigid("rackFuture").position, Cradle), Is.LessThan(.2f));
            Assert.That(Rigid("rackFuture").isKinematic, Is.True);
            Assert.That(((GameObject)Get(level, "futureDebris")).activeSelf, Is.True, "Reset returns to 2176 with the booth sealed.");
            yield return Swap();
            Assert.That(Vector3.Distance(past.position, new Vector3(-10.5f, -.55f, -9)), Is.LessThan(.2f), "Reset must rebase causality as well as restore transforms.");
            Assert.That(Vector3.Distance(Rigid("rackPast").position, Cradle), Is.LessThan(.2f), "Reset restores C2 to the rack.");
            Assert.That(Rigid("rackPast").isKinematic, Is.True, "Reset re-clamps C2.");
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

        // Stage C: replaces the single-crate "Past anchor" rule with two manifests that must be loaded together in 2176.
        [UnityTest] public IEnumerator ReceivingCannotSkipStagingOrCompleteWithOneManifest()
        {
            var past = Rigid("pastCrate"); var future = Rigid("futureCrate"); var c2Past = Rigid("rackPast");
            Place(future, R1); yield return new WaitForSeconds(1);
            Assert.That(Flag("CargoDelivered"), Is.False, "Delivering straight to R1 cannot skip A.");
            yield return Swap(); Place(past, A); yield return new WaitForSeconds(1);
            yield return Swap(); yield return new WaitForSeconds(.5f);
            Assert.That(Flag("FreightAccessGranted"), Is.True);
            Place(future, R2); yield return new WaitForSeconds(1);
            Assert.That(Pressed("rackPlate"), Is.False, "R2 only weighs manifest C2.");
            Place(future, R1); yield return new WaitForSeconds(1);
            Assert.That(Pressed("plate"), Is.True);
            Assert.That(Flag("CargoDelivered"), Is.False, "C1 alone cannot open maintenance access.");
            // The booth is sealed to walking and flying in 2176, open in 2026.
            for (int epoch = 0; epoch < 2; epoch++)
            {
                foreach (float y in new[] { -.6f, .4f, 1.4f, 2.1f })
                for (float z = 7.9f; z <= 9.8f; z += .3f)
                    Assert.That(Physics.Raycast(new Vector3(-10.8f, y, z), Vector3.left, 2, ~0, QueryTriggerInteraction.Ignore), Is.EqualTo(epoch == 0),
                        (epoch == 0 ? "2176 booth gap at " : "2026 booth doorway blocked at ") + y + "," + z);
                Assert.That(Physics.Raycast(new Vector3(-15, 4.5f, 10), Vector3.down, 3, ~0, QueryTriggerInteraction.Ignore), Is.True, "Booth roof.");
                if (epoch == 0) yield return Swap();
            }
            // Ordering matters: using Past C1 as the counterweight pulls its Future copy off R1.
            Place(past, P); yield return new WaitForSeconds(1);
            Place(c2Past, R2); yield return new WaitForSeconds(1);
            yield return Swap(); yield return new WaitForSeconds(.5f);
            Assert.That(Pressed("rackPlate"), Is.True);
            Assert.That(Pressed("plate"), Is.False, "Relocating Past C1 moved its Future copy off R1.");
            Assert.That(Flag("CargoDelivered"), Is.False);
            Assert.That(Vector3.Distance(future.position, new Vector3(P.x, -.3f, P.z)), Is.LessThan(.3f));
            // Weight scales ignore weightless cargo; recovery is to put C1 back once gravity holds it.
            var state = State; var gravity = state.GetType().GetField("gravity");
            gravity.SetValue(state, 0f);
            Place(future, R1); yield return new WaitForSeconds(1);
            Assert.That(Flag("CargoDelivered"), Is.False, "Floating cargo cannot press the scales.");
            gravity.SetValue(state, 14.715f); yield return new WaitForSeconds(1.2f);
            Assert.That(Flag("CargoDelivered"), Is.True);
            Assert.That(((Component)Get(level, "exitDoor")).GetComponent<Collider>().enabled, Is.False);
            yield return Swap(); yield return Swap();
            Assert.That(Vector3.Distance(future.position, new Vector3(R1.x, -.3f, R1.z)), Is.LessThan(.2f), "Unchanged Past must preserve the independent Future delivery.");
            // Delivery latches: moving C2 out of the booth afterwards cannot re-lock the exit.
            yield return Swap(); Place(c2Past, new Vector3(-9, -.2f, 10)); yield return new WaitForSeconds(1);
            body.position = new Vector3(10.5f, 0, 16); yield return new WaitForSeconds(.3f);
            Assert.That(Flag("Completed"), Is.False, "Maintenance access only completes in 2176.");
            yield return Swap(); yield return new WaitForSeconds(.3f);
            Assert.That(Pressed("rackPlate"), Is.False);
            Assert.That(Flag("CargoDelivered"), Is.True);
            Assert.That(Flag("Completed"), Is.True);
            yield return Keys(.06f, Key.R); yield return new WaitForSeconds(.2f);
            Assert.That(Flag("FreightAccessGranted"), Is.False); Assert.That(Flag("CargoDelivered"), Is.False);
            Assert.That(Flag("Completed"), Is.False); Assert.That(Flag("RackReleased"), Is.False);
            foreach (string gate in new[] { "freightDoor", "exitDoor" })
                Assert.That(((Component)Get(level, gate)).GetComponent<Collider>().enabled, Is.True);
        }

        // Stage B: Past counterweight, re-clamping, the seized 2176 clamp, propagation and repeated switching.
        [UnityTest] public IEnumerator ArchiveRackClampNeedsPastCounterweightAndSeizesInFuture()
        {
            var c1Past = Rigid("pastCrate"); var c1Future = Rigid("futureCrate");
            var c2Past = Rigid("rackPast"); var c2Future = Rigid("rackFuture");
            Place(c1Future, P); yield return new WaitForSeconds(1);
            Assert.That(Pressed("counterweight"), Is.False, "Only Past C1 weights the counterweight.");
            Assert.That(c2Future.isKinematic, Is.True, "The 2176 clamp stays seized.");
            yield return Swap();
            Assert.That(c2Past.isKinematic, Is.True);
            Place(c1Past, P); yield return new WaitForSeconds(1);
            Assert.That(Pressed("counterweight"), Is.True); Assert.That(c2Past.isKinematic, Is.False);
            Vector3 aside = new Vector3(-8, -.2f, 0);
            Place(c1Past, aside); yield return new WaitForSeconds(1);
            Assert.That(c2Past.isKinematic, Is.True, "Lifting the counterweight re-closes the clamp on a crate still in the cradle.");
            Assert.That(Flag("RackReleased"), Is.False);
            Place(c1Past, P); yield return new WaitForSeconds(1);
            Place(c2Past, new Vector3(-11.5f, -.2f, -1)); yield return new WaitForSeconds(1);
            Place(c1Past, aside); yield return new WaitForSeconds(1);
            Assert.That(c2Past.isKinematic, Is.False, "Only the cradle clamps.");
            Assert.That(Flag("RackReleased"), Is.True);
            Vector3 settled = c2Past.position;
            yield return Swap();
            Assert.That(Vector3.Distance(c2Future.position, settled), Is.LessThan(.2f), "Future C2 follows the moved Past crate.");
            Assert.That(c2Future.isKinematic, Is.False, "Out of the seized cradle, Future C2 can be carried.");
            Assert.That(Vector3.Distance(c1Future.position, new Vector3(aside.x, -.55f, aside.z)), Is.LessThan(.3f));
            for (int i = 0; i < 6; i++) yield return Swap();
            Assert.That(Timeline, Is.EqualTo("Future"));
            // The inactive timeline's body reports Rigidbody.position as zero; its Transform is authoritative.
            Assert.That(Vector3.Distance(c2Future.position, settled), Is.LessThan(.2f), "Repeated switching cannot drift entangled cargo.");
            Assert.That(Vector3.Distance(c2Past.transform.position, settled), Is.LessThan(.2f));
            Assert.That(c2Future.isKinematic || c2Past.isKinematic, Is.False);
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
            // Phase 3: the booth doorway is open in 2026 (see the receiving test) and collapsed shut in 2176, where this test starts.
            Assert.That(Physics.Raycast(new Vector3(-11, 0, 8.8f), Vector3.left, 4, ~0, QueryTriggerInteraction.Ignore), Is.True, "Future booth collapse.");
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

        // Phase 5: the 2176 girder appears in the booth doorway. Measured: PhysX depenetration moves a player or crate standing
        // there out slowly (no extra code needed); this guards that nothing is trapped or launched.
        [UnityTest] public IEnumerator TimeSwitchInBoothDoorwayNeitherTrapsNorLaunches()
        {
            var c2Past = Rigid("rackPast"); var c2Future = Rigid("rackFuture");
            yield return Swap();
            Place(Rigid("pastCrate"), P); yield return new WaitForSeconds(1);
            Place(c2Past, new Vector3(-12, -.2f, 9.4f)); yield return new WaitForSeconds(1);
            body.position = new Vector3(-12, .02f, 8.2f); yield return new WaitForSeconds(.4f);
            Assert.That(body.position.x, Is.InRange(-12.4f, -11.6f), "Setup: the player stands in the doorway.");
            yield return new WaitForSeconds(1.05f); yield return Keys(.06f, Key.T);
            float peakPlayer = 0, peakCrate = 0;
            for (float end = Time.time + 1.2f; Time.time < end; )
            {
                yield return new WaitForFixedUpdate();
                peakPlayer = Mathf.Max(peakPlayer, body.linearVelocity.magnitude);
                if (Timeline == "Future") peakCrate = Mathf.Max(peakCrate, c2Future.linearVelocity.magnitude);
            }
            Assert.That(Timeline, Is.EqualTo("Future"));
            Assert.That(peakPlayer, Is.LessThan(3), "Player ejected at speed.");
            Assert.That(peakCrate, Is.LessThan(3), "Future C2 ejected at speed.");
            var girder = ((GameObject)Get(level, "futureDebris")).GetComponentInChildren<Collider>().bounds;
            foreach (var (name, position) in new[] { ("player", body.position), ("Future C2", c2Future.position) })
                Assert.That(position.x < girder.min.x - .3f || position.x > girder.max.x + .3f, Is.True, name + " left inside the girder at " + position);
            Assert.That(body.position.y, Is.InRange(-.3f, .5f)); Assert.That(body.linearVelocity.magnitude, Is.LessThan(2), "Not ejected at speed.");
            Assert.That(c2Future.position.y, Is.InRange(-.7f, -.2f));
            Assert.That(Physics.Raycast(new Vector3(-10.8f, .4f, 8.8f), Vector3.left, 2, ~0, QueryTriggerInteraction.Ignore), Is.True, "Booth still sealed.");
        }

        [UnityTest] public IEnumerator LostCargoReturnsAloneWithoutResettingProgress()
        {
            var c1Past = Rigid("pastCrate"); var c1Future = Rigid("futureCrate");
            yield return Swap(); Place(c1Past, A); yield return new WaitForSeconds(1);
            yield return Swap(); yield return new WaitForSeconds(.5f);
            Assert.That(Flag("FreightAccessGranted"), Is.True);
            Vector3 standing = body.position;
            Place(c1Future, new Vector3(0, -30, 0)); yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(c1Future.position, new Vector3(-14, -.55f, -11)), Is.LessThan(.3f), "Future C1 returns to its start.");
            Assert.That(Flag("FreightAccessGranted"), Is.True, "Recovering one crate keeps progress.");
            Assert.That(Vector3.Distance(body.position, standing), Is.LessThan(.5f), "The player is not reset.");
            yield return Swap(); Place(c1Past, new Vector3(30, 0, 0)); yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(c1Past.position, new Vector3(-10.5f, -.55f, -9)), Is.LessThan(.3f), "Past C1 returns to its start.");
            yield return Swap();
            Assert.That(Vector3.Distance(c1Future.position, new Vector3(-10.5f, -.55f, -9)), Is.LessThan(.3f), "Its Future copy follows as for any Past move.");
            Assert.That(Flag("FreightAccessGranted"), Is.True);
        }

        // Stage B with real input: jetpack to the mezzanine, lift Past C2 out of the open clamp and set it down.
        [UnityTest] public IEnumerator PastC2CanBeLiftedFromTheMezzanineWithNormalControls()
        {
            var c2Past = Rigid("rackPast");
            yield return Swap(); Place(Rigid("pastCrate"), P); yield return new WaitForSeconds(1);
            yield return WalkTo(-8.2f, -11); yield return WalkTo(-8.2f, -1.2f); yield return WalkTo(-11.4f, -1.2f);
            yield return Face(180);
            yield return Keys(.06f, Key.Q); yield return Keys(1, Key.Space);
            yield return Keys(1.2f, Key.W); yield return Keys(.06f, Key.Q);
            yield return new WaitForSeconds(1.5f);
            Assert.That(body.position.y, Is.GreaterThan(2.5f), "On the mezzanine. position=" + body.position);
            yield return WalkTo(-14.6f, .6f); yield return Face(90, -.07f);
            Assert.That(c2Past.isKinematic, Is.False, "Counterweight holds the clamp open.");
            yield return Keys(.06f, Key.E);
            Assert.That(Held, Is.Not.Null, "Past C2 can be carried.");
            yield return Keys(.8f, Key.S); yield return new WaitForSeconds(.4f);
            yield return Keys(.06f, Key.E); yield return new WaitForSeconds(1);
            Assert.That(Held, Is.Null);
            Assert.That(Flag("RackReleased"), Is.True, "C2 is out of the cradle. position=" + c2Past.position);
            Assert.That(c2Past.position.y, Is.GreaterThan(1.9f), "Set down on the deck, still reachable.");
        }

        // Phase 4: Cargo Bay uses the tutorial / Power Restoration GameplayHud layout, fonts, wording and visibility rules.
        [UnityTest] public IEnumerator HudMatchesSharedLayoutAndReportsStageState()
        {
            Assert.That(GameObject.Find("Cargo HUD"), Is.Null, "The bespoke 1280x720 HUD is gone.");
            var ui = GameObject.Find("Cargo Bay Objective UI");
            var scaler = ui.GetComponent("CanvasScaler");
            Assert.That((Vector2)scaler.GetType().GetProperty("referenceResolution").GetValue(scaler), Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(((RectTransform)ui.transform.Find("Objective panel")).sizeDelta, Is.EqualTo(new Vector2(470, 148)));
            TMPro.TMP_Text Text(string name) => ui.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.name == name);
            Assert.That(Text("Objective number").text, Is.EqualTo("OBJECTIVE 01"));
            Assert.That(Text("Objective title").text, Is.EqualTo("Open Freight Access"));
            Assert.That(Text("Objective title").font.name, Does.StartWith("Oxanium"));
            Assert.That(Text("Current instruction").font.name, Does.StartWith("SpaceMono"));
            Assert.That(Text("Timeline status").text, Is.EqualTo("2176 / FUTURE  ·  GRAVITY ON"));
            Assert.That(Text("Contextual control hints").text, Does.Contain("R — Reset cargo"));
            Assert.That((bool)Get(player, "allowManualDistortion"), Is.False, "No distortable objects here.");
            Assert.That((bool)Get(GameSettingsMenu.Instance, "manualDistortionControl"), Is.False);
            Assert.That((bool)Get(GameSettingsMenu.Instance, "checkpointControl"), Is.True, "R resets the bay, so its row stays listed.");
            yield return Keys(.06f, Key.Q); yield return null;
            Assert.That(Text("Timeline status").text, Does.EndWith("GRAVITY OFF"));
            Assert.That(Text("Contextual control hints").text, Does.Contain("Q — Toggle jetpack"));
            yield return Keys(.06f, Key.Q); yield return new WaitForSeconds(.5f);
            yield return Swap(); Place(Rigid("pastCrate"), A); yield return new WaitForSeconds(1);
            yield return Swap(); yield return new WaitForSeconds(.5f);
            Assert.That(Text("Objective number").text, Is.EqualTo("OBJECTIVE 02"));
            Assert.That(Text("Current instruction").text, Does.Contain("seized"));
            yield return WalkTo(-8.2f, -11); yield return WalkTo(-10.5f, -5); yield return Face(90, -.07f);
            Assert.That(Text("Contextual interaction prompt").text, Is.EqualTo("E — Pick up"));
            Capture("hud-objective-02");
            GameSettingsMenu.StartupInputBlocked = true; yield return null;
            Assert.That(ui.GetComponent<CanvasGroup>().alpha, Is.Zero, "HUD hides while gameplay input is blocked.");
            GameSettingsMenu.StartupInputBlocked = false; yield return null;
            Assert.That(ui.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
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
