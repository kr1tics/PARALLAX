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
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    // Power Restoration's environment pass: timeline dressing, the gantry under B and the routes the puzzle walks.
    public class PowerRestorationEnvironmentTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Rigidbody player;
        MonoBehaviour look;
        object state;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static MonoBehaviour Component(GameObject go, string name) => go.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == name);
        static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        string Now => state.GetType().GetField("now").GetValue(state).ToString();
        GameObject Environment => GameObject.Find("10 / Environment");

        [UnitySetUp] public IEnumerator Setup()
        {
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            GameSettingsMenu.StartupInputBlocked = false; Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null;
            player = GameObject.Find("Player").GetComponent<Rigidbody>();
            look = Component(Camera.main.gameObject, "Look");
            state = player.GetComponents<MonoBehaviour>().First().GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            yield return new WaitForSeconds(.3f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false;
            yield return null;
        }

        IEnumerator Swap(string expected)
        {
            yield return new WaitForSeconds(1.05f);
            Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Assert.That(Now, Is.EqualTo(expected));
        }
        void Face(float yaw, float pitch = 0) => Call(look, "Rotate", new Vector2(yaw, pitch));
        void Place(Vector3 position) { player.position = position; player.transform.position = position; player.linearVelocity = Vector3.zero; Physics.SyncTransforms(); }

        IEnumerator WalkTo(float x, float z)
        {
            var delta = new Vector3(x, 0, z) - player.position;
            Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            float deadline = Time.time + 6;
            while (Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z)) > .35f && Time.time < deadline)
                yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z)), Is.LessThan(.8f),
                "Walking route blocked at " + player.position + " towards " + x + "," + z);
        }

        void Capture(string name)
        {
            Directory.CreateDirectory(".utmp/level1");
            var camera = Camera.main; var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target; camera.Render();
            RenderTexture.active = target; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/level1/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            Object.Destroy(image); Object.Destroy(target);
        }

        [UnityTest] public IEnumerator TimelineDressingIsColliderFreeAndFollowsTheTimeline()
        {
            var past = Environment.transform.Find("2026 only").gameObject; var future = Environment.transform.Find("2176 only").gameObject;
            Assert.That(past.GetComponentsInChildren<Collider>(true), Is.Empty, "Dressing can never trap a player during a switch.");
            Assert.That(future.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(GameObject.Find("2176 truss collapse").GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(future.activeSelf, Is.True); Assert.That(past.activeSelf, Is.False);
            yield return Swap("Past");
            Assert.That(past.activeSelf, Is.True); Assert.That(future.activeSelf, Is.False);
            Assert.That(GameObject.Find("Route 1 roof truss (intact)"), Is.Not.Null, "In 2026 the truss over route 1 is still up.");
            Assert.That(GameObject.Find("2176 truss collapse"), Is.Null);
            var wall = Environment.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Wall_Simple_01"));
            Assert.That(wall.sharedMaterial.name, Does.Not.Contain("FutureDecay"));
            yield return Swap("Future");
            Assert.That(wall.sharedMaterial.name, Does.Contain("FutureDecay"), "Kit cladding weathers in 2176.");
        }

        [UnityTest] public IEnumerator GantryHoldsThePlayerWithinReachOfB()
        {
            var moduleA = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
            moduleA.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(moduleA, true); // A's path has its own tests.
            yield return Swap("Past");
            Place(new Vector3(7.4f, 11.2f, -.5f)); Face(62);
            yield return new WaitForSeconds(1);
            Assert.That(player.position.y, Is.InRange(10.8f, 11.4f), "Standing on the gantry deck under station gravity.");
            var b = Component(GameObject.Find("Repair panel"), "PowerModuleB");
            Assert.That((bool)Prop(b, "CanRepair"), Is.True, "B is in reach from the gantry.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((bool)Prop(b, "IsRepaired"), Is.True);
        }

        [UnityTest] public IEnumerator MainRoutesStayWalkableInBothTimelines()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Place(new Vector3(0, 1.05f, -8.2f)); yield return new WaitForSeconds(.3f);
                yield return WalkTo(-4.5f, -6); yield return WalkTo(-5.8f, -2.8f); // Maintenance bay, behind module A.
                yield return WalkTo(-6.6f, -2); // Gravity console approach.
                yield return WalkTo(-3, -4); yield return WalkTo(6.5f, -4); yield return WalkTo(7, 1); // Junction approach.
                yield return WalkTo(4.5f, 0); yield return WalkTo(3.5f, 1.2f); // Reactor console.
                if (pass == 0) yield return Swap("Past");
            }
        }

        IEnumerator Press(Key key) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; }
        IEnumerator Hold(Key key, System.Func<bool> done, float seconds)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            for (float end = Time.time + seconds; Time.time < end && !done();) yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.4f);
        }
        // Look at a point the way a mouse would: yaw from the horizontal offset, pitch from the eye height.
        void LookAt(Vector3 point)
        {
            var eye = Camera.main.transform.position; var d = point - eye;
            float flat = new Vector2(d.x, d.z).magnitude;
            Face(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, .25f * d.y / Mathf.Max(.01f, flat));
        }
        // Zero-G legs: the jetpack keeps momentum, so release W early and let its damping bring the player to rest.
        IEnumerator FlyTo(float x, float z)
        {
            float Gap() => Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z));
            for (int attempt = 0; attempt < 4 && Gap() > .6f; attempt++)
            {
                var delta = new Vector3(x, 0, z) - player.position;
                Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg); yield return null;
                // Measured: the jetpack coasts about 0.6 s worth of speed after W is released.
                yield return Hold(Key.W, () => Gap() < .3f + player.linearVelocity.magnitude * .6f, 8);
                yield return new WaitForSeconds(1.5f);
            }
            Assert.That(Gap(), Is.LessThan(.8f), "Flight route blocked at " + player.position + " towards " + x + "," + z);
        }
        float StationGravity => (float)state.GetType().GetField("gravity").GetValue(state);
        object Prop(string objectName, string component, string property) => Prop(Component(GameObject.Find(objectName), component), property);

        // The whole level with normal controls: keyboard for every action, the camera aimed as a mouse would.
        [UnityTest] public IEnumerator FullPowerRestorationWithNormalControls()
        {
            var moduleA = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
            var interact = Component(player.gameObject, "Interact");
            Component(GameObject.Find("08 / Scene presentation"), "TutorialCompletionScreen").enabled = true;
            yield return Press(Key.F); yield return new WaitForSeconds(.8f);
            Assert.That(Now, Is.EqualTo("Past"));
            // Gravity console: cut station gravity so the 380 kg module can be lifted.
            yield return WalkTo(-6.6f, -2);
            LookAt(GameObject.Find("Gravity switch").transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That(StationGravity, Is.LessThan(.1f), "Console pressed with E.");
            LookAt(moduleA.transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That(Prop(interact, "HeldObject"), Is.SameAs(moduleA), "Module A lifted in zero-G.");
            // Fly (W) to stand in front of the socket, face it, release into the guide.
            yield return FlyTo(-6, .9f);
            Face(90, -.03f); yield return new WaitForSeconds(.8f);
            Assert.That((bool)Prop(moduleA, "CanInstall"), Is.True, "Inside the forgiving guide. module=" + moduleA.transform.position);
            yield return Press(Key.E); yield return new WaitForSeconds(.5f);
            Assert.That((bool)Prop(moduleA, "IsRepaired"), Is.True);
            // Fly to the climb column, ascend with Space, repair B.
            yield return FlyTo(6.5f, 1.4f);
            yield return Hold(Key.Space, () => player.position.y + player.linearVelocity.y * .6f > 11.6f, 6);
            yield return new WaitForSeconds(1);
            var panel = GameObject.Find("Repair panel");
            LookAt(panel.transform.position); yield return null;
            Assert.That((bool)Prop("Repair panel", "PowerModuleB", "CanRepair"), Is.True, "At B. player=" + player.position);
            yield return Press(Key.E);
            Assert.That((bool)Prop("Repair panel", "PowerModuleB", "IsRepaired"), Is.True);
            // Descend with Ctrl, switch B's feed to the route that survives.
            yield return Hold(Key.LeftCtrl, () => player.position.y + player.linearVelocity.y * .6f < 1.4f, 8);
            yield return new WaitForSeconds(1);
            yield return FlyTo(7, 1);
            LookAt(GameObject.Find("B feed junction").transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That((int)Prop("09 / Power path", "PowerCircuit", "Route"), Is.EqualTo(2));
            // Back to the console: restore gravity for the control rods.
            yield return FlyTo(-6.6f, -2);
            LookAt(GameObject.Find("Gravity switch").transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That(StationGravity, Is.GreaterThan(4.5f));
            yield return new WaitForSeconds(1);
            // 2176: start the reactor at its console.
            yield return Press(Key.F); yield return new WaitForSeconds(.8f);
            Assert.That(Now, Is.EqualTo("Future"));
            Assert.That((bool)Prop("08 / Scene presentation", "PowerRestorationPresentation", "ReactorReady"), Is.True);
            yield return WalkTo(3.5f, 1.2f);
            LookAt(GameObject.Find("Console housing").transform.position); yield return null;
            yield return Press(Key.E);
            var reactor = Component(GameObject.Find("Console housing"), "PowerReactor");
            Assert.That((bool)Prop(reactor, "IsOnline"), Is.True);
            var completion = Component(GameObject.Find("08 / Scene presentation"), "TutorialCompletionScreen");
            Assert.That((bool)Prop(completion, "IsOpen"), Is.False, "The start-up plays before the completion screen.");
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That((bool)Prop(completion, "IsOpen"), Is.True); Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(2.6f); // Its own one-second delay and fade.
            Assert.That((bool)Prop(completion, "IsReady"), Is.True);
            Assert.That(GameObject.Find("Power Restoration Completion Screen"), Is.Not.Null);
        }

        // Visual review stills; not assertions.
        [UnityTest] public IEnumerator CaptureReviewViews()
        {
            var views = new[] { ("spawn", new Vector3(0, 1.05f, -8.2f), 90f, -.02f), ("bay", new Vector3(-2, 1.05f, -3), 140f, -.06f),
                ("distribution", new Vector3(1, 1.05f, -3), 30f, .22f), ("reactor", new Vector3(-3, 1.05f, -2), 60f, .1f) };
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var (name, position, yaw, pitch) in views)
                {
                    Place(position); Face(yaw, pitch); yield return new WaitForSeconds(.2f);
                    Capture((pass == 0 ? "2176-" : "2026-") + name);
                }
                if (pass == 0) yield return Swap("Past");
            }
        }
    }
}
