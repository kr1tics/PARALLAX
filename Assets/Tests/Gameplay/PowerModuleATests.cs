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
            FindLevelComponents();
        }
        void FindLevelComponents()
        {
            module = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
            body = module.GetComponent<Rigidbody>(); player = GameObject.Find("Player").GetComponent<Rigidbody>();
            interaction = Component(player.gameObject, "Interact"); controller = Component(player.gameObject, "PlayerController");
            gravity = Component(player.gameObject, "ArtificialGravity");
            state = module.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var completion = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetType().Name == "TutorialCompletionScreen");
            if (completion != null) completion.enabled = false;
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
            if (Get(state, "now").ToString() != "Past") yield return Past();
            if ((bool)Get(gravity, "effective"))
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
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
            yield return CycleTimelines(true, false);
        }

        void RepairB()
        {
            var panel = GameObject.Find("Repair panel");
            Aim(panel.transform.position + Vector3.left * 1.6f, Vector3.right);
            Interact();
            Assert.That((bool)Prop(Component(panel, "PowerModuleB"), "IsRepaired"), Is.True);
        }

        MonoBehaviour Presentation => Component(GameObject.Find("08 / Scene presentation"), "PowerRestorationPresentation");

        void CheckRepairState(bool repairedA, bool repairedB)
        {
            var modules = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            Assert.That(modules.Count(x => x.GetType().Name == "PowerModuleA"), Is.EqualTo(1));
            Assert.That(modules.Count(x => x.GetType().Name == "PowerModuleB"), Is.EqualTo(1));
            Assert.That(modules.Count(x => x.GetType().Name == "PlayerController"), Is.EqualTo(1));
            Assert.That(modules.Count(x => x.GetType().Name == "GlobalStateCompanion"), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameSettingsMenu>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var moduleB = Component(GameObject.Find("Repair panel"), "PowerModuleB");
            Assert.That(Repaired, Is.EqualTo(repairedA));
            Assert.That((bool)Prop(moduleB, "IsRepaired"), Is.EqualTo(repairedB));
            Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.EqualTo(repairedA && repairedB));
            bool past = Get(state, "now").ToString() == "Past";
            Assert.That(body.isKinematic, Is.EqualTo(repairedA || !past));
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Assert.That(((GameObject)Get(module, "alignmentMarker")).activeSelf, Is.EqualTo(past && !repairedA));
            Assert.That(GameObject.Find("A / INSTALLATION SLOT").GetComponent<TextMesh>().text,
                Is.EqualTo(repairedA ? "A / REPAIRED" : "A / INSTALLATION SLOT"));
            Assert.That(GameObject.Find("B / REPAIR").GetComponent<TextMesh>().text,
                Is.EqualTo(repairedB ? "B / REPAIRED" : "B / REPAIR"));
            Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text,
                Is.EqualTo(repairedB ? "REPAIR COMPLETE" : "CONNECTION LOST"));
            Assert.That(GameObject.Find("REACTOR / OFFLINE").GetComponent<TextMesh>().text, Is.EqualTo("REACTOR / OFFLINE"));
            Assert.That(GameObject.Find("RESTART / LOCKED").GetComponent<TextMesh>().text,
                Is.EqualTo(repairedA && repairedB && !past ? "RESTART / READY" : "RESTART / LOCKED"));
            if (repairedA)
            {
                Assert.That(Vector3.Distance(body.position, (Vector3)Get(module, "installationPosition")), Is.LessThan(.01f));
                Assert.That(Quaternion.Angle(body.rotation, (Quaternion)Get(module, "initialRotation")), Is.LessThan(.01f));
                Assert.That(Ready, Is.False);
                Assert.That((Color32)GameObject.Find("A / INSTALLATION SLOT").GetComponent<TextMesh>().color,
                    Is.EqualTo((Color32)new Color(.35f, 1, .65f)));
            }
            var instruction = GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>();
            Assert.That(instruction.text, Does.Contain((repairedA ? "A repaired" : "A pending") + " · " +
                (repairedB ? "B repaired" : "B pending")));
            if (repairedA && repairedB) Assert.That(instruction.text, Does.Contain("Reactor ready"));
            else Assert.That(instruction.text, Does.Not.Contain("Reactor ready"));
            if (!past && (repairedA ^ repairedB)) Assert.That(instruction.text, Does.Contain("preserved"));
            Assert.That(GameObject.Find("Objective number").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("OBJECTIVE " + (repairedA && repairedB ? past ? "02" : "03" : "01")));
            instruction.ForceMeshUpdate(); Assert.That(instruction.isTextOverflowing, Is.False, instruction.text);
        }

        IEnumerator CycleTimelines(bool repairedA, bool repairedB)
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                yield return new WaitForSeconds(.4f); // Finish the existing wipe's cooldown.
                string expected = Get(state, "now").ToString() == "Past" ? "Future" : "Past";
                Call(state, "SwapTime"); Call(state, "SwapTime"); // Repeated input must produce one swap.
                Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.EqualTo(repairedA && repairedB));
                yield return new WaitForSeconds(.7f);
                Assert.That(Get(state, "now").ToString(), Is.EqualTo(expected));
                if (repairedA) Call(module, "onInteract", interaction);
                if (repairedB) Call(Component(GameObject.Find("Repair panel"), "PowerModuleB"), "onInteract", interaction);
                yield return null;
                CheckRepairState(repairedA, repairedB);
            }
        }

        IEnumerator CheckBothRepairedAcrossTimelines()
        {
            Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.True, "Readiness changes immediately with the second repair.");
            yield return null;
            CheckRepairState(true, true);
            GameSettingsMenu.Instance.Open(); yield return null;
            CheckRepairState(true, true);
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            yield return CycleTimelines(true, true);
        }

        [UnityTest] public IEnumerator ModuleBCanBeRepairedAfterInstallingModuleA()
        {
            yield return CarryToMarker(); Interact();
            Assert.That(Repaired, Is.True);
            yield return null;
            Assert.That(GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("A repaired · B pending").And.Contain("Float to B"));
            Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.False);
            RepairB(); yield return CheckBothRepairedAcrossTimelines();
            // Repairs belong to this playthrough, not a singleton or save file.
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null;
            FindLevelComponents();
            Assert.That(Get(state, "now").ToString(), Is.EqualTo("Future"));
            CheckRepairState(false, false);
            Assert.That(controller.enabled, Is.True); Assert.That(player.isKinematic, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
        }

        [UnityTest] public IEnumerator ModuleACanBeCarriedAndInstalledAfterRepairingModuleB()
        {
            yield return Past();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            RepairB(); yield return null;
            Assert.That(GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("A pending · B repaired").And.Contain("Align A"));
            Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.False);
            yield return CarryToMarker(); Interact(); yield return CheckBothRepairedAcrossTimelines();
        }

        [UnityTest] public IEnumerator MisalignedDropBesideSocketRemainsRecoverable()
        {
            yield return CarryToMarker();
            Aim(Alignment + Vector3.left * 1.6f + Vector3.back * 1.8f, Vector3.forward);
            yield return new WaitForSeconds(.8f); // The normal grab spring carries it beside the socket.
            Assert.That(Ready, Is.False); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            yield return new WaitForSeconds(1);
            Assert.That(Repaired, Is.False);
            Assert.That(Vector3.Distance(body.position, (Vector3)Get(module, "initialPosition")), Is.GreaterThan(2));
            yield return CycleTimelines(false, false);
            Assert.That(Vector3.Distance(body.position, (Vector3)Get(module, "initialPosition")), Is.LessThan(.15f));
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
        }

        [UnityTest] public IEnumerator SceneReloadClearsUntouchedAndPartialRepairProgress()
        {
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null;
            FindLevelComponents(); CheckRepairState(false, false);
            yield return CarryToMarker(); Interact(); yield return null;
            CheckRepairState(true, false);
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null;
            FindLevelComponents(); CheckRepairState(false, false);
            CheckObjective("01", "Restore Power Modules");
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator UnrepairedModulesRemainPendingAcrossTimelineCycles()
        {
            yield return CycleTimelines(false, false);
        }

        MonoBehaviour Reactor => Component(GameObject.Find("Console housing"), "PowerReactor");
        MonoBehaviour Completion => Component(GameObject.Find("08 / Scene presentation"), "TutorialCompletionScreen");
        void AimAtReactor(float distance = 1.8f) => Aim(Reactor.transform.position + Vector3.back * distance, Vector3.forward);
        void RejectReactor()
        {
            Assert.That((bool)Prop(Reactor, "CanActivate"), Is.False);
            Interact(); Call(Reactor, "onInteract", interaction); Call(Completion, "TryShow");
            Assert.That((bool)Prop(Reactor, "IsOnline"), Is.False);
            Assert.That((bool)Prop(Completion, "IsOpen"), Is.False);
        }
        void CheckObjective(string number, string title)
        {
            Assert.That(GameObject.Find("Objective number").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("OBJECTIVE " + number));
            Assert.That(GameObject.Find("Objective title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(title));
            var instruction = GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>();
            instruction.ForceMeshUpdate(); Assert.That(instruction.isTextOverflowing, Is.False, instruction.text);
        }
        IEnumerator ActivateAndCheckCompletion(bool click = false)
        {
            player.isKinematic = false;
            player.position = new Vector3(3.5f, 1.05f, 1.2f); player.transform.position = player.position;
            player.linearVelocity = Vector3.zero;
            Call(Component(Camera.main.gameObject, "Look"), "Rotate", new Vector2(90, 0));
            Physics.SyncTransforms();
            controller.enabled = true; // Exercise the real E / click bindings at the console.
            yield return null; yield return null; // Let cursor locking settle before injecting the click.
            Assert.That((bool)Prop(Reactor, "CanActivate"), Is.True, "The normal player camera must reach the console.");
            if (click) InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            else InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((bool)Prop(Reactor, "IsOnline"), Is.True);
            Assert.That((bool)Prop(Completion, "IsOpen"), Is.True);
            Assert.That(Time.timeScale, Is.Zero); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.True);
            Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(GameObject.Find("REACTOR / OFFLINE").GetComponent<TextMesh>().text, Is.EqualTo("REACTOR / ONLINE"));
            Assert.That(GameObject.Find("RESTART / LOCKED").GetComponent<TextMesh>().text, Is.EqualTo("POWER / RESTORED"));
            Assert.That(((Light)Get(Reactor, "poweredLight")).enabled, Is.True);
            Assert.That(((Light)Get(Reactor, "poweredLight")).intensity, Is.GreaterThan(0));
            CheckObjective("04", "Power Restored");
            // Modal input cannot move/look, change time/gravity, open Settings or activate again.
            var position = player.position; var rotation = Camera.main.transform.rotation;
            bool gravityBefore = (bool)Get(gravity, "effective");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space, Key.Q, Key.F, Key.Escape, Key.E));
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(100, 30));
            GameSettingsMenu.Instance.Open(); Call(Reactor, "onInteract", interaction); Call(Completion, "TryShow");
            yield return new WaitForSecondsRealtime(2.7f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False);
            Assert.That(player.position, Is.EqualTo(position)); Assert.That(Camera.main.transform.rotation, Is.EqualTo(rotation));
            Assert.That((bool)Get(gravity, "effective"), Is.EqualTo(gravityBefore));
            Assert.That(Get(state, "now").ToString(), Is.EqualTo("Future"));
            Assert.That((bool)Prop(Completion, "IsReady"), Is.True, "UI animates on unscaled time.");
            Assert.That((bool)Prop(Reactor, "CanActivate"), Is.False);
            var screen = GameObject.Find("Power Restoration Completion Screen");
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(x => x.name == screen.name), Is.EqualTo(1));
            CollectionAssert.AreEquivalent(new[] { "RESTART LEVEL", "CONTINUE" },
                screen.GetComponentsInChildren<UnityEngine.UI.Button>().Select(x => x.name));
            foreach (var label in screen.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False, label.name);
                Assert.That(label.font.name, Is.EqualTo(label.name.Contains("description") || label.name.Contains("final message") ?
                    "SpaceMono-Regular SDF" : "Oxanium-Regular SDF"));
            }
        }

        [UnityTest] public IEnumerator ReactorGatesRealRepairsRangeOcclusionAndPauseThenRestartsCleanly()
        {
            CheckObjective("01", "Restore Power Modules"); AimAtReactor(); RejectReactor();
            Assert.That((string)Prop(Reactor, "InteractionHint"), Is.EqualTo("Power modules incomplete"));
            controller.enabled = true; player.isKinematic = false;
            yield return CarryToMarker(); Interact(); yield return null;
            CheckObjective("01", "Restore Power Modules"); AimAtReactor(); RejectReactor(); // Only A.
            Call(state, "SwapTime"); yield return new WaitForSeconds(1.1f);
            AimAtReactor(); RejectReactor(); CheckObjective("01", "Restore Power Modules"); // Only A in Future.
            Call(state, "SwapTime"); yield return new WaitForSeconds(1.1f);
            RepairB(); yield return null;
            CheckObjective("02", "Return to the Future"); AimAtReactor(); RejectReactor();
            Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.True);
            Assert.That((string)Prop(Reactor, "InteractionHint"), Does.Contain("Return to the Future"));
            Call(state, "SwapTime"); yield return new WaitForSeconds(1.1f);
            CheckObjective("03", "Activate the Reactor");
            AimAtReactor(4); RejectReactor();
            AimAtReactor(); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (Camera.main.transform.position + Reactor.transform.position) * .5f;
            Physics.SyncTransforms(); RejectReactor(); Object.Destroy(wall); yield return null;
            Physics.SyncTransforms(); Assert.That((bool)Prop(Reactor, "CanActivate"), Is.True);
            GameSettingsMenu.Instance.Open(); RejectReactor(); GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            GameSettingsMenu.StartupInputBlocked = true; RejectReactor(); GameSettingsMenu.StartupInputBlocked = false;
            yield return ActivateAndCheckCompletion();
            GameObject.Find("RESTART LEVEL").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; yield return null;
            FindLevelComponents(); CheckRepairState(false, false); CheckObjective("01", "Restore Power Modules");
            Assert.That((bool)Prop(Reactor, "IsOnline"), Is.False); Assert.That((bool)Prop(Completion, "IsOpen"), Is.False);
            Assert.That(((Light)Get(Reactor, "poweredLight")).enabled, Is.False);
            Assert.That(GameObject.Find("Power Restoration Completion Screen"), Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(Vector3.Distance(player.position, new Vector3(0, 1.05f, -8.2f)), Is.LessThan(.25f));
            Assert.That(controller.enabled, Is.True); Assert.That(player.isKinematic, Is.False);
            Assert.That(Cursor.visible, Is.False);
            var before = player.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Assert.That(Vector3.Distance(before, player.position), Is.GreaterThan(.2f));
            GameSettingsMenu.Instance.Open(); Assert.That(GameSettingsMenu.Instance.IsOpen, Is.True); GameSettingsMenu.Instance.Close();
        }

        [UnityTest] public IEnumerator ModuleBAloneCannotActivateOrUseTutorialCompletionShortcut()
        {
            yield return Past();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); RepairB(); yield return null;
            CheckObjective("01", "Restore Power Modules");
            yield return new WaitForSeconds(.4f); Call(state, "SwapTime"); yield return new WaitForSeconds(1.1f);
            AimAtReactor(); RejectReactor(); Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.K)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((bool)Prop(Completion, "IsOpen"), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CompletedReactorContinuesToCargoAndReentryStartsFresh()
        {
            yield return CarryToMarker(); Interact(); RepairB();
            Call(state, "SwapTime"); yield return new WaitForSeconds(1.1f);
            yield return ActivateAndCheckCompletion(click: true);
            GameObject.Find("CONTINUE").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level2_CargoBay"));
            Assert.That(GameSettingsMenu.StartupInputBlocked, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(GameObject.Find("Power Restoration Completion Screen"), Is.Null);
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null;
            FindLevelComponents(); CheckRepairState(false, false); CheckObjective("01", "Restore Power Modules");
            Assert.That((bool)Prop(Reactor, "IsOnline"), Is.False); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            // A second run must get a fresh completion panel and lock, with no stale listeners.
            yield return CarryToMarker(); Interact(); RepairB();
            Call(state, "SwapTime"); yield return new WaitForSeconds(1.1f);
            yield return ActivateAndCheckCompletion();
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
            yield return new WaitForSeconds(.4f); Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module), "A dropped by time travel remains recoverable in the Past.");
        }
    }
}
