using System.Collections;
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
    public class PowerModuleBTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MonoBehaviour module, interaction, controller, gravity;
        Rigidbody player;
        object state;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static MonoBehaviour Component(GameObject go, string name) => go.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == name);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Fields).SetValue(obj, value);
        static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        bool Repaired => (bool)Prop(module, "IsRepaired");
        bool Ready => (bool)Prop(module, "CanRepair");
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        MonoBehaviour ModuleA => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
        // A's own carry and seating are covered by PowerModuleATests; here it only has to feed B's junction.
        void InstallA() => ModuleA.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(ModuleA, true);

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
            module = Component(GameObject.Find("Repair panel"), "PowerModuleB");
            player = GameObject.Find("Player").GetComponent<Rigidbody>();
            interaction = Component(player.gameObject, "Interact"); controller = Component(player.gameObject, "PlayerController");
            gravity = Component(player.gameObject, "ArtificialGravity");
            state = module.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
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
        void Aim(float distance = 1.6f, float height = 11.8f)
        {
            controller.enabled = false; player.isKinematic = true;
            player.position = new Vector3(module.transform.position.x - distance, height, module.transform.position.z);
            player.transform.position = player.position;
            Camera.main.transform.SetPositionAndRotation(player.position, Quaternion.LookRotation(Vector3.right));
            Physics.SyncTransforms();
        }
        void Reject()
        {
            Assert.That(Ready, Is.False);
            Call(interaction, "TryInteract"); Call(module, "onInteract", interaction);
            Assert.That(Repaired, Is.False);
        }

        [UnityTest] public IEnumerator RealQSpaceAndEInputsReachAndRepairTheElevatedPanel()
        {
            Keys(Key.T); yield return null; Keys(); yield return new WaitForSeconds(.7f);
            InstallA();
            player.position = new Vector3(6.5f, 1.05f, 1.4f); player.linearVelocity = Vector3.zero;
            Call(Component(Camera.main.gameObject, "Look"), "Rotate", new Vector2(0, 0));
            Physics.SyncTransforms(); yield return null;
            Keys(Key.Q); yield return null; Keys(); yield return null;
            Keys(Key.Space);
            float deadline = Time.time + 3;
            while (player.position.y < 11f && Time.time < deadline) yield return new WaitForFixedUpdate();
            Keys(); player.linearVelocity = Vector3.zero; // Stabilise the test at the panel after real Zero-G ascent.
            yield return null; yield return null;
            Assert.That(player.position.y, Is.GreaterThan(10.5f)); Assert.That(Ready, Is.True);
            Assert.That(GameObject.Find("Contextual interaction prompt").GetComponent<TMP_Text>().text, Is.EqualTo("E — Repair B-02"));
            Keys(Key.E); yield return null; yield return null; Keys();
            Assert.That(Repaired, Is.True);
            Assert.That(GameObject.Find("Interaction result").GetComponent<TMP_Text>().text, Is.EqualTo("Connection restored."));
            Assert.That(GameObject.Find("B / REPAIR").GetComponent<TextMesh>().text, Is.EqualTo("B / REPAIRED"));
            Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text, Is.EqualTo("REPAIR COMPLETE"));
            Assert.That(GameObject.Find("B / REPAIR").GetComponent<TextMesh>().color.g, Is.GreaterThan(.9f));
            Assert.That(GameObject.Find("Current instruction").GetComponent<TMP_Text>().text, Does.Contain("Both modules are repaired."));
            yield return null; Keys(Key.E); yield return null; Keys();
            Assert.That(Repaired, Is.True); Assert.That(Ready, Is.False, "Repeated interaction cannot repair twice.");
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("Module B repaired"));
        }

        [UnityTest] public IEnumerator RepairRequiresPastFeedHeightRangeClearRayAndUnpausedInput()
        {
            InstallA(); Aim(); Reject(); // Future.
            ModuleA.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(ModuleA, false);
            Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Aim(); Reject(); // No feed until module A is installed.
            Assert.That((string)Prop(module, "InteractionHint"), Is.EqualTo("No feed — module A offline"));
            InstallA();
            Aim(height: 1.05f); Reject(); // Floor.
            // From the floor this ray meets the route selector at the foot of B's wall; keep route 1 for the checks below.
            Call(Component(GameObject.Find("09 / Power path"), "PowerCircuit"), "SelectRoute", 1);
            Aim(); Set(gravity, "effective", true); Assert.That(Ready, Is.True, "Reaching the panel is enough; no extra zero-G rule.");
            Aim(2.7f); Reject(); // Outside the existing 2.5 m ray.
            Aim(2.4f); Assert.That(Ready, Is.True, "The existing range must remain usable.");
            Aim(); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (Camera.main.transform.position + module.transform.position) * .5f;
            Physics.SyncTransforms(); Reject();
            Object.Destroy(wall); yield return null; Physics.SyncTransforms();
            Assert.That(Ready, Is.True);
            GameSettingsMenu.Instance.Open(); Reject();
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            Assert.That(Ready, Is.True); Call(interaction, "TryInteract"); Assert.That(Repaired, Is.True);
            var presentation = Component(GameObject.Find("08 / Scene presentation"), "PowerRestorationPresentation");
            for (int cycle = 0; cycle < 4; cycle++)
            {
                yield return new WaitForSeconds(.5f); Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
                Assert.That(state.GetType().GetField("now").GetValue(state).ToString(), Is.EqualTo(cycle % 2 == 0 ? "Future" : "Past"));
                Call(interaction, "TryInteract"); Call(module, "onInteract", interaction); yield return null;
                Assert.That(Repaired, Is.True); Assert.That(Ready, Is.False, "A completed repair cannot be repeated in either timeline.");
                Assert.That((bool)Prop(presentation, "ReactorReady"), Is.False, "Route 1 does not survive to 2176.");
                Assert.That(GameObject.Find("B / REPAIR").GetComponent<TextMesh>().text, Is.EqualTo("B / REPAIRED"));
                Assert.That((Color32)GameObject.Find("B / REPAIR").GetComponent<TextMesh>().color,
                    Is.EqualTo((Color32)new Color(.35f, 1, .65f)));
                Assert.That(GameObject.Find("CONNECTION LOST").GetComponent<TextMesh>().text, Is.EqualTo(cycle % 2 == 0 ? "NO FEED" : "REPAIR COMPLETE"));
                Assert.That(GameObject.Find("Current instruction").GetComponent<TMP_Text>().text, Does.Contain("Both modules are repaired."));
                Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.name=="Contextual interaction prompt").text, Is.Empty);
                Assert.That(GameObject.Find("Interaction result").GetComponent<TMP_Text>().text, Is.Empty);
            }
        }
    }
}
