using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    // Rule tests set up a 2026 arrangement by placing copies directly and say so; they check rules, not playability.
    // The full solves use only movement, jetpack, carrying, interaction and time-switch input.
    public class ColdStorageTests
    {
        const int A = 0, B = 1;
        MonoBehaviour plant, interaction, controller, completion, look;
        Rigidbody player;
        Rigidbody[] past, future;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static readonly Vector3 S1 = new Vector3(0, .8f, 5), S2 = new Vector3(5, 7.75f, 5.8f), Pan = new Vector3(2.6f, .8f, 3), R = new Vector3(9.25f, 9.75f, 6);

        static object Prop(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name).Invoke(owner, args);
        static MonoBehaviour Script(GameObject owner, string name) => owner.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == name);
        bool Flag(string name) => (bool)Prop(plant, name);
        bool Usable(int cell) => (bool)Call(plant, "CellUsable", cell);
        string Reason(int cell) => (string)Call(plant, "CellReason", cell);
        string SiteOf(Rigidbody body) => Call(plant, "SiteOf", body.transform.position).ToString(); // inactive copies: Transform is authoritative
        bool Past => GlobalStateValue("now").ToString() == "Past";
        object GlobalStateValue(string field)
        {
            var state = plant.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            return state.GetType().GetField(field).GetValue(state);
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        Rigidbody Held => (Prop(interaction, "HeldObject") as Component)?.GetComponent<Rigidbody>();

        [UnitySetUp] public IEnumerator Setup()
        {
            background = InputSystem.settings.backgroundBehavior; editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keyboard.MakeCurrent(); mouse.MakeCurrent();
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1; AudioListener.pause = false; GameSettingsMenu.StartupInputBlocked = false;
            yield return SceneManager.LoadSceneAsync("Level3_ColdStorage"); yield return new WaitForSeconds(.3f);
            Bind();
        }
        void Bind()
        {
            plant = Script(GameObject.Find("Level 3 - Cold Storage"), "ColdStorageController");
            completion = Script(plant.gameObject, "TutorialCompletionScreen");
            player = GameObject.Find("Player").GetComponent<Rigidbody>();
            controller = Script(player.gameObject, "PlayerController"); interaction = Script(player.gameObject, "Interact");
            look = Script(Camera.main.gameObject, "Look");
            past = new[] { Body("Cell A - 2026"), Body("Cell B - 2026") };
            future = new[] { Body("Cell A - 2176"), Body("Cell B - 2176") };
        }
        static Rigidbody Body(string name) => Resources.FindObjectsOfTypeAll<Rigidbody>().Single(b => b.name == name && b.gameObject.scene.IsValid());

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (completion != null) completion.enabled = false;
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.timeScale = 1; AudioListener.pause = false; GameSettingsMenu.StartupInputBlocked = false; yield return null;
        }

        // ------------------------------------------------------------ setup helpers (rule tests only)
        static IEnumerator Physics2() { yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null; }
        IEnumerator Teleport(Rigidbody body, Vector3 position)
        {
            body.position = position; body.transform.position = position;
            if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms(); yield return Physics2(); yield return new WaitForSeconds(.4f);
        }
        IEnumerator PlacePlayer(Vector3 position)
        {
            controller.enabled = false; player.isKinematic = true;
            player.position = position; player.transform.position = position; Physics.SyncTransforms(); yield return Physics2();
        }
        IEnumerator FreePlayer() { player.isKinematic = false; controller.enabled = true; yield return Physics2(); }
        IEnumerator SwapTo(bool toPast)
        {
            if (Past == toPast) yield break;
            var state = plant.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            Call(state, "SwapTime"); yield return new WaitForSeconds(1.15f);
            Assert.That(Past, Is.EqualTo(toPast));
        }
        void Operate(string kind)
        {
            var type = plant.GetType().Assembly.GetType("ColdStorageControl+Kind");
            Call(plant, "Operate", Enum.Parse(type, kind));
        }
        // A 2026 arrangement with a sealed, repaired loop: valve SERVICE, F3 repaired by the player at its height, then LOOP.
        IEnumerator SealLoop()
        {
            yield return SwapTo(true);
            if (!Flag("ServiceMode")) Operate("Valve");
            if (!Flag("FlangeRepaired")) { yield return PlacePlayer(new Vector3(2.9f, 4.6f, 6.6f)); Operate("Flange"); }
            Assert.That(Flag("FlangeRepaired"), Is.True);
            Operate("Valve"); Assert.That(Flag("ServiceMode"), Is.False);
            yield return PlacePlayer(new Vector3(0, 1.05f, -8));
        }

        // ------------------------------------------------------------ rules
        [UnityTest] public IEnumerator StartStateIsTheRecordOfThe2026Start()
        {
            Assert.That(Past, Is.False);
            Assert.That(Usable(A), Is.False); Assert.That(Reason(A), Is.EqualTo("ACTIVE CHILL FAILED")); Assert.That(SiteOf(future[A]), Is.EqualTo("Vault"));
            Assert.That(Usable(B), Is.False); Assert.That(Reason(B), Is.EqualTo("UNSEALED SOCKET")); Assert.That(SiteOf(future[B]), Is.EqualTo("S2"));
            Assert.That(Flag("HatchOpen"), Is.False); Assert.That(Flag("VaultOpen"), Is.True, "The 2176 vault door is buckled open.");
            Assert.That(Flag("LoopSealed"), Is.False); Assert.That(Flag("Ready"), Is.False);
            Assert.That(GameObject.Find("Objective title").GetComponent<TMP_Text>().text, Is.EqualTo("Restore Reactor Cooling"));
            yield return null;
        }

        [UnityTest] public IEnumerator ValveIsSeizedIn2176AndServiceOpensThe2026Vault()
        {
            Operate("Valve"); Assert.That(Flag("ServiceMode"), Is.False, "Seized in 2176.");
            yield return SwapTo(true); yield return Physics2();
            Assert.That(Flag("VaultOpen"), Is.False);
            Operate("Valve"); yield return Physics2(); Assert.That(Flag("VaultOpen"), Is.True);
            yield return PlacePlayer(new Vector3(2.9f, 4.6f, 6.6f)); Operate("Valve");
            Operate("Flange"); Assert.That(Flag("FlangeRepaired"), Is.False, "F3 cannot be repaired while the line is pressurised.");
            yield return PlacePlayer(new Vector3(0, 1.05f, -8)); yield return Physics2(); Assert.That(Flag("VaultOpen"), Is.False);
        }

        [UnityTest] public IEnumerator StorageOutcomesFollowTheirOwnDependencies()
        {
            // A in S1 with the loop vented, B in R with the pan empty.
            yield return SwapTo(true);
            Operate("Valve");
            yield return Teleport(past[A], S1); yield return Teleport(past[B], R);
            yield return SwapTo(false);
            Assert.That(Usable(A), Is.False); Assert.That(Reason(A), Is.EqualTo("BAY VENTED"));
            Assert.That(Usable(B), Is.True); Assert.That(Reason(B), Is.EqualTo("PASSIVE COLD"));
            yield return SwapTo(true); Operate("Valve");
            yield return SwapTo(false);
            Assert.That(Reason(A), Is.EqualTo("LOOP LEAKED AT F3"));
            yield return SealLoop(); yield return SwapTo(false);
            Assert.That(Usable(A), Is.True); Assert.That(Reason(A), Is.EqualTo("SEALED IN THE LOOP"));
        }

        // T1 + T2: a valve change re-places only the S1 cell; the R-preserved cell already delivered to S2 stays put and usable.
        [UnityTest] public IEnumerator UnrelatedValveChangeDoesNotTeleportADeliveredCell()
        {
            yield return SealLoop();
            yield return Teleport(past[A], S1); yield return Teleport(past[B], R);
            yield return SwapTo(false);
            Assert.That(Usable(A) && Usable(B), Is.True);
            yield return Teleport(future[B], S2); // present-day handling in 2176
            Assert.That(SiteOf(future[B]), Is.EqualTo("S2"));
            yield return SwapTo(true); yield return SwapTo(false); // T2: no change
            Assert.That(SiteOf(future[B]), Is.EqualTo("S2")); Assert.That(Usable(B), Is.True);
            yield return SwapTo(true); Operate("Valve"); yield return SwapTo(false); // T1
            Assert.That(SiteOf(future[B]), Is.EqualTo("S2"), "B's history does not depend on the valve.");
            Assert.That(Usable(B), Is.True);
            Assert.That(Usable(A), Is.False); Assert.That(Reason(A), Is.EqualTo("BAY VENTED"));
            Assert.That(Vector3.Distance(future[A].transform.position, past[A].transform.position), Is.LessThan(.3f), "A's history changed, so A' is re-placed.");
        }

        // T3 + T4: the pan holds R open; leaving a cell on it rewrites the R cell, putting it down and lifting it in one visit does not.
        [UnityTest] public IEnumerator BalancePanAffectsOnlyTheCellInR()
        {
            yield return SealLoop();
            yield return Teleport(past[B], R);
            yield return SwapTo(false); Assert.That(Usable(B), Is.True);
            yield return Teleport(future[B], S2);
            yield return SwapTo(true);
            yield return Teleport(past[A], Pan); Assert.That(Flag("HatchOpen"), Is.True);
            yield return Teleport(past[A], new Vector3(-6, .45f, 6.6f)); // T4: back where it was, same visit
            yield return SwapTo(false);
            Assert.That(SiteOf(future[B]), Is.EqualTo("S2"), "Same history: B' untouched."); Assert.That(Usable(B), Is.True);
            yield return SwapTo(true); yield return Teleport(past[A], Pan); yield return SwapTo(false); // T3
            Assert.That(Usable(B), Is.False); Assert.That(Reason(B), Is.EqualTo("HATCH LEFT OPEN"));
            Assert.That(SiteOf(future[B]), Is.EqualTo("R"), "B's history changed, so B' is re-placed in R.");
            Assert.That(Reason(A), Is.EqualTo("LEFT ON BALANCE PAN"));
        }

        // T8 + T9: present-day handling never changes condition; opening R in 2176 does not degrade its preserved cell.
        [UnityTest] public IEnumerator HandlingIn2176NeverChangesCondition()
        {
            yield return SealLoop();
            yield return Teleport(past[A], S1); yield return Teleport(past[B], R);
            yield return SwapTo(false);
            yield return Teleport(future[A], Pan);
            Assert.That(Flag("HatchOpen"), Is.True, "Borrowed A' lifts the hatch in 2176.");
            Assert.That(Usable(A), Is.True); Assert.That(Usable(B), Is.True, "Opening R now does not rewrite 150 years.");
            yield return Teleport(future[A], S1); Assert.That(Usable(A), Is.True);
            yield return SwapTo(true); yield return SwapTo(false);
            Assert.That(Usable(A) && Usable(B), Is.True);
            // A boiled-off copy stays boiled off wherever it is carried in 2176.
            yield return SwapTo(true); Operate("Valve"); yield return SwapTo(false);
            Assert.That(Usable(A), Is.False);
            yield return Teleport(future[B], S2); yield return Teleport(future[A], R);
            yield return SwapTo(true); yield return SwapTo(false);
            Assert.That(Usable(A), Is.False);
        }

        // The hatch never closes through the player or a cell, and an obstructed hatch is not sealed for 150 years.
        [UnityTest] public IEnumerator HatchObstructionHoldsItOpenAndBreaksPreservationUntilCleared()
        {
            yield return SealLoop();
            yield return Teleport(past[A], Pan); Assert.That(Flag("HatchOpen"), Is.True);
            past[B].isKinematic = true; // a cell wedged in the opening
            yield return Teleport(past[B], new Vector3(7.9f, 9.75f, 6));
            yield return Teleport(past[A], new Vector3(-6, .45f, 6.6f));
            Assert.That(Flag("HatchOpen"), Is.True, "The cell in the opening holds the hatch.");
            past[B].isKinematic = false; yield return Teleport(past[B], R);
            Assert.That(Flag("HatchOpen"), Is.False, "Cleared: the hatch closes.");
            yield return PlacePlayer(new Vector3(7.9f, 9.9f, 6)); // the player in the opening
            yield return Teleport(past[A], Pan); yield return Teleport(past[A], new Vector3(-6, .45f, 6.6f));
            Assert.That(Flag("HatchOpen"), Is.True, "The hatch never closes through the player.");
            yield return SwapTo(false);
            Assert.That(Usable(B), Is.False); Assert.That(Reason(B), Is.EqualTo("HATCH LEFT OPEN"));
            yield return SwapTo(true); yield return PlacePlayer(new Vector3(0, 1.05f, -8)); yield return Physics2();
            Assert.That(Flag("HatchOpen"), Is.False);
            yield return SwapTo(false);
            Assert.That(Usable(B), Is.True); Assert.That(Reason(B), Is.EqualTo("PASSIVE COLD"));
            Assert.That(SiteOf(future[B]), Is.EqualTo("R"));
        }

        // T7: switching while carrying releases the 2026 copy mid-air; R5.
        [UnityTest] public IEnumerator SwitchingWhileCarryingLeavesThe2026CopyUnstored()
        {
            yield return SwapTo(true); yield return FreePlayer(); yield return Tap(Key.Q);
            yield return Fly(new Vector3(-4, 1.3f, -4.8f), 90); Look(GameObject.Find("Plant valve").transform.position); yield return Tap(Key.E);
            yield return Fly(new Vector3(-6, 1.3f, -4.8f), 90); yield return Fly(new Vector3(-6, 1.3f, 4.6f), 90); yield return Grab(past[A]);
            yield return Fly(new Vector3(-6, 2.2f, 1.2f), 270);
            yield return Tap(Key.T); yield return new WaitForSeconds(1.2f);
            Assert.That(Held, Is.Null); Assert.That(Usable(A), Is.False); Assert.That(Reason(A), Is.EqualTo("UNSTORED"));
        }

        // R7: a lost copy returns alone to its start; the other cell keeps its history.
        [UnityTest] public IEnumerator LostCellReturnsAloneWithoutDisturbingTheOther()
        {
            yield return SealLoop(); yield return Teleport(past[B], R);
            yield return SwapTo(false); Assert.That(Usable(B), Is.True);
            yield return Teleport(future[B], S2);
            future[A].position = new Vector3(0, -5, 0); future[A].transform.position = future[A].position; yield return Physics2(); yield return null;
            Assert.That(Vector3.Distance(future[A].position, new Vector3(-6, .45f, 6.6f)), Is.LessThan(.3f));
            Assert.That(SiteOf(future[B]), Is.EqualTo("S2")); Assert.That(Usable(B), Is.True);
        }

        // Physics-aware version of the original occupant/doorway test: wait for the simulation after each teleport.
        [UnityTest] public IEnumerator VaultHoldsOpenForOccupantAndDoorwayCellThenClosesWhenClear()
        {
            yield return SwapTo(true); Operate("Valve");
            yield return PlacePlayer(new Vector3(-6, 1.05f, 5)); Operate("Valve"); yield return Physics2();
            Assert.That(Flag("VaultOpen"), Is.True, "Occupant inside.");
            yield return Teleport(past[A], new Vector3(-6, .45f, 3));
            yield return PlacePlayer(new Vector3(-6, 1.05f, 0)); yield return Physics2();
            Assert.That(Flag("VaultOpen"), Is.True, "Cell in the doorway.");
            yield return Teleport(past[A], new Vector3(-6, .45f, -1)); yield return Physics2();
            Assert.That(Flag("VaultOpen"), Is.False, "Clear: closes.");
            yield return PlacePlayer(new Vector3(-6, 1.05f, 2.2f)); yield return Physics2();
            Assert.That(Flag("VaultOpen"), Is.False, "Approaching a closed door does not open it.");
        }

        [UnityTest] public IEnumerator PauseBlocksControlsAndRResetsEverything()
        {
            yield return SwapTo(true); Operate("Valve"); yield return Teleport(past[A], Pan);
            var menu = GameSettingsMenu.Instance; menu.Open();
            Operate("Valve"); Call(plant, "ResetPlant"); Assert.That(Flag("ServiceMode"), Is.True);
            menu.Close(); yield return null; yield return null; yield return FreePlayer();
            yield return Tap(Key.R); yield return Physics2(); yield return new WaitForSeconds(.2f);
            Assert.That(Past, Is.False); Assert.That(Flag("ServiceMode") || Flag("FlangeRepaired") || Flag("HatchOpen"), Is.False);
            Assert.That(Vector3.Distance(past[A].transform.position, new Vector3(-6, .45f, 6.6f)), Is.LessThan(.2f));
            Assert.That(Vector3.Distance(player.position, new Vector3(0, 1.05f, -8)), Is.LessThan(.3f));
            Assert.That(Reason(A), Is.EqualTo("ACTIVE CHILL FAILED")); Assert.That(Reason(B), Is.EqualTo("UNSEALED SOCKET"));
        }

        // ------------------------------------------------------------ input-only play
        IEnumerator Tap(Key key) { Keys(key); yield return null; yield return null; Keys(); yield return null; yield return null; }
        IEnumerator Swap() { var was = Past; yield return Tap(Key.T); yield return new WaitForSeconds(1.15f); Assert.That(Past, Is.Not.EqualTo(was)); }
        void Look(Vector3 target)
        {
            var pivot = Camera.main.transform.parent.position; var d = target - pivot;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            Call(look, "Rotate", new Vector2(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, Mathf.Clamp(d.y / Mathf.Max(.1f, horizontal) * .25f, -.5f, .5f)));
        }
        // Jetpack flight with the view held level at a fixed heading, steering with strafe/forward/ascend/descend keys.
        IEnumerator Fly(Vector3 target, float heading)
        {
            Call(look, "Rotate", new Vector2(heading, 0));
            float deadline = Time.time + 30;
            while (Vector3.Distance(player.position, target) > .15f || player.linearVelocity.magnitude > .25f)
            {
                Assert.That(Time.time, Is.LessThan(deadline), "Flight blocked at " + player.position + " towards " + target);
                var desired = Vector3.ClampMagnitude((target - player.position) * 1.6f, 2.2f);
                var thrust = (desired - player.linearVelocity) * 4;
                var keys = new System.Collections.Generic.List<Key>();
                float lateral = Vector3.Dot(thrust, Camera.main.transform.right), forward = Vector3.Dot(thrust, Camera.main.transform.forward);
                if (Mathf.Abs(lateral) > .3f) keys.Add(lateral > 0 ? Key.D : Key.A);
                if (Mathf.Abs(forward) > .3f) keys.Add(forward > 0 ? Key.W : Key.S);
                if (Mathf.Abs(thrust.y) > .3f) keys.Add(thrust.y > 0 ? Key.Space : Key.LeftCtrl);
                Keys(keys.ToArray()); yield return new WaitForFixedUpdate();
            }
            Keys(); yield return new WaitForSeconds(.2f);
        }
        IEnumerator Grab(Rigidbody cell)
        {
            Debug.Log("SOLVE: grab " + cell.name + " from " + player.position);
            Look(cell.position); yield return new WaitForSeconds(.2f); yield return Tap(Key.E);
            Assert.That(Held, Is.SameAs(cell), "Could not grab " + cell.name + " from " + player.position);
        }
        // Moves the player until the carried cell hangs over the target (or, for R, sits at it), then releases it to fall.
        IEnumerator Drop(Vector3 target, float heading, bool exact = false)
        {
            var cell = Held; Debug.Log("SOLVE: drop at " + target + " holding " + (cell != null ? cell.name : "nothing") + " from " + player.position); Assert.That(cell, Is.Not.Null);
            Vector3 Error()
            {
                var e = target - cell.position;
                if (!exact) e.y = cell.position.y < target.y ? target.y - cell.position.y : cell.position.y > target.y + 1.1f ? target.y + .6f - cell.position.y : 0;
                return e;
            }
            for (int i = 0; i < 8 && Error().magnitude > .15f; i++)
            {
                yield return Fly(player.position + Error(), heading);
                yield return new WaitForSeconds(.5f);
            }
            Assert.That(Error().magnitude, Is.LessThan(.3f), "Carried cell not over " + target + ": " + cell.position);
            yield return Tap(Key.E); yield return new WaitForSeconds(1.2f);
        }
        IEnumerator Use(string name) { Look(GameObject.Find(name).transform.position); yield return new WaitForSeconds(.2f); yield return Tap(Key.E); }

        IEnumerator SolveWithInput(bool stagedRoles = false)
        {
            yield return Tap(Key.Q); // jetpack on for the whole route
            yield return Swap();
            yield return Fly(new Vector3(-4, 1.3f, -4.8f), 90); yield return Use("Plant valve");
            Assert.That(Flag("ServiceMode"), Is.True);
            yield return Fly(new Vector3(-6, 1.3f, -4.8f), 90); yield return Fly(new Vector3(-6, 1.3f, -1), 90); yield return Fly(new Vector3(-6, 1.3f, 4.6f), 90);
            yield return Grab(past[A]);
            yield return Fly(new Vector3(-6, 1.3f, -.6f), 270); yield return Fly(new Vector3(1.6f, 1.3f, -.6f), 90); // clear of the vault before turning
            yield return Drop(Pan + Vector3.up * .15f, 90);
            Assert.That(Flag("HatchOpen"), Is.True, "A on the pan lifts R's hatch.");
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(5, 8.5f, 4.2f), 90);
            yield return Grab(past[B]);
            yield return Fly(new Vector3(6.8f, 9.8f, 6), 0);
            yield return Drop(new Vector3(8.7f, 9.8f, 6), 0, true);
            yield return new WaitForSeconds(1);
            Assert.That(SiteOf(past[B]), Is.EqualTo("R"), "B slides to the back of R: " + past[B].position);
            yield return Fly(new Vector3(6, 9.8f, 6), 0); yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(2.2f, 1.6f, 1.4f), 90);
            yield return Grab(past[A]); yield return Physics2();
            Assert.That(Flag("HatchOpen"), Is.False, "Lifting A off the pan seals R.");
            yield return Drop(S1 + Vector3.up * .15f, 90);
            yield return Fly(new Vector3(2.2f, 4.6f, 6.4f), 90); yield return Fly(new Vector3(2.9f, 4.6f, 6.6f), 90); yield return Use("Flange F3");
            Assert.That(Flag("FlangeRepaired"), Is.True);
            yield return Fly(new Vector3(2.2f, 1.3f, 3), 90); yield return Fly(new Vector3(-1, 1.3f, -4.8f), 90); yield return Fly(new Vector3(-4, 1.3f, -4.8f), 90); yield return Use("Plant valve");
            Assert.That(Flag("ServiceMode"), Is.False);
            yield return Swap();
            Assert.That(Usable(A) && Usable(B), Is.True, "A sealed in S1 and B in R survive 150 years: " + Reason(A) + " / " + Reason(B));
            yield return Fly(new Vector3(-1, 1.3f, -4.8f), 90); yield return Fly(new Vector3(0, 1.3f, 3.2f), 90); yield return Grab(future[A]);
            yield return Drop(Pan + Vector3.up * .15f, 90);
            Assert.That(Flag("HatchOpen"), Is.True, "Borrowed A' lifts the hatch in 2176.");
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(7.05f, 9.8f, 6), 0);
            yield return Grab(future[B]);
            yield return Fly(new Vector3(5.2f, 9.8f, 6), 0); yield return Fly(new Vector3(5, 8.6f, 4.4f), 90);
            yield return Drop(S2 + Vector3.up * .15f, 90);
            Assert.That(Usable(B), Is.True);
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(2.2f, 1.6f, 1.4f), 90);
            yield return Grab(future[A]); yield return Drop(S1 + Vector3.up * .15f, 90);
            Assert.That(Usable(A), Is.True); Assert.That(Flag("Ready"), Is.True, "Both sockets usable, loop sealed.");
            yield return Fly(new Vector3(-2.8f, 1.3f, 3.2f), 90); yield return Use("Exchanger console");
            Assert.That(Flag("Completed"), Is.True);
            for (float t = 0; t < 12 && !(bool)Prop(completion, "IsReady"); t += Time.unscaledDeltaTime) yield return null; // 3 s start-up + reveal
            Assert.That((bool)Prop(completion, "IsReady"), Is.True);
        }

        [UnityTest] public IEnumerator FullSolveUsesOnlyMovementJetpackCarryInteractionAndTimeInput()
        {
            yield return SolveWithInput();
            Assert.That(GameObject.Find("Completion title").GetComponent<TMP_Text>().text, Is.EqualTo("STATION STABLE"));
        }

        // Legitimate alternative: B is the 2026 counterweight and A is flown into R; B ends sealed in S1.
        [UnityTest] public IEnumerator RoleSwappedSolveWithInput()
        {
            yield return Tap(Key.Q); yield return Swap();
            yield return Fly(new Vector3(-4, 1.3f, -4.8f), 90); yield return Use("Plant valve");
            yield return Fly(new Vector3(-1, 1.3f, -4.8f), 90); yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(5, 8.5f, 4.2f), 90);
            yield return Grab(past[B]);
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(2.2f, 1.6f, .8f), 90);
            yield return Drop(Pan + Vector3.up * .15f, 90);
            Assert.That(Flag("HatchOpen"), Is.True);
            yield return Fly(new Vector3(-1, 1.3f, -.6f), 90); yield return Fly(new Vector3(-6, 1.3f, -.6f), 90); yield return Fly(new Vector3(-6, 1.3f, 4.6f), 90);
            yield return Grab(past[A]);
            yield return Fly(new Vector3(-6, 1.3f, -.6f), 270); yield return Fly(new Vector3(1.8f, 1.6f, -.6f), 90);
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(6.8f, 9.8f, 6), 0);
            yield return Drop(new Vector3(8.7f, 9.8f, 6), 0, true); yield return new WaitForSeconds(1);
            Assert.That(SiteOf(past[A]), Is.EqualTo("R"));
            yield return Fly(new Vector3(6, 9.8f, 6), 0); yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(2.2f, 1.6f, 1.4f), 90);
            yield return Grab(past[B]); yield return Drop(S1 + Vector3.up * .15f, 90);
            yield return Fly(new Vector3(2.2f, 4.6f, 6.4f), 90); yield return Fly(new Vector3(2.9f, 4.6f, 6.6f), 90); yield return Use("Flange F3");
            yield return Fly(new Vector3(2.2f, 1.3f, 3), 90); yield return Fly(new Vector3(-1, 1.3f, -4.8f), 90); yield return Fly(new Vector3(-4, 1.3f, -4.8f), 90); yield return Use("Plant valve");
            yield return Swap();
            Assert.That(Usable(A) && Usable(B), Is.True, Reason(A) + " / " + Reason(B));
            yield return Fly(new Vector3(-1, 1.3f, -4.8f), 90); yield return Fly(new Vector3(0, 1.3f, 3.2f), 90); yield return Grab(future[B]);
            yield return Drop(Pan + Vector3.up * .15f, 90);
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(7.05f, 9.8f, 6), 0); yield return Grab(future[A]);
            yield return Fly(new Vector3(5.2f, 9.8f, 6), 0); yield return Fly(new Vector3(5, 8.6f, 4.4f), 90); yield return Drop(S2 + Vector3.up * .15f, 90);
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(2.2f, 1.6f, 1.4f), 90);
            yield return Grab(future[B]); yield return Drop(S1 + Vector3.up * .15f, 90);
            Assert.That(Flag("Ready"), Is.True);
            yield return Fly(new Vector3(-2.8f, 1.3f, 3.2f), 90); yield return Use("Exchanger console");
            Assert.That(Flag("Completed"), Is.True);
        }

        // Legitimate alternative: preserve B first, use the still boiled-off A' as the 2176 counterweight to deliver B',
        // then seal A in S1. The later valve and flange changes must not disturb the delivered B'.
        [UnityTest] public IEnumerator StagedSolveReusesTheBoiledOffCellAsCounterweight()
        {
            yield return Tap(Key.Q); yield return Swap();
            yield return Fly(new Vector3(-4, 1.3f, -4.8f), 90); yield return Use("Plant valve");
            yield return Fly(new Vector3(-6, 1.3f, -4.8f), 90); yield return Fly(new Vector3(-6, 1.3f, 4.6f), 90); yield return Grab(past[A]);
            yield return Fly(new Vector3(-6, 1.3f, -.6f), 270); yield return Fly(new Vector3(1.6f, 1.3f, -.6f), 90); yield return Drop(Pan + Vector3.up * .15f, 90);
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(5, 8.5f, 4.2f), 90); yield return Grab(past[B]);
            yield return Fly(new Vector3(6.8f, 9.8f, 6), 0); yield return Drop(new Vector3(8.7f, 9.8f, 6), 0, true); yield return new WaitForSeconds(1);
            yield return Fly(new Vector3(6, 9.8f, 6), 0); yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(2.2f, 1.6f, 1.4f), 90);
            yield return Grab(past[A]); yield return Drop(new Vector3(-1.5f, .95f, 1.2f), 90); // left on the open floor for now
            yield return Swap();
            Assert.That(Usable(B), Is.True); Assert.That(Reason(B), Is.EqualTo("PASSIVE COLD"));
            Assert.That(Usable(A), Is.False); Assert.That(Reason(A), Is.EqualTo("UNSTORED"));
            yield return Fly(new Vector3(-1.5f, 1.3f, -.4f), 90); yield return Grab(future[A]); yield return Drop(Pan + Vector3.up * .15f, 90);
            Assert.That(Flag("HatchOpen"), Is.True, "A boiled-off cell is still a counterweight.");
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(7.05f, 9.8f, 6), 0); yield return Grab(future[B]);
            yield return Fly(new Vector3(5.2f, 9.8f, 6), 0); yield return Fly(new Vector3(5, 8.6f, 4.4f), 90); yield return Drop(S2 + Vector3.up * .15f, 90);
            Assert.That(SiteOf(future[B]), Is.EqualTo("S2"));
            yield return Fly(new Vector3(2.2f, 8.5f, 2.2f), 90); yield return Fly(new Vector3(-1.5f, 1.6f, -.4f), 90);
            yield return Swap();
            yield return Grab(past[A]); yield return Drop(S1 + Vector3.up * .15f, 90);
            yield return Fly(new Vector3(2.2f, 4.6f, 6.4f), 90); yield return Fly(new Vector3(2.9f, 4.6f, 6.6f), 90); yield return Use("Flange F3");
            yield return Fly(new Vector3(2.2f, 1.3f, 3), 90); yield return Fly(new Vector3(-1, 1.3f, -4.8f), 90); yield return Fly(new Vector3(-4, 1.3f, -4.8f), 90); yield return Use("Plant valve");
            yield return Swap();
            Assert.That(SiteOf(future[B]), Is.EqualTo("S2"), "B's history did not change: B' stays delivered."); Assert.That(Usable(B), Is.True);
            Assert.That(Usable(A), Is.True); Assert.That(SiteOf(future[A]), Is.EqualTo("S1"), "A's history changed: A' re-placed, usable.");
            yield return Physics2(); Assert.That(Flag("Ready"), Is.True);
            yield return Fly(new Vector3(-1, 1.3f, -4.8f), 90); yield return Fly(new Vector3(-2.8f, 1.3f, 3.2f), 90); yield return Use("Exchanger console");
            Assert.That(Flag("Completed"), Is.True);
        }

        // Moving usable 2176 equipment cannot make one preservation site serve two cells.
        [UnityTest] public IEnumerator MovingAUsableFutureCellCannotStretchOneStorageSite()
        {
            yield return SealLoop(); yield return Teleport(past[A], S1);
            yield return SwapTo(false); Assert.That(Usable(A), Is.True);
            yield return Teleport(future[A], S2);
            yield return SwapTo(true);
            yield return Teleport(past[A], new Vector3(-1.5f, .45f, 1)); yield return Teleport(past[B], S1);
            yield return SwapTo(false);
            Assert.That(Usable(B), Is.True); Assert.That(SiteOf(future[B]), Is.EqualTo("S1"));
            Assert.That(Usable(A), Is.False); Assert.That(Reason(A), Is.EqualTo("UNSTORED"));
            Assert.That(SiteOf(future[A]), Is.EqualTo("Open"), "A's 2026 move rewrote the 2176 delivery.");
            yield return Physics2(); Assert.That(Flag("Ready"), Is.False);
        }

        // Wrong orders and refused actions, each with its recovery.
        [UnityTest] public IEnumerator WrongOrdersFailVisiblyAndRecover()
        {
            yield return SwapTo(true);
            Operate("Console"); Assert.That(Flag("Completed"), Is.False, "Nothing to start in 2026.");
            Operate("Valve"); yield return Physics2(); Assert.That(Flag("VaultOpen"), Is.True);
            Operate("Valve"); yield return Physics2(); Assert.That(Flag("VaultOpen"), Is.False, "LOOP with A still inside locks the vault.");
            Operate("Valve"); yield return Physics2(); Assert.That(Flag("VaultOpen"), Is.True, "SERVICE again reopens it.");
            Operate("Valve");
            yield return PlacePlayer(new Vector3(2.6f, 1.25f, 3)); yield return Physics2();
            Assert.That(Flag("HatchOpen"), Is.False, "Standing on the pan does not lift the hatch.");
            yield return PlacePlayer(new Vector3(0, 1.05f, -8));
            yield return SwapTo(false);
            yield return PlacePlayer(new Vector3(2.9f, 4.6f, 6.6f)); Operate("Flange");
            Assert.That(Flag("FlangeRepaired"), Is.False, "F3 is beyond repair in 2176.");
        }

        // ------------------------------------------------------------ narrative handover
        MonoBehaviour Memory() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).SingleOrDefault(m => m.GetType().Name == "MemoryRecoveryCinematic");
        IEnumerator SkipMemory() { Keys(); yield return new WaitForSecondsRealtime(.35f); Keys(Key.Space); yield return null; yield return null; Keys(); yield return null; }

        // Entry from Cargo Bay: its real Continue plays the Fragment memory, then loads a fresh Cold Storage with one Settings instance.
        [UnityTest] public IEnumerator CargoBayContinuePlaysFragmentThenLoadsAFreshColdStorage()
        {
            var menu = GameSettingsMenu.Instance;
            yield return SceneManager.LoadSceneAsync("Level2_CargoBay"); yield return new WaitForSeconds(.3f);
            var screen = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(m => m.GetType().Name == "TutorialCompletionScreen");
            Call(screen, "ShowCompletion"); // navigation only; Cargo Bay's own tests solve it with normal controls
            for (float t = 0; t < 6 && !(bool)Prop(screen, "IsReady"); t += Time.unscaledDeltaTime) yield return null;
            GameObject.Find("CONTINUE").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; yield return null;
            Assert.That(Prop(Memory(), "Stage").ToString(), Is.EqualTo("Fragment")); Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.True);
            yield return SkipMemory();
            for (float t = 0; t < 6 && SceneManager.GetActiveScene().name != "Level3_ColdStorage"; t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(.3f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level3_ColdStorage")); Assert.That(Memory(), Is.Null);
            Assert.That(GameSettingsMenu.Instance, Is.SameAs(menu)); Assert.That(Object.FindObjectsByType<GameSettingsMenu>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Bind(); Assert.That(Flag("Completed"), Is.False); Assert.That(Past, Is.False);
            Assert.That(Reason(A), Is.EqualTo("ACTIVE CHILL FAILED")); Assert.That(Reason(B), Is.EqualTo("UNSEALED SOCKET"));
        }

        // ------------------------------------------------------------ ending and presentation
        [UnityTest] public IEnumerator FinalCompletionOffersRestartAndContinue()
        {
            yield return SolveWithInput();
            var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Select(b => b.name).OrderBy(n => n).ToArray();
            Assert.That(buttons, Is.EqualTo(new[] { "CONTINUE", "RESTART LEVEL" }));
            Assert.That(GameObject.Find("Completion final message").GetComponent<TMP_Text>().text, Is.EqualTo("The core archive is cooling. Continue to recover the final memory."));
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t => t.text.Contains("not available")), Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            GameObject.Find("RESTART LEVEL").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; yield return null; yield return new WaitForSeconds(.3f);
            Bind(); Assert.That(Flag("Completed"), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(Reason(A), Is.EqualTo("ACTIVE CHILL FAILED"));
        }

        [UnityTest] public IEnumerator FinalContinuePlaysRestoredThenReturnsToMainMenu()
        {
            yield return SolveWithInput();
            GameObject.Find("CONTINUE").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; yield return null;
            var memory = Memory(); Assert.That(memory, Is.Not.Null);
            Assert.That(Prop(memory, "Stage").ToString(), Is.EqualTo("Restored")); Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level3_ColdStorage"));
            yield return SkipMemory();
            for (float t = 0; t < 12 && !(bool)Prop(memory, "EndCardReady"); t += Time.unscaledDeltaTime) yield return null;
            Assert.That((bool)Prop(memory, "EndCardReady"), Is.True); yield return new WaitForSecondsRealtime(2.5f);
            GameObject.Find("Return to Main Menu").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            for (float t = 0; t < 6 && SceneManager.GetActiveScene().name != "MainMenu"; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.False); Assert.That(GameSettingsMenu.StartupInputBlocked, Is.False);
        }

        // Main's puzzle HUD and mounted plates: Settings top-left, status top-right, no information board or floating slabs,
        // every displayed glyph present, and a cell's history explained once when it is picked up in 2176.
        [UnityTest] public IEnumerator HudMatchesThePuzzleLayoutAndPickupExplainsHistoryOnce()
        {
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var document = GameSettingsMenu.Instance.GetComponent<UnityEngine.UIElements.UIDocument>();
            var launcher = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(document.rootVisualElement, "open-settings");
            Assert.That(launcher.style.left.value.value, Is.EqualTo(24)); Assert.That(launcher.style.top.value.value, Is.EqualTo(24));
            var hud = GameObject.Find("Cold Storage Objective UI"); Assert.That(hud, Is.Not.Null);
            var status = hud.GetComponentsInChildren<TMP_Text>(true).Single(t => t.text.StartsWith("2176 / FUTURE"));
            Assert.That(status.rectTransform.position.x, Is.GreaterThan(Screen.width * .5f)); Assert.That(status.rectTransform.position.y, Is.GreaterThan(Screen.height * .5f));
            Assert.That(GameObject.Find("Plant board"), Is.Null);
            var backings = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.EndsWith(" backing") && t.name != "R hull backing").Select(t => t.name).ToArray();
            Assert.That(backings, Is.EqualTo(new[] { "Reactor loop repeater backing" }), "Only the reactor wall display keeps a backing.");
            Assert.That(GameObject.Find("Balance pan plate"), Is.Not.Null); Assert.That(GameObject.Find("R plate"), Is.Not.Null);
            foreach (bool era in new[] { false, true })
            {
                yield return SwapTo(era); yield return null;
                foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled && t.font != null))
                {
                    label.ForceMeshUpdate();
                    var shown = new string(label.GetParsedText().Where(c => !char.IsWhiteSpace(c)).ToArray());
                    Assert.That(label.font.HasCharacters(shown, out var missing, true, true), Is.True, label.name + " is missing " + string.Join(" ", (missing ?? new uint[0]).Select(u => "U+" + u.ToString("X4"))));
                }
            }
            yield return SwapTo(false);
            var feedback = Script(plant.gameObject, "PowerInteractionFeedback");
            yield return PlacePlayer(new Vector3(-6, 1.3f, 4.6f)); yield return FreePlayer(); yield return Tap(Key.Q); yield return Fly(new Vector3(-6, 1.3f, 4.6f), 90);
            yield return Grab(future[A]); yield return null;
            Assert.That(Prop(feedback, "Message"), Is.EqualTo("Cell A boiled off — the vault's chillers died with the power."));
            yield return Tap(Key.E); yield return new WaitForSeconds(1); Call(feedback, "Clear");
            yield return Grab(future[A]); yield return null;
            Assert.That(Prop(feedback, "Message"), Is.EqualTo(""), "An unchanged history is not repeated.");
        }

        [UnityTest] public IEnumerator HudAndSignageRenderWithoutOverflowInBothEras()
        {
            string output = Path.Combine(Application.dataPath, "../.utmp/cold-storage/renders"); Directory.CreateDirectory(output);
            var camera = Camera.main;
            foreach (bool era in new[] { false, true })
            {
                yield return SwapTo(era);
                foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1920, 1080) })
                {
                    var render = new RenderTexture(size.x, size.y, 24); render.Create(); camera.targetTexture = render;
                    var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
                    foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = .8f; }
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    foreach (var label in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled))
                    { label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False, label.name); }
                    camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = render;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, (era ? "2026" : "2176") + "-arrival-" + size.x + ".png"), image.EncodeToPNG());
                    RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); Object.Destroy(render);
                    foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
            var steps = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.text, @"\b\d+ / \d+\b") || t.text.Contains("STEP")).ToArray();
            Assert.That(steps, Is.Empty, "No printed solution steps.");
        }
    }
}
