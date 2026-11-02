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
