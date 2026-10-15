using System.Collections;
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
    public class PowerModuleATests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MonoBehaviour module, interaction, controller, gravity;
        Rigidbody body, player;
        object state;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static MonoBehaviour Component(GameObject go, string name) => go.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == name);
        static object Get(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Fields).SetValue(obj, value);
        static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        bool Repaired => (bool)Prop(module, "IsRepaired");
        bool Ready => (bool)Prop(module, "CanInstall");
        Vector3 Alignment => (Vector3)Get(module, "alignmentPosition");

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
            module = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
            body = module.GetComponent<Rigidbody>(); player = GameObject.Find("Player").GetComponent<Rigidbody>();
            interaction = Component(player.gameObject, "Interact"); controller = Component(player.gameObject, "PlayerController");
            gravity = Component(player.gameObject, "ArtificialGravity");
            state = module.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (interaction != null) Call(interaction, "DropHeldObject");
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false;
            yield return null;
        }
        IEnumerator Past()
        {
            Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Assert.That(Get(state, "now").ToString(), Is.EqualTo("Past"));
        }
        void Aim(Vector3 rayPosition, Vector3 direction)
        {
            controller.enabled = false; player.isKinematic = true;
            player.position = rayPosition - Vector3.up * .2f; player.transform.position = player.position;
            Camera.main.transform.SetPositionAndRotation(rayPosition, Quaternion.LookRotation(direction));
            Physics.SyncTransforms();
        }
        void Interact() => Call(interaction, "TryInteract");

        IEnumerator CarryToMarker()
        {
            yield return Past();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((bool)Get(gravity, "effective"), Is.False, "Use the actual Q binding.");
            Assert.That((bool)Get(Component(module.gameObject, "ArtificialGravity"), "effective"), Is.True,
                "Q must not change the module's artificial gravity.");
            var start = body.position + Vector3.back * 1.8f;
            Aim(start, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            // Move the carrier, never teleport the module: its existing spring must lift and align it.
            var destination = Alignment + Vector3.back * 1.8f;
            for (int step = 1; step <= 100; step++)
            {
                Aim(Vector3.Lerp(start, destination, step / 100f), Vector3.forward);
                yield return new WaitForFixedUpdate();
            }
            yield return new WaitForSeconds(.5f);
            Assert.That(Vector3.Distance(body.position, Alignment), Is.LessThan(.35f));
            Assert.That(Ready, Is.True);
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("E — Install Module A"));
        }

        [UnityTest] public IEnumerator PhysicalCarryInstallsInZeroGAndCannotRepeat()
        {
            yield return CarryToMarker();
            Interact(); yield return null;
            Assert.That(Repaired, Is.True); Assert.That(body.isKinematic, Is.True);
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Assert.That((bool)Get(gravity, "effective"), Is.False, "Installation does not require restoring gravity.");
            Assert.That(Vector3.Distance(body.position, (Vector3)Get(module, "installationPosition")), Is.LessThan(.01f));
            Assert.That(GameObject.Find("A / INSTALLATION SLOT").GetComponent<TextMesh>().text, Is.EqualTo("A / REPAIRED"));
            Assert.That(((GameObject)Get(module, "alignmentMarker")).activeSelf, Is.False);
            var installed = body.position;
            Call(module, "onInteract", interaction); Interact(); yield return null;
            Assert.That(body.position, Is.EqualTo(installed)); Assert.That(Repaired, Is.True);
            Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Assert.That(Repaired, Is.False, "Cross-timeline persistence is deferred to Stage 3.");
            Assert.That(body.isKinematic, Is.True); Assert.That(Prop(interaction, "HeldObject"), Is.Null);
        }

        [UnityTest] public IEnumerator InstallationRejectsGroundedMisalignedBlockedAndPausedAttempts()
        {
            yield return CarryToMarker();
            Set(gravity, "effective", true); Assert.That(Ready, Is.False);
            Set(gravity, "effective", false);
            var oldPosition = player.position; player.position = new Vector3(oldPosition.x, 1.05f, oldPosition.z);
            player.transform.position = player.position; Assert.That(Ready, Is.False, "Q alone from the floor is insufficient.");
            player.position = oldPosition; player.transform.position = oldPosition;
            var oldModule = body.position; body.position += Vector3.right; body.transform.position = body.position;
            Assert.That(Ready, Is.False); body.position = oldModule; body.transform.position = oldModule;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = (Vector3)Get(module, "installationPosition"); Physics.SyncTransforms();
            Assert.That(Ready, Is.False, "An occupied socket cannot accept the module.");
            Object.Destroy(blocker); yield return null; Physics.SyncTransforms();
            Assert.That(Ready, Is.True);
            GameSettingsMenu.Instance.Open(); Interact(); Call(module, "onInteract", interaction);
            Assert.That(Repaired, Is.False); Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            Assert.That(Ready, Is.True); Interact(); Assert.That(Repaired, Is.True);
        }

        [UnityTest] public IEnumerator PickupRequiresPastZeroGReachAndClearRayAndSwitchingTimeDropsSafely()
        {
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            yield return Past(); Interact(); Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Set(gravity, "effective", false);
            Aim(body.position + Vector3.back * 4, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = body.position + Vector3.back; Physics.SyncTransforms(); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Object.Destroy(wall); yield return null; Physics.SyncTransforms(); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            Interact(); Assert.That(Prop(interaction, "HeldObject"), Is.Null, "E releases a misaligned module.");
            Interact(); Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            yield return new WaitForSeconds(.5f); Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Assert.That(Prop(interaction, "HeldObject"), Is.Null); Assert.That(body.isKinematic, Is.True);
            Assert.That(Repaired, Is.False);
        }
    }
}
