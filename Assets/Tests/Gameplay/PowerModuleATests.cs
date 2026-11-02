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
        static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        bool Repaired => (bool)Prop(module, "IsRepaired");
        bool Ready => (bool)Prop(module, "CanInstall");
        Vector3 Socket => (Vector3)Get(module, "installationPosition");
        string Now => Get(state, "now").ToString();
        float StationGravity => (float)Get(state, "gravity");

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
        IEnumerator Swap(string expected)
        {
            yield return new WaitForSeconds(1.05f); // The existing wipe has a one-second cooldown.
            Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Assert.That(Now, Is.EqualTo(expected));
        }
        IEnumerator Past() { if (Now != "Past") yield return Swap("Past"); }
        void Aim(Vector3 rayPosition, Vector3 direction)
        {
            controller.enabled = false; player.isKinematic = true;
            player.position = rayPosition - Vector3.up * .2f; player.transform.position = player.position;
            Camera.main.transform.SetPositionAndRotation(rayPosition, Quaternion.LookRotation(direction));
            Physics.SyncTransforms();
        }
        void Interact() => Call(interaction, "TryInteract");
        MonoBehaviour Circuit => Component(GameObject.Find("09 / Power path"), "PowerCircuit");
        MonoBehaviour Presentation => Component(GameObject.Find("08 / Scene presentation"), "PowerRestorationPresentation");
        MonoBehaviour ModuleB => Component(GameObject.Find("Repair panel"), "PowerModuleB");

        // The real gravity console: a shared Switch + GravityTrigger, reachable only in 2026.
        void ToggleGravity()
        {
            var console = GameObject.Find("Gravity switch");
            Assert.That(console, Is.Not.Null, "The gravity switch exists only in 2026.");
            Aim(console.transform.position + Vector3.right * 1.5f, Vector3.left); Interact();
        }
        IEnumerator StationGravityOff() { yield return Past(); if (StationGravity > .1f) ToggleGravity(); yield return null; Assert.That(StationGravity, Is.LessThan(.1f)); }

        IEnumerator CarryToSocket()
        {
            yield return StationGravityOff();
            var start = body.position + Vector3.back * 1.8f;
            Aim(start, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            // Move the carrier, never teleport the module: its existing spring carries it in front of the socket.
            var destination = Socket + Vector3.back * 2.2f;
            for (int step = 1; step <= 100; step++)
            {
                Aim(Vector3.Lerp(start, destination, step / 100f), Vector3.forward);
                yield return new WaitForFixedUpdate();
            }
            yield return new WaitForSeconds(.5f);
            Assert.That(Vector3.Distance(body.position, Socket), Is.LessThan((float)Get(module, "guideRadius")));
            Assert.That(Ready, Is.True);
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("E — Install Module A"));
        }
        IEnumerator Install() { yield return CarryToSocket(); Interact(); Assert.That(Repaired, Is.True); yield return new WaitForSeconds(.4f); }

        void RepairB()
        {
            var panel = GameObject.Find("Repair panel");
            Aim(panel.transform.position + Vector3.left * 1.6f, Vector3.right);
            Interact();
            Assert.That((bool)Prop(ModuleB, "IsRepaired"), Is.True);
        }
        void SelectRoute(int route)
        {
            var junction = GameObject.Find("B feed junction");
            Aim(junction.transform.position + Vector3.left * 1.5f, Vector3.right);
            if ((int)Prop(Circuit, "Route") != route) Interact();
            Assert.That((int)Prop(Circuit, "Route"), Is.EqualTo(route));
        }
        // Everything done in 2026: A, B, the surviving route and station gravity back on.
        IEnumerator SolveIn2026()
        {
            yield return Install(); RepairB(); SelectRoute(2);
            ToggleGravity(); yield return null; Assert.That(StationGravity, Is.GreaterThan(4.5f));
        }

        void CheckRepairState(bool repairedA, bool repairedB)
        {
            var modules = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            Assert.That(modules.Count(x => x.GetType().Name == "PowerModuleA"), Is.EqualTo(1));
            Assert.That(modules.Count(x => x.GetType().Name == "PowerModuleB"), Is.EqualTo(1));
            Assert.That(modules.Count(x => x.GetType().Name == "PlayerController"), Is.EqualTo(1));
            Assert.That(modules.Count(x => x.GetType().Name == "GlobalStateCompanion"), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameSettingsMenu>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Repaired, Is.EqualTo(repairedA));
            Assert.That((bool)Prop(ModuleB, "IsRepaired"), Is.EqualTo(repairedB));
            bool ready = (bool)Prop(Presentation, "ReactorReady");
            Assert.That(ready, Is.EqualTo(repairedA && repairedB && (int)Prop(Circuit, "Route") == 2 && StationGravity > 4.5f));
            bool past = Now == "Past";
            Assert.That(body.isKinematic, Is.EqualTo(repairedA || !past));
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Assert.That(((GameObject)Get(module, "alignmentMarker")).activeSelf, Is.EqualTo(past && !repairedA));
            Assert.That(GameObject.Find("A / INSTALLATION SLOT").GetComponent<TextMesh>().text,
                Is.EqualTo(repairedA ? "A / REPAIRED" : "A / INSTALLATION SLOT"));
            Assert.That(GameObject.Find("B / REPAIR").GetComponent<TextMesh>().text, Is.EqualTo(repairedB ? "B / REPAIRED" : "B / REPAIR"));
            Assert.That(GameObject.Find("REACTOR / OFFLINE").GetComponent<TextMesh>().text, Is.EqualTo("REACTOR / OFFLINE"));
            Assert.That(GameObject.Find("RESTART / LOCKED").GetComponent<TextMesh>().text,
                Is.EqualTo(ready && !past ? "RESTART / READY" : "RESTART / LOCKED"));
            if (repairedA)
            {
                Assert.That(Vector3.Distance(body.position, Socket), Is.LessThan(.01f));
                Assert.That(Quaternion.Angle(body.rotation, (Quaternion)Get(module, "initialRotation")), Is.LessThan(.01f));
                Assert.That(Ready, Is.False);
                Assert.That((Color32)GameObject.Find("A / INSTALLATION SLOT").GetComponent<TextMesh>().color, Is.EqualTo((Color32)new Color(.35f, 1, .65f)));
            }
            var instruction = GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>();
            Assert.That(instruction.text, Does.Contain((repairedA ? "A repaired" : "A pending") + " · " + (repairedB ? "B repaired" : "B pending")));
            Assert.That(instruction.text.Contains("Reactor ready"), Is.EqualTo(ready));
            Assert.That(GameObject.Find("Objective number").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("OBJECTIVE " + (!(repairedA && repairedB) ? "01" : ready ? "03" : "02")));
            instruction.ForceMeshUpdate(); Assert.That(instruction.isTextOverflowing, Is.False, instruction.text);
        }

        IEnumerator CycleTimelines(bool repairedA, bool repairedB)
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                yield return new WaitForSeconds(.4f); // Finish the existing wipe's cooldown.
                string expected = Now == "Past" ? "Future" : "Past";
                Call(state, "SwapTime"); Call(state, "SwapTime"); // Repeated input must produce one swap.
                yield return new WaitForSeconds(.7f);
                Assert.That(Now, Is.EqualTo(expected));
                if (repairedA) Call(module, "onInteract", interaction);
                if (repairedB) Call(ModuleB, "onInteract", interaction);
                yield return null;
                CheckRepairState(repairedA, repairedB);
            }
        }

        [UnityTest] public IEnumerator CarryAndSeatInStationZeroGAndCannotRepeat()
        {
            yield return CarryToSocket();
            Interact(); yield return null;
            Assert.That(Repaired, Is.True); Assert.That(body.isKinematic, Is.True);
            Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            Assert.That(Vector3.Distance(body.position, Socket), Is.GreaterThan(.01f), "The rails draw the casing in over a moment.");
            yield return new WaitForSeconds(.4f);
            Assert.That(Vector3.Distance(body.position, Socket), Is.LessThan(.01f));
            Assert.That(GameObject.Find("A / INSTALLATION SLOT").GetComponent<TextMesh>().text, Is.EqualTo("A / REPAIRED"));
            Assert.That(((GameObject)Get(module, "alignmentMarker")).activeSelf, Is.False);
            var installed = body.position;
            Call(module, "onInteract", interaction); Interact(); yield return null;
            Assert.That(body.position, Is.EqualTo(installed)); Assert.That(Repaired, Is.True);
            ToggleGravity(); yield return null;
            Assert.That(body.position, Is.EqualTo(installed), "Restoring gravity leaves the installed module seated.");
            yield return CycleTimelines(true, false);
        }

        [UnityTest] public IEnumerator PickupNeedsPastStationZeroGReachAndClearRayAndGravityOrTimeDropsSafely()
        {
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null, "2176: the module is fixed in its cradle.");
            yield return Past();
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null, "380 kg cannot be lifted under station gravity.");
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("Too heavy — station gravity ON"));
            controller.enabled = true; player.isKinematic = false; // Use the real Q binding.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((bool)Get(gravity, "effective"), Is.False);
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null, "Personal zero-G does not make the module weightless.");
            ToggleGravity(); yield return null; Assert.That(StationGravity, Is.LessThan(.1f));
            Aim(body.position + Vector3.back * 4, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null, "Out of reach.");
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = body.position + Vector3.back; Physics.SyncTransforms(); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.Null, "Blocked ray.");
            Object.Destroy(wall); yield return null;
            // Weightless, the module may have drifted from the blocker's contact; aim at where it is now.
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            Interact(); Assert.That(Prop(interaction, "HeldObject"), Is.Null, "E releases a module away from its socket.");
            Interact(); Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            // While carrying, E belongs to the held module, so the console cannot be pressed; gravity returning by any
            // other means still drops the 380 kg module where it is.
            var console = Component(GameObject.Find("Gravity switch"), "Switch");
            Call(console, "onInteract", interaction); yield return null; yield return null;
            Assert.That(StationGravity, Is.GreaterThan(4.5f));
            Assert.That(Prop(interaction, "HeldObject"), Is.Null); Assert.That(Repaired, Is.False);
            yield return new WaitForSeconds(1.5f); // Let it land before gravity goes off again.
            Call(console, "onInteract", interaction); yield return null;
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            yield return Swap("Future");
            Assert.That(Prop(interaction, "HeldObject"), Is.Null); Assert.That(body.isKinematic, Is.True); Assert.That(Repaired, Is.False);
            yield return Swap("Past");
            Assert.That(Vector3.Distance(body.position, (Vector3)Get(module, "initialPosition")), Is.LessThan(.05f), "Time travel returns it to its cradle.");
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module), "A dropped module remains recoverable in 2026.");
        }

        [UnityTest] public IEnumerator ReleaseAwayFromTheSocketRemainsRecoverable()
        {
            yield return CarryToSocket();
            Aim(Socket + Vector3.back * 2.2f + Vector3.left * 1.8f, Vector3.forward);
            yield return new WaitForSeconds(.8f); // The normal grab spring carries it beside the socket.
            Assert.That(Ready, Is.False);
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("Bring A to its socket · E — Release"));
            Interact(); Assert.That(Prop(interaction, "HeldObject"), Is.Null);
            yield return new WaitForSeconds(1);
            Assert.That(Repaired, Is.False);
            Assert.That(Vector3.Distance(body.position, (Vector3)Get(module, "initialPosition")), Is.GreaterThan(2));
            yield return CycleTimelines(false, false);
            Assert.That(Vector3.Distance(body.position, (Vector3)Get(module, "initialPosition")), Is.LessThan(.15f));
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module), "Station gravity is still off in 2026.");
        }

        [UnityTest] public IEnumerator SocketGuideIsForgivingButRejectsTiltedBlockedAndPausedAttempts()
        {
            yield return CarryToSocket();
            var initial = (Quaternion)Get(module, "initialRotation");
            body.rotation = initial * Quaternion.Euler(0, 30, 20); body.transform.rotation = body.rotation;
            Assert.That(Ready, Is.True, "A loosely tilted module still seats.");
            body.rotation = initial * Quaternion.Euler(0, 70, 0); body.transform.rotation = body.rotation;
            Assert.That(Ready, Is.False, "Beyond the rails' 45 degrees it does not.");
            body.rotation = initial; body.transform.rotation = initial;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = Socket; Physics.SyncTransforms();
            Assert.That(Ready, Is.False, "An occupied socket cannot accept the module.");
            Object.Destroy(blocker); yield return null; Physics.SyncTransforms();
            Assert.That(Ready, Is.True);
            GameSettingsMenu.Instance.Open(); Interact(); Call(module, "onInteract", interaction);
            Assert.That(Repaired, Is.False); Assert.That(Prop(interaction, "HeldObject"), Is.SameAs(module));
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            Assert.That(Ready, Is.True); Interact(); Assert.That(Repaired, Is.True);
            yield return new WaitForSeconds(.4f);
            Assert.That(Quaternion.Angle(body.rotation, initial), Is.LessThan(.01f), "The rails square the casing up.");
        }

        string Message => GameObject.Find("Power event message").GetComponent<TMPro.TMP_Text>().text;
        string Prompt => GameObject.Find("Contextual interaction prompt").GetComponent<TMPro.TMP_Text>().text;

        // Phase 3: every refusal says why, every change is announced once, and no key sign spells out the route.
        [UnityTest] public IEnumerator FeedbackExplainsRefusalsAndAnnouncesChanges()
        {
            Assert.That(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t => t.text.Contains("JETPACK")), Is.False, "The key sign is gone.");
            Assert.That(GameObject.Find("Contextual control hints").GetComponent<TMPro.TMP_Text>().text, Does.Contain("Q — Toggle jetpack"));
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("Seized in its cradle in 2176"));
            Assert.That((string)Prop(ModuleB, "InteractionHint"), Is.EqualTo("Junction corroded in 2176"));
            yield return Past();
            var console = GameObject.Find("Gravity switch");
            Aim(console.transform.position + Vector3.right * 1.5f, Vector3.left); yield return null;
            Assert.That(Prompt, Is.EqualTo("E — Cut station gravity"));
            ToggleGravity(); yield return null; // Re-aims in the same frame, as the other gravity helpers do.
            Assert.That(Message, Is.EqualTo("Station gravity OFF")); Assert.That(Prompt, Is.EqualTo("E — Restore station gravity"));
            yield return CarryToSocket();
            var initial = (Quaternion)Get(module, "initialRotation");
            body.rotation = initial * Quaternion.Euler(0, 70, 0); body.transform.rotation = body.rotation;
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("Square A up to the socket · E — Release"));
            body.rotation = initial; body.transform.rotation = initial;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.position = Socket; Physics.SyncTransforms();
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("Socket blocked · E — Release"));
            Object.Destroy(blocker); yield return null; Physics.SyncTransforms();
            Interact(); yield return null;
            Assert.That(Message, Is.EqualTo("Module A seated · feed live"));
            SelectRoute(2); yield return null;
            Assert.That(Message, Is.EqualTo("B feed switched to route 2"));
            yield return new WaitForSeconds(2.8f);
            Assert.That(Message, Is.Empty, "Announcements clear themselves.");
        }

        [UnityTest] public IEnumerator TimeTravelReturningModuleAIsAnnounced()
        {
            yield return StationGravityOff();
            Aim(body.position + Vector3.back * 1.8f, Vector3.forward); Interact();
            Aim(body.position + Vector3.back * 1.8f + Vector3.right * 2, Vector3.forward); yield return new WaitForSeconds(.8f);
            Interact(); yield return new WaitForSeconds(.3f);
            yield return Swap("Future"); yield return null;
            Assert.That(Message, Is.EqualTo("Module A returned to its cradle"));
        }

        [UnityTest] public IEnumerator ModuleBHasNoFeedUntilModuleAIsInstalled()
        {
            yield return Past();
            var panel = GameObject.Find("Repair panel");
            Aim(panel.transform.position + Vector3.left * 1.6f, Vector3.right); Interact();
            Assert.That((bool)Prop(ModuleB, "IsRepaired"), Is.False);
            Assert.That((string)Prop(ModuleB, "InteractionHint"), Is.EqualTo("No feed — module A offline"));
            Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text, Is.EqualTo("CONNECTION LOST"));
            yield return Install(); yield return null;
            Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text, Is.EqualTo("FEED LIVE"));
            RepairB(); yield return null;
            Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text, Is.EqualTo("REPAIR COMPLETE"));
            CheckRepairState(true, true);
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null;
            FindLevelComponents();
            Assert.That(Now, Is.EqualTo("Future")); Assert.That(StationGravity, Is.GreaterThan(4.5f));
            Assert.That((int)Prop(Circuit, "Route"), Is.EqualTo(1));
            CheckRepairState(false, false);
            Assert.That(controller.enabled, Is.True); Assert.That(player.isKinematic, Is.False);
        }

        [UnityTest] public IEnumerator UnrepairedModulesRemainPendingAcrossTimelineCycles() { yield return CycleTimelines(false, false); }

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
        bool Lit(string name, int index = 0) =>
            ((Renderer[])Get(Circuit, name))[index].sharedMaterial == (Material)Get(Circuit, "liveMaterial");

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
            // The start-up plays first, visibly, with input already locked so Settings cannot open over it.
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.True);
            Assert.That((bool)Prop(Completion, "IsOpen"), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            var core = (Renderer)Get(Reactor, "core");
            Assert.That(GameObject.Find("Power event message").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Reactor start-up"));
            float startIntensity = ((Light)Get(Reactor, "poweredLight")).intensity;
            GameSettingsMenu.Instance.Open(); Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False);
            yield return new WaitForSecondsRealtime(1);
            Assert.That(((Light)Get(Reactor, "poweredLight")).intensity, Is.GreaterThan(startIntensity), "The core light ramps up.");
            Assert.That(core.sharedMaterial, Is.SameAs(Get(Reactor, "coreOnline")), "The core lights up.");
            yield return new WaitForSecondsRealtime(1.5f);
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
            Assert.That(Now, Is.EqualTo("Future"));
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

        // The whole power path: route 1 dies by 2176, the selector is seized there, and the rods need station gravity.
        [UnityTest] public IEnumerator ReactorNeedsTheSurvivingRouteAndSeatedRodsThenRestartsCleanly()
        {
            CheckObjective("01", "Restore Power Modules"); AimAtReactor(); RejectReactor();
            Assert.That((string)Prop(Reactor, "InteractionHint"), Is.EqualTo("Power path incomplete"));
            controller.enabled = true; player.isKinematic = false;
            yield return Install(); CheckObjective("01", "Restore Power Modules");
            Assert.That(Lit("feedLamps"), Is.True); Assert.That(Lit("routeOneLamps", 4), Is.True, "In 2026 route 1 is intact.");
            RepairB(); yield return null;
            CheckObjective("02", "Complete the Power Path");
            // First visit to 2176 with route 1 and station gravity still off.
            yield return Swap("Future"); yield return null;
            CheckObjective("02", "Complete the Power Path"); AimAtReactor(); RejectReactor();
            Assert.That(Lit("consoleLamps", 0), Is.True, "A feed."); Assert.That(Lit("consoleLamps", 1), Is.False, "B feed.");
            Assert.That(Lit("consoleLamps", 2), Is.False, "Rods.");
            Assert.That(Lit("routeOneLamps", 2), Is.True); Assert.That(Lit("routeOneLamps", 3), Is.False, "Dark beyond the collapse.");
            Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text, Is.EqualTo("NO FEED"));
            Assert.That(GameObject.Find("2176 truss collapse").activeInHierarchy, Is.True);
            Assert.That(GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>().text, Does.Contain("B has no feed in 2176."));
            var rod = ((Transform[])Get(Circuit, "controlRods"))[0]; float floating = rod.position.y;
            SelectRoute(1); Interact(); Assert.That((int)Prop(Circuit, "Route"), Is.EqualTo(1), "The selector has seized by 2176.");
            Assert.That(GameObject.Find("Gravity switch"), Is.Null, "So has the gravity console.");
            // Back to 2026: the surviving route, and station gravity on.
            yield return Swap("Past");
            SelectRoute(2); ToggleGravity(); yield return null;
            Assert.That(StationGravity, Is.GreaterThan(4.5f)); Assert.That(rod.position.y, Is.LessThan(floating - .5f), "Rods seat.");
            Assert.That(Lit("routeTwoLamps", 4), Is.True); Assert.That(Lit("routeOneLamps", 0), Is.False);
            CheckObjective("03", "Activate the Reactor"); AimAtReactor(); RejectReactor();
            Assert.That((string)Prop(Reactor, "InteractionHint"), Does.Contain("Return to the Future"));
            yield return Swap("Future"); yield return null;
            Assert.That(Lit("consoleLamps", 1), Is.True); Assert.That(Lit("consoleLamps", 2), Is.True);
            Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text, Is.EqualTo("REPAIR COMPLETE"));
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
            Assert.That(StationGravity, Is.GreaterThan(4.5f)); Assert.That((int)Prop(Circuit, "Route"), Is.EqualTo(1));
            Assert.That(Vector3.Distance(player.position, new Vector3(0, 1.05f, -8.2f)), Is.LessThan(.25f));
            Assert.That(controller.enabled, Is.True); Assert.That(player.isKinematic, Is.False);
            var before = player.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Assert.That(Vector3.Distance(before, player.position), Is.GreaterThan(.2f));
        }

        [UnityTest] public IEnumerator PartialPathsCannotActivateOrUseTutorialCompletionShortcut()
        {
            yield return Install(); ToggleGravity(); yield return null; // A only, gravity restored.
            yield return Swap("Future"); AimAtReactor(); RejectReactor();
            Assert.That((bool)Prop(Presentation, "ReactorReady"), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.K)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((bool)Prop(Completion, "IsOpen"), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            yield return Swap("Past"); ToggleGravity(); yield return null; RepairB(); SelectRoute(2); // Route right, gravity off.
            yield return Swap("Future"); AimAtReactor(); RejectReactor();
            Assert.That(GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>().text, Does.Contain("Control rods are not seated."));
        }

        [UnityTest] public IEnumerator CompletedReactorContinuesToCargoAndReentryStartsFresh()
        {
            yield return SolveIn2026(); yield return Swap("Future");
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
            yield return SolveIn2026(); yield return Swap("Future");
            yield return ActivateAndCheckCompletion();
        }
    }
}
