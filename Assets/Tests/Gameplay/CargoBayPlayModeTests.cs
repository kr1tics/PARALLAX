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
            var canvas = (GameObject.Find("Cargo Bay Objective UI") ?? GameObject.Find("Power Restoration Objective UI")).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/cargo/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(image); Object.Destroy(target);
        }

        string CargoNotice => GameObject.Find("Interaction result").GetComponent<TMPro.TMP_Text>().text;
        void ClearCargoNotice() => Call(Get(level, "feedback"), "Clear");
        void AimCargoSurface(Collider surface, Vector3 outward, float gap = .65f)
        {
            body.constraints = RigidbodyConstraints.FreezeAll;
            var bounds = surface.bounds;
            float radius = Mathf.Abs(outward.x) * bounds.extents.x + Mathf.Abs(outward.z) * bounds.extents.z;
            Vector3 origin = bounds.center + outward * (radius + gap);
            body.position = origin - Vector3.up * .2f; player.transform.position = body.position;
            var direction = -outward;
            Call(Component(Camera.main.gameObject, "Look"), "Rotate", new Vector2(Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg, 0));
            Camera.main.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction)); Physics.SyncTransforms();
        }
        Collider CargoSurface(string name) => GameObject.Find(name).GetComponent<Collider>();
        void CaptureCargoGuidance(string name)
        {
            Directory.CreateDirectory(".utmp/cargo-guidance");
            var canvas = GameObject.Find("Cargo Bay Objective UI").GetComponent<Canvas>(); var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/cargo-guidance/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(image); Object.Destroy(target);
        }
        [UnityTest] public IEnumerator CargoFailureHintsRequireActualAttemptsAndProgressSeparately()
        {
            var gate = ((Component)Get(level, "freightDoor")).GetComponent<Collider>();
            AimCargoSurface(gate, Vector3.back, 4); yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.Empty);
            AimCargoSurface(gate, Vector3.back); yield return new WaitForSeconds(.3f); Assert.That(CargoNotice, Is.Empty);
            var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.transform.position = Camera.main.transform.position + Camera.main.transform.forward * .3f;
            obstruction.transform.localScale = Vector3.one * .15f; Physics.SyncTransforms();
            yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.Empty);
            Object.Destroy(obstruction); yield return null;
            AimCargoSurface(gate, Vector3.back); yield return Keys(.06f, Key.E);
            Assert.That(CargoNotice, Is.EqualTo("Something must be keeping this gate locked."));
            yield return Keys(.3f, Key.E); Assert.That(CargoNotice, Is.EqualTo("There must be something controlling it."));
            int attempts = ((int[])Get(level, "failedAttempts"))[1];
            yield return new WaitForSeconds(.3f); Assert.That(((int[])Get(level, "failedAttempts"))[1], Is.EqualTo(attempts));
            ClearCargoNotice(); var cage = CargoSurface("Sealed freight cage front"); AimCargoSurface(cage, Vector3.back);
            yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.EqualTo("This cargo won't budge..."),
                "Ray=" + ((Tuple<bool, RaycastHit>)Call(Interaction, "fireInteractRay", false)).Item2.collider?.name +
                "; Future-only=" + string.Join(",", ((GameObject[])Get(level, "futureOnly")).Select(x => x.name)));
            yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.EqualTo("Maybe things were different in the past."));
            ClearCargoNotice(); var rack = Rigid("rackFuture").GetComponent<Collider>(); AimCargoSurface(rack, Vector3.right);
            yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.EqualTo("Something's holding C2 in place."));
            yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.EqualTo("I wonder what releases the clamp."));
            ClearCargoNotice(); var collapse = ((GameObject)Get(level, "futureDebris")).GetComponentInChildren<Collider>(); AimCargoSurface(collapse, Vector3.right);
            yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.EqualTo("Looks like this section has collapsed."));
            yield return Keys(.06f, Key.E); Assert.That(CargoNotice, Is.EqualTo("I wonder what this area looked like before..."));
            yield return new WaitForSeconds(.2f); CaptureCargoGuidance("collapse-hint");
            ClearCargoNotice(); Place(Rigid("futureCrate"), new Vector3(-7, -.2f, -10));
            AimCargoSurface(cage, Vector3.back); yield return Keys(.06f, Key.E);
            Assert.That(CargoNotice, Is.Empty, "An empty cage must not report an immovable cargo unit.");
            GameSettingsMenu.Instance.Open(); yield return null;
            Assert.That((float)Get(level, "feedback").GetType().GetProperty("Opacity").GetValue(Get(level, "feedback")), Is.Zero);
            GameSettingsMenu.Instance.Close(); yield return null; yield return null; Call(level, "ResetCurrentPuzzle"); yield return null;
            Assert.That(((int[])Get(level, "failedAttempts")).All(x => x == 0), Is.True); Assert.That(CargoNotice, Is.Empty);
        }
        [UnityTest] public IEnumerator RackReclampingHintNeedsPreviouslyHandledCargoAndDoesNotRepeatWhileNearby()
        {
            body.constraints = RigidbodyConstraints.FreezeAll; yield return Swap();
            var c1 = Rigid("pastCrate"); var c2 = Rigid("rackPast");
            Place(c1, P); yield return new WaitForSeconds(.6f);
            Place(c1, new Vector3(-8, -.2f, 0)); yield return new WaitForSeconds(.6f);
            Assert.That(CargoNotice, Is.Empty, "An untouched rack does not announce a failed carry.");
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Place(c1, P); yield return new WaitForSeconds(.7f); Assert.That(c2.isKinematic, Is.False);
                AimCargoSurface(c2.GetComponent<Collider>(), Vector3.right); yield return Keys(.06f, Key.E);
                Assert.That(Held, Is.Not.Null); Call(Interaction, "DropHeldObject");
                Place(c1, new Vector3(-8, -.2f, 0)); yield return new WaitForSeconds(.5f);
                Assert.That(c2.isKinematic, Is.True); Assert.That(Flag("RackReleased"), Is.False);
                Assert.That(CargoNotice, Is.EqualTo(cycle == 0 ? "Not quite free yet." : "I need to move it clear of the rack."));
                int count = ((int[])Get(level, "failedAttempts"))[3]; yield return new WaitForSeconds(.4f);
                Assert.That(((int[])Get(level, "failedAttempts"))[3], Is.EqualTo(count));
                ClearCargoNotice();
            }
        }
        [UnityTest] public IEnumerator CargoGuidanceNamesTheCurrentProblemWithoutReannouncingMinorChanges()
        {
            var detail = GameObject.Find("Current instruction").GetComponent<TMPro.TMP_Text>();
            var compact = GameObject.Find("Compact objective").GetComponent<TMPro.TMP_Text>();
            Assert.That(detail.text, Is.EqualTo("Freight access: locked"));
            Assert.That(compact.text, Does.StartWith("RESTORE FREIGHT ACCESS\n"));
            yield return Swap();
            level.GetType().GetField("<FreightAccessGranted>k__BackingField", Fields).SetValue(level, true);
            yield return null; yield return null;
            Assert.That(detail.text, Is.EqualTo("C2: secured"));
            int revision = (int)level.GetType().GetProperty("ObjectiveRevision").GetValue(level);
            Place(Rigid("pastCrate"), P); yield return new WaitForSeconds(.7f);
            Assert.That(Pressed("counterweight"), Is.True);
            Assert.That(detail.text, Is.EqualTo("C2: clamp released")); Assert.That(CargoNotice, Is.EqualTo("Looks like that worked."));
            Assert.That((int)level.GetType().GetProperty("ObjectiveRevision").GetValue(level), Is.EqualTo(revision));
            yield return new WaitForSeconds(3); Assert.That(CargoNotice, Is.Empty);
            Place(Rigid("rackPast"), new Vector3(-8, -.2f, 0)); yield return new WaitForSeconds(.4f);
            Assert.That(compact.text, Does.StartWith("COMPLETE THE CARGO TRANSFER\n"));
            Assert.That(detail.text, Is.EqualTo("C1: pending · C2: pending"));
            Assert.That(CargoNotice, Is.EqualTo("Both cargo units need to be delivered."));
            revision = (int)level.GetType().GetProperty("ObjectiveRevision").GetValue(level);
            yield return Swap(); Place(Rigid("futureCrate"), R1); yield return new WaitForSeconds(.8f);
            Assert.That(Pressed("plate"), Is.True); Assert.That(CargoNotice, Is.EqualTo("One down, one to go."));
            Assert.That(detail.text, Is.EqualTo("C1: received · C2: pending"));
            Assert.That(compact.text, Does.Not.Contain("One down"));
            Assert.That((int)level.GetType().GetProperty("ObjectiveRevision").GetValue(level), Is.EqualTo(revision));
            yield return new WaitForSeconds(3); Assert.That(CargoNotice, Is.Empty);
            Place(Rigid("futureCrate"), new Vector3(10, -.2f, 10)); yield return new WaitForSeconds(.6f);
            Place(Rigid("futureCrate"), R1); yield return new WaitForSeconds(.8f); Assert.That(CargoNotice, Is.Empty);
            Place(Rigid("rackFuture"), R2); yield return new WaitForSeconds(.8f);
            Assert.That(Flag("CargoDelivered"), Is.True); Assert.That(CargoNotice, Is.EqualTo("The way forward is open."));
            Assert.That(GameObject.Find("Objective title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Reach Maintenance Access"));
            Assert.That(detail.text, Is.EqualTo("Cargo transfer: complete"));
            Call(level, "ResetCurrentPuzzle"); yield return null; yield return null;
            Assert.That(detail.text, Is.EqualTo("Freight access: locked")); Assert.That(CargoNotice, Is.Empty);
            Assert.That((int)Get(level, "receivingSeen"), Is.Zero);
        }
        [UnityTest] public IEnumerator CentralFreightKeepsOriginalWoodAndOtherVisualPolishRemains()
        {
            var containers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(x => x.name == "Freight container").ToArray();
            Assert.That(containers.Length, Is.EqualTo(6));
            foreach (var container in containers) Assert.That(container.sharedMaterial.name, Is.EqualTo("Small Crate_diffuse"));
            Assert.That(GameObject.Find("Container latch"), Is.Null); Assert.That(GameObject.Find("Container recessed seam"), Is.Null);
            Assert.That(GameObject.Find("Freight container reinforcement"), Is.Null);
            Assert.That(CargoSurface("Sealed freight cage front").GetComponent<Renderer>().sharedMaterial.name, Is.EqualTo("CargoContainmentField"));
            Assert.That(GameObject.Find("A freight plate"), Is.Not.Null); yield return null;
        }
        [UnityTest] public IEnumerator CargoObjectiveCardsAreVioletConciseAndOnlyExpandForMajorStages()
        {
            var title = GameObject.Find("Objective title").GetComponent<TMPro.TMP_Text>();
            Assert.That(title.text, Is.EqualTo("Restore Freight Access"));
            Assert.That(GameObject.Find("Objective number").GetComponent<TMPro.TMP_Text>().color, Is.EqualTo((Color)new Color32(181,160,255,255)));
            var document = GameSettingsMenu.Instance.GetComponent<UnityEngine.UIElements.UIDocument>();
            var launcher = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(document.rootVisualElement, "open-settings");
            Assert.That(launcher.style.left.value.value, Is.EqualTo(24)); Assert.That(launcher.style.top.value.value, Is.EqualTo(24));
            CaptureCargoGuidance("freight-expanded");
            int revision = (int)level.GetType().GetProperty("ObjectiveRevision").GetValue(level);
            yield return new WaitForSeconds(5.5f);
            Assert.That((bool)level.GetType().GetProperty("ObjectiveExpanded").GetValue(level), Is.False);
            CaptureCargoGuidance("freight-compact"); yield return Swap();
            Assert.That((int)level.GetType().GetProperty("ObjectiveRevision").GetValue(level), Is.EqualTo(revision));
            level.GetType().GetField("<FreightAccessGranted>k__BackingField", Fields).SetValue(level, true); yield return null; yield return null;
            Assert.That(title.text, Is.EqualTo("Release the Secured Cargo"));
            Place(Rigid("rackPast"), new Vector3(-8, -.2f, 0)); yield return null; yield return null;
            Assert.That(title.text, Is.EqualTo("Complete the Cargo Transfer"));
            level.GetType().GetField("<CargoDelivered>k__BackingField", Fields).SetValue(level, true); yield return null; yield return null;
            Assert.That(title.text, Is.EqualTo("Reach Maintenance Access"));
            var canvas = GameObject.Find("Cargo Bay Objective UI").GetComponent<Canvas>();
            foreach (var size in new[] { new Vector2Int(960,540), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(1920,1080) })
            {
                var target = new RenderTexture(size.x, size.y, 24); Camera.main.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = .5f;
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))
                { text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.name + " at " + size); }
                Camera.main.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay; Object.Destroy(target);
            }
            Call(level, "ResetCurrentPuzzle"); yield return null; yield return null;
            Assert.That(title.text, Is.EqualTo("Restore Freight Access"));
        }

        [UnityTest] public IEnumerator CargoVisualReviewViews()
        {
            body.constraints = RigidbodyConstraints.FreezeAll; player.enabled = false;
            var gravity = Component(player.gameObject, "ArtificialGravity"); gravity.enabled = false;
            void View(Vector3 position, Vector3 target)
            {
                body.position = position; player.transform.position = position; Physics.SyncTransforms();
                Vector3 origin = position + Vector3.up * .2f;
                Camera.main.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(target - origin));
            }
            var canvas = GameObject.Find("Cargo Bay Objective UI").GetComponent<Canvas>(); canvas.enabled = false;
            View(new Vector3(0, .02f, -15), new Vector3(0, .7f, 0)); yield return null; CaptureCargoGuidance("visual-arrival-future");
            View(new Vector3(-10, .9f, -15.3f), new Vector3(-14, 1, -11)); yield return null; CaptureCargoGuidance("visual-c1-enclosure");
            View(new Vector3(-10.4f, 1.9f, .5f), new Vector3(-14.6f, 2.65f, 2.6f)); yield return null; CaptureCargoGuidance("visual-c2-future");
            View(new Vector3(-9.8f, .8f, -5), new Vector3(-10.5f, -.48f, -4.2f)); yield return null; CaptureCargoGuidance("visual-a-station");
            var state = State; yield return new WaitForSeconds(1.05f); Call(state, "SwapTime"); yield return new WaitForSeconds(1.2f);
            Assert.That(Timeline, Is.EqualTo("Past"));
            View(new Vector3(-10.5f, .15f, -10.5f), new Vector3(-10.5f, -.45f, -9)); yield return null; CaptureCargoGuidance("visual-c1-past");
            View(new Vector3(-11, 2.3f, 0), new Vector3(-14.6f, 2.6f, 2.6f)); yield return null; CaptureCargoGuidance("visual-c2-past");
            View(new Vector3(13.5f, .55f, 11.1f), new Vector3(13.5f, 1, 13.2f)); yield return null; CaptureCargoGuidance("visual-r1-station");
            View(new Vector3(-10.2f, .7f, 10), new Vector3(-11.925f, 1.1f, 11.55f)); yield return null; CaptureCargoGuidance("visual-r2-station");
            canvas.enabled = true;
        }
        [UnityTest] public IEnumerator CargoVisualAdditionsAreColliderFreeAndSignsStayContainedAcrossTimelines()
        {
            var finish = GameObject.Find("Cargo Bay / equipment finish"); Assert.That(finish, Is.Not.Null);
            Assert.That(finish.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(finish.GetComponentsInChildren<Light>(true), Is.Empty);
            var signage = Component(level.gameObject, "CargoBaySignage");
            foreach (var text in (TMPro.TMP_Text[])Get(signage, "legacyTexts")) Assert.That(text.enabled, Is.False, text.name);
            Assert.That(GameObject.Find("Mezzanine light").GetComponent<Light>().type, Is.EqualTo(LightType.Spot));
            for (int timeline = 0; timeline < 2; timeline++)
            {
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas.renderMode != RenderMode.WorldSpace || !canvas.transform.IsChildOf(level.transform)) continue;
                    foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>())
                    { text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, canvas.name + "/" + text.name); }
                }
                foreach (var text in (TMPro.TMP_Text[])Get(signage, "legacyTexts")) Assert.That(text.enabled, Is.False);
                foreach (var crate in new[] { Rigid("pastCrate"), Rigid("futureCrate"), Rigid("rackPast"), Rigid("rackFuture") })
                foreach (var canvas in crate.GetComponentsInChildren<Canvas>(true))
                    Assert.That(canvas.transform.lossyScale.x, Is.EqualTo(.01f).Within(.00001f));
                var surfaces = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(x => x.name == "Floor_01");
                foreach (var surface in surfaces)
                    Assert.That(surface.sharedMaterial.name, Is.EqualTo(timeline == 0 ? "CargoFloorFuture" : "Diffuse_01"));
                if (timeline == 0) yield return Swap();
            }
            var r1Plate = GameObject.Find("R1 receiving plate").transform;
            var origin = new Vector3(13.5f, 1.05f, 11.1f); var direction = r1Plate.position - origin;
            Assert.That(Physics.RaycastAll(origin, direction.normalized, direction.magnitude - .005f, ~0, QueryTriggerInteraction.Ignore)
                .Where(hit => !hit.collider.transform.IsChildOf(player.transform)), Is.Empty, "R1 plaque must sit in front of the wall.");
            Assert.That(GameObject.Find("A freight plate"), Is.Not.Null); Assert.That(GameObject.Find("P counterweight plate"), Is.Not.Null);
            Assert.That(GameObject.Find("R1 receiving plate"), Is.Not.Null); Assert.That(GameObject.Find("R2 receiving plate"), Is.Not.Null);
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

        // Every crate names its manifest on all four sides and the top; each scale is signed directly behind itself.
        [UnityTest] public IEnumerator ManifestsAndScalesAreLabelled()
        {
            foreach (var (field, name) in new[] { ("pastCrate", "C1"), ("futureCrate", "C1"), ("rackPast", "C2"), ("rackFuture", "C2") })
            {
                var labels = Rigid(field).GetComponentsInChildren<TMPro.TextMeshPro>(true);
                Assert.That(labels.Length, Is.EqualTo(5), field);
                Assert.That(labels.All(l => l.text == name), Is.True, field + " is " + name);
            }
            foreach (var (label, plate) in new[] { ("Cargo pad label", "plate"), ("R2 label", "rackPlate") })
            {
                var sign = GameObject.Find(label).GetComponent<TMPro.TMP_Text>();
                var at = ((Component)Get(level, plate)).transform.position;
                Assert.That(sign.text, Does.StartWith(plate == "plate" ? "R1" : "R2"));
                Assert.That(Vector2.Distance(new Vector2(sign.transform.position.x, sign.transform.position.z), new Vector2(at.x, at.z)), Is.LessThan(3.2f), label + " is beside its scale.");
            }
            body.position = new Vector3(-10.5f, .02f, -11); yield return new WaitForSeconds(.3f);
            yield return Face(180, -.05f); Capture("label-c1-cage");
            body.position = new Vector3(11, .02f, 8.4f); yield return new WaitForSeconds(.3f);
            yield return Face(61, .02f); Capture("label-r1");
            body.position = new Vector3(-10, .02f, 2.6f); yield return new WaitForSeconds(.3f);
            yield return Face(180, .05f); Capture("label-c2-rack");
        }

        // Phase 4: Cargo Bay uses the tutorial / Power Restoration GameplayHud layout, fonts, wording and visibility rules.
        [UnityTest] public IEnumerator PuzzleHudPreservesCopyAndUsesTutorialControls()
        {
            foreach (var scene in new[] { "Level2_CargoBay", "Level1_PowerRestoration" })
            {
                if (SceneManager.GetActiveScene().name != scene)
                {
                    #if UNITY_EDITOR
                    yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
                    yield return SceneManager.LoadSceneAsync(scene);
#endif
                    yield return null; yield return null;
                    player = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == "PlayerController");
                }
                var ui = GameObject.Find(scene == "Level2_CargoBay" ? "Cargo Bay Objective UI" : "Power Restoration Objective UI");
                var labels = ui.GetComponentsInChildren<TMPro.TMP_Text>(true);
                var heading = labels.Single(t => t.name == "Objective title");
                var detail = labels.Single(t => t.name == "Current instruction");
                string titleCopy = heading.text, detailCopy = detail.text;
                Capture(scene + "-expanded");
                Assert.That(titleCopy, Is.Not.Empty); Assert.That(detailCopy, Is.Not.Empty);
                var rect = (RectTransform)ui.transform.Find("Objective panel");
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, 1)));
                Assert.That((bool)Get(Interaction, "automaticInteractionEffects"), Is.False);
                var original = Timeline;
                yield return Keys(.06f, Key.F); yield return new WaitForSeconds(.7f);
                Assert.That(Timeline, Is.EqualTo(original));
                yield return Swap(); Assert.That(Timeline, Is.Not.EqualTo(original));
                yield return Swap(); Assert.That(Timeline, Is.EqualTo(original));
                yield return new WaitForSeconds(4);
                Assert.That(rect.sizeDelta.y, Is.EqualTo(110).Within(.1f));
                Capture(scene + "-compact");
                Assert.That(heading.text, Is.EqualTo(titleCopy));
                Assert.That(detail.text, Is.EqualTo(detailCopy));
                var doc = GameSettingsMenu.Instance.GetComponent<UnityEngine.UIElements.UIDocument>();
                var button = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(doc.rootVisualElement, "open-settings");
                Assert.That(button.resolvedStyle.top, Is.EqualTo(24).Within(1));
                foreach (var key in new[] { Key.Escape, Key.P })
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.06f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                    Assert.That(GameSettingsMenu.Instance.IsOpen, Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.06f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                    Assert.That(GameSettingsMenu.Instance.IsOpen, Is.False);
                }
                var prompt = labels.Single(t => t.name == "Contextual interaction prompt");
                Assert.That(prompt.color.r, Is.GreaterThan(prompt.color.b));
            }
        }

        [UnityTest] public IEnumerator HudMatchesSharedLayoutAndReportsStageState()
        {
            Assert.That(GameObject.Find("Cargo HUD"), Is.Null, "The bespoke 1280x720 HUD is gone.");
            var ui = GameObject.Find("Cargo Bay Objective UI");
            var scaler = ui.GetComponent("CanvasScaler");
            Assert.That((Vector2)scaler.GetType().GetProperty("referenceResolution").GetValue(scaler), Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(((RectTransform)ui.transform.Find("Objective panel")).anchorMin, Is.EqualTo(new Vector2(.5f, 1)));
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
            yield return null; yield return null;
            Assert.That(ui.transform.Find("Interaction background").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(ui.transform.Find("Interaction background/Interaction keycap").gameObject.activeSelf, Is.True);
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
