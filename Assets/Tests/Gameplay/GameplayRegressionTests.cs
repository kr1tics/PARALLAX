using System;
using System.Collections;
using System.Collections.Generic;
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
    public class GameplayRegressionTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private MonoBehaviour player, interaction;
        private Rigidbody playerBody;
        private Camera camera;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private readonly List<GameObject> objects = new List<GameObject>();
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Get(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Fields).SetValue(obj, value);
        private static object Property(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static MonoBehaviour Component(GameObject obj, string name) => obj.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == name);
        private static void Call(object obj, string name) => obj.GetType().GetMethod(name).Invoke(obj, null);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1f;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            player = Component(GameObject.Find("Player"), "PlayerController");
            interaction = Component(player.gameObject, "Interact");
            playerBody = player.GetComponent<Rigidbody>(); camera = Camera.main;
            var stateType = player.GetType().Assembly.GetType("GlobalState");
            var state = stateType.GetMethod("get").Invoke(null, null);
            Set(state, "gravity", (float)stateType.GetField("StationGravityStrength").GetRawConstantValue());
            yield return Keys();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (interaction != null) Call(interaction, "DropHeldObject");
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
            objects.Clear();
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.timeScale = 1f;
            yield return null;
        }

        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
        }
        private static IEnumerator Ticks(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }
        private IEnumerator StandOnDeck()
        {
            playerBody.position = new Vector3(1.5f, 0.06f, 9f);
            playerBody.linearVelocity = Vector3.zero;
            var look = Component(camera.gameObject, "Look");
            look.GetType().GetMethod("Rotate").Invoke(look, new object[] { new Vector2(90f, 0f) });
            yield return Ticks(20);
            Assert.That((bool)Get(player, "isInJetpack"), Is.False);
            Assert.That(playerBody.position.y, Is.InRange(-0.1f, 0.2f), "Must stand on the real modular floor.");
        }

        [UnityTest]
        public IEnumerator WalkingStopsOnReleaseAndJumpDoesNotRepeatWhileHeld()
        {
            yield return StandOnDeck();
            Vector3 start = playerBody.position;
            yield return Keys(Key.W); yield return Ticks(15);
            Assert.That(playerBody.position.z - start.z, Is.GreaterThan(1f), "W must walk with Q off.");
            yield return Keys(); yield return Ticks(5);
            Assert.That(new Vector2(playerBody.linearVelocity.x, playerBody.linearVelocity.z).magnitude, Is.LessThan(0.1f));
            yield return Keys(Key.S); yield return Ticks(10);
            Assert.That(playerBody.linearVelocity.z, Is.LessThan(-5f));
            yield return Keys(); yield return Ticks(5);
            yield return Keys(Key.A); yield return Ticks(5); Assert.That(playerBody.linearVelocity.x, Is.LessThan(-5f));
            yield return Keys(Key.D); yield return Ticks(5); Assert.That(playerBody.linearVelocity.x, Is.GreaterThan(5f));
            yield return Keys(); yield return Ticks(5);
            yield return Keys(Key.Space); yield return Ticks(8);
            Assert.That(playerBody.position.y, Is.GreaterThan(0.5f));
            yield return Ticks(100);
            Assert.That(playerBody.position.y, Is.InRange(-0.1f, 0.2f), "Holding Space must not restart a jump.");
            yield return Keys(); yield return Ticks(5);
            yield return Keys(Key.Space); yield return Ticks(8);
            Assert.That(playerBody.position.y, Is.GreaterThan(0.5f), "A new press can jump again.");
        }

        [UnityTest]
        public IEnumerator JetpackTransitionsAndPausePreserveMovementControls()
        {
            yield return StandOnDeck();
            yield return Keys(Key.Q); yield return Keys();
            Assert.That((bool)Get(player, "isInJetpack"), Is.True);
            yield return Keys(Key.Space); yield return Ticks(10);
            Assert.That(playerBody.linearVelocity.y, Is.GreaterThan(0.5f));
            yield return Keys(); yield return Keys(Key.Q); yield return Keys(); yield return Ticks(100);
            Assert.That((bool)Get(player, "isInJetpack"), Is.False);
            yield return Keys(Key.W); yield return Ticks(8); Assert.That(playerBody.linearVelocity.z, Is.GreaterThan(5f));
            GameSettingsMenu.Instance.Open(); Vector3 before = playerBody.position;
            yield return Keys(Key.W, Key.Q, Key.E); yield return null; yield return null;
            Assert.That(playerBody.position, Is.EqualTo(before));
            Assert.That((bool)Get(player, "isInJetpack"), Is.False);
            GameSettingsMenu.Instance.Close(); yield return Keys(); yield return Ticks(5);
            yield return Keys(Key.W); yield return Ticks(8); Assert.That(playerBody.linearVelocity.z, Is.GreaterThan(5f));
            yield return Keys();
            playerBody.position = new Vector3(30f, 2f, 30f); playerBody.linearVelocity = Vector3.zero;
            yield return Keys(Key.Space); yield return Ticks(10);
            Assert.That(playerBody.linearVelocity.y, Is.GreaterThan(0.5f), "Open-space flight works with Q off.");
        }

        [UnityTest]
        public IEnumerator RealKeyboardAndMouseCanCarryWhileWalkingAndReleaseOnce()
        {
            yield return StandOnDeck();
            var item = Cube(camera.transform.position + camera.transform.forward * 2f);
            yield return Keys(Key.E); yield return Keys();
            Assert.That(Property(interaction, "HeldObject"), Is.SameAs(item));
            Vector3 before = item.transform.position;
            yield return Keys(Key.A); yield return Ticks(12); yield return Keys(); yield return Ticks(30);
            Assert.That(item.transform.position.x, Is.LessThan(before.x - 1f), "Carried objects follow walking.");
            Assert.That(item.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.LessThan(10f));
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return null;
            Assert.That(Property(interaction, "HeldObject"), Is.Null, "A click drops exactly once.");
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            playerBody.position = new Vector3(1.5f, 0.06f, 9f); playerBody.linearVelocity = Vector3.zero;
            yield return Ticks(5);
            item.GetComponent<Rigidbody>().position = camera.transform.position + camera.transform.forward * 2f;
            item.GetComponent<Rigidbody>().linearVelocity = Vector3.zero; Physics.SyncTransforms();
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return null;
            Assert.That(Property(interaction, "HeldObject"), Is.SameAs(item), "A new click can pick up again.");
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            yield return Keys(Key.E); Assert.That(Property(interaction, "HeldObject"), Is.Null);
        }

        [UnityTest]
        public IEnumerator TimeSwitchReleasesTheActualEntangledCubeAndRestoresGravity()
        {
            AimForObjectTests();
            var item = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(x => x.GetType().Name == "MovableObject");
            var body = item.GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            body.position = camera.transform.position + camera.transform.forward * 2f;
            body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
            var gravity = Component(item.gameObject, "ArtificialGravity");
            Set(gravity, "effective", true);
            Call(interaction, "TryInteract");
            Assert.That(Property(interaction, "HeldObject"), Is.SameAs(item));
            Assert.That((bool)Get(gravity, "effective"), Is.False);
            var stateType = player.GetType().Assembly.GetType("GlobalState");
            var state = stateType.GetMethod("get").Invoke(null, null);
            stateType.GetMethod("SwapTime").Invoke(state, null);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(item.gameObject.activeInHierarchy, Is.False);
            Assert.That(Property(interaction, "HeldObject"), Is.Null);
            Assert.That((bool)Get(gravity, "effective"), Is.True);
        }

        private void AimForObjectTests()
        {
            player.enabled = false; playerBody.interpolation = RigidbodyInterpolation.None; playerBody.isKinematic = true;
            playerBody.position = new Vector3(30f, 2f, 30f);
            camera.transform.SetParent(null);
            camera.transform.position = new Vector3(30f, 2f, 30f);
            camera.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
        }
        private MonoBehaviour Cube(Vector3 position, bool parented = false)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Grab regression cube";
            objects.Add(cube); cube.layer = 7; cube.transform.position = position;
            cube.transform.localScale = Vector3.one * 0.5f;
            if (parented)
            {
                var parent = new GameObject("Rotated scaled parent"); objects.Add(parent);
                parent.transform.position = new Vector3(25f, 0f, 28f);
                parent.transform.rotation = Quaternion.Euler(0f, 40f, 0f); parent.transform.localScale = Vector3.one * 1.3f;
                cube.transform.SetParent(parent.transform, true);
            }
            var body = cube.AddComponent<Rigidbody>(); body.useGravity = false;
            var movable = (MonoBehaviour)cube.AddComponent(player.GetType().Assembly.GetType("MovableObject"));
            Physics.SyncTransforms(); return movable;
        }

        [UnityTest]
        public IEnumerator ParentScaledObjectFollowsAndDropsEvenWhenLookingAway()
        {
            AimForObjectTests(); var item = Cube(new Vector3(30f, 2f, 32f), true);
            var hit = (Tuple<bool, RaycastHit>)interaction.GetType().GetMethod("fireInteractRay").Invoke(interaction, new object[] {true});
            Assert.That(hit.Item1, Is.True);
            Call(interaction, "TryInteract"); Assert.That(Property(interaction, "HeldObject"), Is.SameAs(item));
            camera.transform.position += Vector3.right * 0.6f;
            yield return Ticks(70);
            Assert.That(item.transform.position.x, Is.EqualTo(30.6f).Within(0.15f));
            Assert.That(item.GetComponent<Rigidbody>().angularVelocity.magnitude, Is.LessThan(0.1f));
            camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Call(interaction, "TryInteract"); Assert.That(Property(interaction, "HeldObject"), Is.Null);
            Assert.That(Physics.GetIgnoreCollision(item.GetComponent<Collider>(), player.GetComponent<Collider>()), Is.False);
            Assert.That(item.GetComponent<Rigidbody>().collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.Discrete));
        }

        [UnityTest]
        public IEnumerator WallsOccludeInteractionAndHeldObjectsKeepWorldCollision()
        {
            AimForObjectTests(); var item = Cube(new Vector3(30f, 2f, 32f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall);
            wall.transform.position = new Vector3(30f, 2f, 31f); wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms(); Call(interaction, "TryInteract");
            Assert.That(Property(interaction, "HeldObject"), Is.Null, "Cannot pick up through a wall.");
            wall.SetActive(false); Physics.SyncTransforms(); Call(interaction, "TryInteract");
            Assert.That(Property(interaction, "HeldObject"), Is.SameAs(item));
            wall.transform.position = new Vector3(30.6f, 2f, 32f); wall.transform.localScale = new Vector3(0.2f, 3f, 3f);
            wall.SetActive(true); Physics.SyncTransforms(); camera.transform.position += Vector3.right * 1.1f;
            yield return Ticks(100);
            Assert.That(item.transform.position.x, Is.LessThan(30.3f), "The held cube must not cross the wall.");
            GameSettingsMenu.Instance.Open(); Vector3 position = item.transform.position;
            Call(interaction, "TryInteract"); yield return null; yield return null;
            Assert.That(Property(interaction, "HeldObject"), Is.SameAs(item));
            Assert.That(item.transform.position, Is.EqualTo(position));
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            item.gameObject.SetActive(false);
            Assert.That(Property(interaction, "HeldObject"), Is.Null, "Time-state deactivation releases a held item.");
            Assert.That(Physics.GetIgnoreCollision(item.GetComponent<Collider>(), player.GetComponent<Collider>()), Is.False);
        }

        [UnityTest]
        public IEnumerator ChildCollidersResolveToParentInteractableAndRepeatedPickupIsReliable()
        {
            AimForObjectTests(); var item = Cube(new Vector3(30f, 2f, 32f));
            Object.Destroy(item.GetComponent<Collider>()); yield return null;
            var child = new GameObject("Child collider"); child.transform.SetParent(item.transform, false);
            child.AddComponent<BoxCollider>(); Physics.SyncTransforms();
            for (int attempt = 0; attempt < 5; attempt++)
            {
                Physics.SyncTransforms();
                Call(interaction, "TryInteract"); Assert.That(Property(interaction, "HeldObject"), Is.SameAs(item));
                Call(interaction, "TryInteract"); Assert.That(Property(interaction, "HeldObject"), Is.Null);
                yield return null;
            }
        }
    }
}
