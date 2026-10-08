using System;
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
    public class Level1PlayModeTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MonoBehaviour level, player, interaction;
        object state;
        Rigidbody playerBody;
        static object Get(object obj, string field) => obj.GetType().GetField(field, Fields).GetValue(obj);
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
        static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        static MonoBehaviour Component(GameObject obj, string type) => obj.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == type);
        bool Flag(string name) => (bool)Prop(level, name);
        Rigidbody[] Cubes(string name) => (Rigidbody[])Get(level, name);
        Collider Door(string name) => ((Component)Get(level, name)).GetComponent<Collider>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            level = Component(GameObject.Find("Level 1 - Engineering Access"), "Level1Controller");
            player = Component(GameObject.Find("Player"), "PlayerController");
            interaction = Component(player.gameObject, "Interact");
            playerBody = player.GetComponent<Rigidbody>();
            state = player.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            yield return Ticks(20);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (interaction != null) Call(interaction, "DropHeldObject");
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            Time.timeScale = 1; yield return null;
        }

        static IEnumerator Ticks(int count) { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }
        IEnumerator Swap()
        {
            yield return new WaitForSeconds(1.05f);
            Call(state, "SwapTime"); yield return new WaitForSeconds(0.65f);
        }
        void Place(Rigidbody body, Vector3 position)
        {
            body.interpolation = RigidbodyInterpolation.None;
            body.position = position; body.transform.position = position;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.WakeUp(); Physics.SyncTransforms();
        }
        void Aim(Vector3 position, Vector3 target)
        {
            player.enabled = false; playerBody.isKinematic = true;
            playerBody.interpolation = RigidbodyInterpolation.None;
            playerBody.position = position;
            player.transform.position = position;
            Camera.main.transform.position = position + Vector3.up * 0.3f;
            Camera.main.transform.LookAt(target);
            Physics.SyncTransforms();
        }
        void Snapshot(string name)
        {
            Directory.CreateDirectory(".utmp/level1");
            var camera = Camera.main; var previousTarget = camera.targetTexture;
            var rt = new RenderTexture(1280, 720, 24); camera.targetTexture = rt; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/level1/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = previousTarget; RenderTexture.active = previous;
            Object.Destroy(image); Object.Destroy(rt);
        }
        IEnumerator SolveIntro()
        {
            yield return Swap(); Assert.That(Get(state, "now").ToString(), Is.EqualTo("Past"));
            Place(Cubes("introCubes")[0], new Vector3(2.5f, 0.1f, 13.45f));
            yield return Ticks(60);
            Assert.That(Flag("IntroSolved"), Is.False, "The past plate cannot power the future relay.");
            yield return Swap(); yield return Ticks(60);
            Assert.That(Flag("IntroSolved"), Is.True, "Real cube contact after causality must open access.");
            Assert.That(Door("introDoor").enabled, Is.False);
        }
        IEnumerator ReleaseStorage()
        {
            yield return Swap();
            Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
            Call(interaction, "TryInteract"); yield return null;
            Assert.That((float)Get(state, "gravity"), Is.Zero, "The console must work through the actual 2.5 m interaction ray.");
            Assert.That(Flag("StorageReleased"), Is.True);
            Assert.That(Door("storageDoor").enabled, Is.False);
            Snapshot("engineering-console");
        }
        IEnumerator SolveMain()
        {
            yield return ReleaseStorage();
            Place(Cubes("mainCubes")[0], new Vector3(-5.5f, -0.25f, 5));
            yield return Ticks(15);
            Assert.That(Flag("MainSolved"), Is.False);
            Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
            var hit = (Tuple<bool, RaycastHit>)Call(interaction, "fireInteractRay", true);
            Assert.That(hit.Item1, Is.True, "Gravity console must remain reachable for restoring gravity.");
            Assert.That(hit.Item2.collider.GetComponent("Switch"), Is.Not.Null);
            Call(interaction, "TryInteract"); yield return Ticks(60);
            Assert.That((float)Get(state, "gravity"), Is.GreaterThan(4.5f));
            Assert.That(Flag("StorageReleased"), Is.False, "Restoring gravity must re-engage an empty hatch.");
            Assert.That(Flag("StorageAccessGranted"), Is.True, "Closing storage must preserve the puzzle prerequisite.");
            yield return Swap(); yield return Ticks(60);
            Assert.That(Flag("MainSolved"), Is.True);
            Assert.That(Door("exitDoor").enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator SpawnDecksSealedStoreAndLockedRoutesAreValid()
        {
            Assert.That(playerBody.position.x, Is.EqualTo(3).Within(0.1f));
            Assert.That(playerBody.position.z, Is.EqualTo(9).Within(0.1f));
            Assert.That(playerBody.position.y, Is.InRange(-0.1f, 0.2f));
            Assert.That((float)Get(interaction, "reachDistance"), Is.EqualTo(2.5f));
            var environment = GameObject.Find("Environment");
            Assert.That(environment.transform.Cast<Transform>().Count(t => t.name.StartsWith("Room_")), Is.EqualTo(7));
            var gravityType = player.GetType().Assembly.GetType("StationGravity");
            foreach (var p in new[] { new Vector3(3, 0, 9), new Vector3(2.5f, -0.2f, 13.45f), new Vector3(-6, 0, 15), new Vector3(-5.5f, 0, 5), new Vector3(-13, 0, 4.45f) })
                Assert.That((bool)gravityType.GetMethod("Contains").Invoke(null, new object[] { p }), Is.True, "Missing deck volume at " + p);
            foreach (float x in new[] { 0f, -9f })
                for (float z = 0.5f; z < 17.5f; z += 0.5f)
                    foreach (float y in new[] { -0.5f, 0.5f, 2.4f })
                        Assert.That(Physics.Raycast(new Vector3(x + 0.6f, y, z), Vector3.left, 1.2f, ~0, QueryTriggerInteraction.Ignore), Is.True, $"Bulkhead gap at {x},{y},{z}");
            Assert.That(Physics.Raycast(new Vector3(3, 1, 9), Vector3.up, 2), Is.True, "Jetpack roof bypass must be sealed.");
            var future = Cubes("introCubes")[1];
            yield return Ticks(100);
            Assert.That(future.position.x, Is.EqualTo(4.65f).Within(0.1f), "Storage collider must not eject its cube.");
            Aim(new Vector3(4.65f, 0, 13.9f), future.position);
            Call(interaction, "TryInteract");
            Assert.That(Prop(interaction, "HeldObject"), Is.Null, "Cannot grab the future cube through glass.");
            Snapshot("sealed-store");
            yield return Swap();
            Set(Component(player.gameObject, "ArtificialGravity"), "effective", false);
            yield return null;
            Assert.That(Flag("StorageReleased"), Is.False, "Personal jetpack cannot release storage.");
            Assert.That(Door("introDoor").enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator BothPhysicsPuzzlesAndPlayerExitCompleteTheLevel()
        {
            Snapshot("arrival");
            yield return SolveIntro();
            Assert.That(Physics.SphereCast(new Vector3(1, 0, 13.45f), 0.3f, Vector3.left, out _, 2, ~0, QueryTriggerInteraction.Ignore), Is.False, "Access doorway must be traversable.");
            yield return SolveMain();
            Assert.That(Physics.SphereCast(new Vector3(-8, 0, 4.45f), 0.3f, Vector3.left, out _, 2, ~0, QueryTriggerInteraction.Ignore), Is.False, "Exit doorway must be traversable.");
            Place(Cubes("mainCubes")[1], new Vector3(-13.3f, 0, 4.45f));
            yield return Ticks(10); Assert.That(Flag("Completed"), Is.False, "Only the player completes the level.");
            Assert.That(Door("exitDoor").enabled, Is.False, "Solved gates remain open when a cube moves.");
            Aim(new Vector3(-11.5f, 0, 4.45f), new Vector3(-14, 0, 4.45f)); Snapshot("exit");
            playerBody.position = new Vector3(-13.3f, 0, 4.45f); Physics.SyncTransforms();
            yield return Ticks(5); Assert.That(Flag("Completed"), Is.True);
            Call(level, "ResetCurrentPuzzle"); yield return null;
            Assert.That(Flag("Completed"), Is.False); Assert.That(Flag("IntroSolved"), Is.False);
            Assert.That(Door("introDoor").enabled, Is.True); Assert.That(Door("exitDoor").enabled, Is.True);
            Assert.That(Get(state, "now").ToString(), Is.EqualTo("Future"));
            yield return SolveIntro();
        }

        [UnityTest]
        public IEnumerator CheckpointCancelsTimeSwapAndRestoresGravityWithoutLosingAccess()
        {
            yield return SolveIntro(); yield return ReleaseStorage();
            Aim(new Vector3(-6, 0.05f, 13.2f), Cubes("mainCubes")[0].position);
            Call(interaction, "TryInteract");
            Assert.That(Prop(interaction, "HeldObject"), Is.Not.Null);
            yield return new WaitForSeconds(1.05f); Call(state, "SwapTime");
            Call(level, "ResetCurrentPuzzle"); yield return new WaitForSeconds(0.7f);
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Assert.That(Flag("IntroSolved"), Is.True); Assert.That(Door("introDoor").enabled, Is.False);
            Assert.That(Flag("StorageReleased"), Is.False); Assert.That(Door("storageDoor").enabled, Is.True);
            Assert.That(Get(state, "now").ToString(), Is.EqualTo("Future"));
            Assert.That((float)Get(state, "gravity"), Is.EqualTo(14.715f));
            Assert.That(playerBody.position.x, Is.EqualTo(-1.5f).Within(0.1f));
            Assert.That(Cubes("mainCubes")[1].position.x, Is.EqualTo(-6).Within(0.1f));
            yield return SolveMain();
        }

        [UnityTest]
        public IEnumerator PressurePlateRejectsPlayerWrongCubeAndWeightlessCube()
        {
            var plate = Get(level, "introPlate");
            Place(playerBody, new Vector3(2.5f, 0.5f, 13.45f)); yield return Ticks(60);
            Assert.That((bool)Prop(plate, "IsPressed"), Is.False);
            Place(playerBody, new Vector3(3, 0, 9));
            Place(Cubes("mainCubes")[1], new Vector3(2.5f, 0.1f, 13.45f)); yield return Ticks(60);
            Assert.That((bool)Prop(plate, "IsPressed"), Is.False);
            Place(Cubes("mainCubes")[1], new Vector3(-6, -0.48f, 15));
            var body = Cubes("introCubes")[1];
            Place(body, new Vector3(2.5f, -0.255f, 13.45f));
            Set(state, "gravity", 0f); yield return Ticks(15);
            Assert.That((bool)Prop(plate, "IsPressed"), Is.False);
            Set(state, "gravity", 14.715f); yield return Ticks(60);
            Assert.That((bool)Prop(plate, "IsPressed"), Is.True);
            Set(Component(body.gameObject, "ArtificialGravity"), "effective", false); yield return Ticks(5);
            Assert.That((bool)Prop(plate, "IsPressed"), Is.False, "Carried cubes cannot act as weights.");
            Assert.That(Flag("IntroSolved"), Is.True, "Latched progress survives removing the weight.");
        }

        [UnityTest]
        public IEnumerator InterlockRespondsRepeatedlyAndKeepsGravityAcrossTimelines()
        {
            yield return SolveIntro(); yield return ReleaseStorage();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
                Call(interaction, "TryInteract"); yield return Ticks(3);
                Assert.That(Door("storageDoor").enabled, Is.True);
                Assert.That(Flag("StorageReleased"), Is.False);
                Set(Component(player.gameObject, "ArtificialGravity"), "effective", false);
                yield return Ticks(3);
                Assert.That(Door("storageDoor").enabled, Is.True, "Personal gravity override cannot open storage.");
                Assert.That((float)Get(state, "gravity"), Is.EqualTo(14.715f));
                Set(Component(player.gameObject, "ArtificialGravity"), "effective", true);
                Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
                Call(interaction, "TryInteract"); yield return Ticks(3);
                Assert.That(Door("storageDoor").enabled, Is.False);
            }
            yield return Swap();
            Assert.That(Flag("StorageReleased"), Is.True, "The past console's gravity setting persists in the future.");
            Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
            Call(interaction, "TryInteract"); yield return null;
            Assert.That((float)Get(state, "gravity"), Is.Zero, "The broken future console must not toggle gravity.");
            yield return Swap();
            Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
            Call(interaction, "TryInteract"); yield return Ticks(3);
            Assert.That(Door("storageDoor").enabled, Is.True);
            Assert.That(Door("introDoor").enabled, Is.False, "Completed relay doors remain latched.");
        }

        [UnityTest]
        public IEnumerator RealQKeyChangesPersonalFlightWithoutReleasingStorage()
        {
            yield return SolveIntro(); yield return ReleaseStorage();
            Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
            Call(interaction, "TryInteract"); yield return Ticks(3);
            Assert.That(Door("storageDoor").enabled, Is.True);
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard = null; Mouse mouse = null;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
                keyboard.MakeCurrent(); mouse.MakeCurrent();
                playerBody.isKinematic = false; player.enabled = true;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
                Assert.That((bool)Get(player, "isInJetpack"), Is.True);
                Assert.That((float)Get(state, "gravity"), Is.EqualTo(14.715f));
                Assert.That(Door("storageDoor").enabled, Is.True, "Q must not act as the station console.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
                Assert.That((bool)Get(player, "isInJetpack"), Is.False);
                Assert.That(Door("storageDoor").enabled, Is.True);
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [UnityTest]
        public IEnumerator StorageSafetyProtectsPlayerAndCarriedCubeAcrossTimeChanges()
        {
            yield return SolveIntro(); yield return ReleaseStorage();
            var interlock = Get(level, "storageInterlock");
            Aim(new Vector3(-6, 0.05f, 14), new Vector3(-6, 1, 12));
            // Simulate gravity being restored while inside; the real console is outside the chamber.
            Call(Get(level, "gravitySwitch"), "onInteract", interaction); yield return Ticks(3);
            Assert.That((bool)Prop(interlock, "ClosingDeferred"), Is.True);
            Assert.That(Door("storageDoor").enabled, Is.False, "Never seal a player inside storage.");
            yield return Swap();
            Assert.That(Door("storageDoor").enabled, Is.False, "Escape safeguard exists in both timelines.");
            Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
            yield return Ticks(3); Assert.That(Door("storageDoor").enabled, Is.True);
            yield return Swap();
            Aim(new Vector3(-2, 0.05f, 8.8f), new Vector3(-2, 0.35f, 10.5f));
            Call(interaction, "TryInteract"); yield return Ticks(3);
            var cube = Cubes("mainCubes")[0];
            Place(cube, new Vector3(-6, -0.48f, 12));
            Aim(new Vector3(-6, 0.05f, 10.3f), cube.position);
            Call(interaction, "TryInteract");
            Assert.That(Prop(interaction, "HeldObject"), Is.Not.Null);
            Call(Get(level, "gravitySwitch"), "onInteract", interaction); yield return Ticks(3);
            Assert.That((bool)Prop(interlock, "ClosingDeferred"), Is.True, "A carried cube must block closure too.");
            Assert.That(Door("storageDoor").enabled, Is.False);
            yield return Swap();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Assert.That(Door("storageDoor").enabled, Is.False, "Causal future cube in the hatch must also block closure.");
            Place(Cubes("mainCubes")[1], new Vector3(-5.5f, -0.25f, 5));
            yield return Ticks(60);
            Assert.That(Door("storageDoor").enabled, Is.True, "Close automatically once the obstruction leaves.");
            Assert.That(Flag("MainSolved"), Is.True, "Safety holds must not make the relay unsolvable.");
            Assert.That(Door("exitDoor").enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator CompactStationSignsRetainSafeProjectionReferences()
        {
            var root = GameObject.Find("Holographic guidance");
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "Projections must not block walking or interaction rays.");
            var projections = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "Unlit/SimpleHologram").ToArray();
            Assert.That(projections.Length, Is.EqualTo(3), "Only two panels and one objective projection are needed.");
            foreach (var renderer in projections)
            {
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                Assert.That(renderer.sharedMaterial.GetFloat("_FlickerStrength"), Is.LessThanOrEqualTo(0.03f));
                Assert.That(renderer.sharedMaterial.GetFloat("_Alpha"), Is.InRange(0.2f, 0.5f));
            }
            var holograms = Component(root, "LevelHolograms");
            var text = Get(holograms, "consoleText");
            Aim(new Vector3(-2, 0.05f, 7.7f), new Vector3(-2, 1.15f, 10.5f));
            yield return null; Snapshot("hologram-console-future");
            Assert.That(Prop(text, "text").ToString(), Does.Contain("OFFLINE"));
            yield return Swap(); Snapshot("hologram-console-past");
            Assert.That(Prop(text, "text").ToString(), Does.Contain("GRAVITY CONTROL"));
            Aim(new Vector3(-6, 0.05f, 8.8f), new Vector3(-6, 1.35f, 12));
            yield return null; Snapshot("hologram-storage");
        }
    }
}
