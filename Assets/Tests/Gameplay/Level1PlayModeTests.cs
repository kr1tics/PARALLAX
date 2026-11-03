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
        [UnityTest] public IEnumerator LayoutReadabilityCapturesBothTimelines()
        {
            player.enabled = false; playerBody.isKinematic = true;
            player.GetComponent<Renderer>().enabled = false;
            var camera = Camera.main;
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>()) if (behaviour.GetType().Name == "Look") behaviour.enabled = false;
            string version = GameObject.Find("Tutorial navigation cladding") != null ? "after" : "before";
            camera.transform.position = new Vector3(3.5f, 1.4f, 7.5f);
            camera.transform.LookAt(new Vector3(0, 1, 10.6f));
            yield return null;
            Scene01PresentationTests.Capture(camera, "navigation-" + version + "-future", 1280, 720);
            yield return Swap();
            Scene01PresentationTests.Capture(camera, "navigation-" + version + "-past", 1280, 720);
            camera.transform.position = new Vector3(3.6f, 1.25f, 11.8f);
            camera.transform.LookAt(new Vector3(0, 1, 13.45f)); yield return null;
            Scene01PresentationTests.Capture(camera, "navigation-" + version + "-access-door", 1280, 720);
        }

        [UnityTest] public IEnumerator ObservationWindowShowsEarthAcrossTimelinesWithoutChangingBoundary()
        {
            var pane = GameObject.Find("Solid wall | Arrival airlock sealed").GetComponent<Renderer>();
            Assert.That(pane.sharedMaterial.name, Is.EqualTo("TutorialObservationGlass"));
            Assert.That(pane.sharedMaterial.color.a, Is.LessThan(.15f));
            Assert.That(GameObject.Find("Arrival airlock sealed").GetComponentInChildren<Collider>().enabled, Is.True);
            player.enabled = false; playerBody.isKinematic = true; player.GetComponent<Renderer>().enabled = false;
            var camera = Camera.main;
            foreach(var behaviour in camera.GetComponents<MonoBehaviour>()) if(behaviour.GetType().Name=="Look")behaviour.enabled=false;
            camera.transform.position = new Vector3(3.5f,.9f,8.95f);
            camera.transform.LookAt(GameObject.Find("Distant Earth").transform.position);
            yield return null;
            Scene01PresentationTests.Capture(camera,"observation-earth-future",1280,720);
            yield return Swap();
            Assert.That(pane.sharedMaterial.name, Is.EqualTo("TutorialObservationGlass"));
            Assert.That(pane.sharedMaterial.color.a, Is.LessThan(.15f));
            Scene01PresentationTests.Capture(camera,"observation-earth-past",1280,720);
        }

        [UnityTest] public IEnumerator WallFinishUsesStationTexturesAndFollowsTimeline()
        {
            var panel = GameObject.Find("Solid wall | Access bulkhead 3").GetComponent<Renderer>();
            var original = GameObject.Find("Access bulkhead 3").GetComponentInChildren<Renderer>();
            Assert.That(panel.sharedMaterial.name, Is.EqualTo("TutorialWallFinishFuture"));
            Assert.That(panel.sharedMaterial.mainTexture, Is.SameAs(original.sharedMaterial.mainTexture));
            yield return Swap();
            Assert.That(panel.sharedMaterial.name, Is.EqualTo("TutorialWallFinish"));
            Assert.That(panel.sharedMaterial.shader.name, Is.EqualTo("Standard"));
            Assert.That(panel.sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(panel.sharedMaterial.GetTexture("_MetallicGlossMap"), Is.Not.Null);
            var properties = new MaterialPropertyBlock(); panel.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_Color"), Is.EqualTo(new Color(.68f,.69f,.68f)));
            yield return Swap();
            Assert.That(panel.sharedMaterial.name, Is.EqualTo("TutorialWallFinishFuture"));
            panel.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_Color"), Is.EqualTo(new Color(.62f,.64f,.63f)));
            Assert.That(properties.GetFloat("_FlickerStrength"),Is.Zero,"Tutorial walls retain decay without flicker.");
            Assert.That(GameObject.Find("Panel seam | Access bulkhead 5").GetComponent<Renderer>().enabled,Is.False);
            Assert.That(GameObject.Find("Panel seam | Engineering bulkhead 2").GetComponent<Renderer>().enabled,Is.False);
            Assert.That(GameObject.Find("Industrial kickplate | Access bulkhead 3").GetComponent<Renderer>().sharedMaterial.name,
                Is.EqualTo("TutorialWallTrimFuture"));
        }

        [UnityTest] public IEnumerator SolidCladdingMatchesOriginalCollisionAndKeepsDoorOpenings()
        {
            var root = GameObject.Find("Tutorial navigation cladding");
            Assert.That(root, Is.Not.Null);
            var panels = root.GetComponentsInChildren<Renderer>().Where(x => x.name.StartsWith("Solid wall | ")).ToArray();
            Assert.That(panels.Length, Is.GreaterThanOrEqualTo(14));
            foreach (var panel in panels)
            {
                var original = GameObject.Find(panel.name.Substring("Solid wall | ".Length));
                var collider = original.GetComponentInChildren<BoxCollider>();
                Assert.That(collider, Is.Not.Null, original.name);
                Assert.That(Vector3.Distance(collider.bounds.center, panel.bounds.center), Is.LessThan(.01f), original.name);
                Assert.That(Vector3.Distance(collider.bounds.size, panel.bounds.size), Is.LessThan(.01f), original.name);
                Assert.That(original.GetComponentInChildren<Renderer>().enabled, Is.False);
            }
            Assert.That(Physics.Raycast(new Vector3(1, .8f, 4.45f), Vector3.left, 2), Is.True, "Sealed access wall must remain solid.");
            yield return SolveIntro();
            Assert.That(Physics.Raycast(new Vector3(.7f, .8f, 13.45f), Vector3.left, 1.4f), Is.False, "The true access doorway must remain clear after unlocking.");
            yield return SolveMain();
            Assert.That(Physics.Raycast(new Vector3(-8.3f, .8f, 4.45f), Vector3.left, 1.4f), Is.False, "The true west exit must remain clear after unlocking.");
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
        public IEnumerator SolvedTutorialExitContinuesToPowerRestoration()
        {
            yield return SolveIntro();
            yield return SolveMain();
            playerBody.isKinematic = false;
            Place(playerBody, new Vector3(-13.3f, 0, 4.45f));
            yield return new WaitForSecondsRealtime(3f);
            Assert.That(Flag("Completed"), Is.True);
            var completion = Component(level.gameObject, "TutorialCompletionScreen");
            Assert.That((bool)Prop(completion, "IsReady"), Is.True);
            GameObject.Find("CONTINUE").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1_PowerRestoration"));
            Assert.That(GameObject.Find("Memory Recovery Narrative"), Is.Not.Null);
            Assert.That(GameSettingsMenu.NarrativeInputBlocked, Is.True);
            float deadline = Time.realtimeSinceStartup + 35;
            while (GameObject.Find("Memory Recovery Narrative") != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GameObject.Find("Memory Recovery Narrative"), Is.Null);
            Assert.That(GameSettingsMenu.GameplayInputBlocked, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Component(GameObject.Find("Player"), "PlayerController").enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator TimelineWipeRendersAfterSceneReload()
        {
            // Ensure the clocks differ enough to detect an application-time timestamp.
            yield return new WaitForSeconds(1.2f);
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            state = state.GetType().GetMethod("get").Invoke(null, null);
            var material = (Material)Get(state, "shader");
            var target = new RenderTexture(64, 64, 24);
            var image = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var cameraObject = new GameObject("Wipe render camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = .5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << 31; camera.targetTexture = target;
            camera.transform.position = new Vector3(1000, 0, 0);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.layer = 31; quad.GetComponent<Collider>().enabled = false;
            quad.transform.position = new Vector3(1000, 0, 2);
            quad.GetComponent<Renderer>().sharedMaterial = material;
            int DarkPixels()
            {
                // Camera.Render supplies Unity's built-in clock; a bare Blit does not.
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); image.Apply();
                return image.GetPixels().Count(c => c.grayscale < .5f);
            }
            try
            {
                Assert.That(DarkPixels(), Is.Zero, "No wipe before a transition.");
                Call(state, "SwapTime");
                yield return new WaitForSeconds(.55f);
                Assert.That(DarkPixels(), Is.GreaterThan(1000),
                    "The transition must cover the frame around the actual timeline swap, including after menu/replay loads.");
                yield return new WaitForSeconds(.6f);
                Assert.That(DarkPixels(), Is.Zero, "The wipe must clear at the end of the transition.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(quad); Object.Destroy(cameraObject);
                target.Release(); Object.Destroy(target); Object.Destroy(image);
            }
        }

        [UnityTest]
        public IEnumerator GravityConsoleColourTracksEveryToggleAndCheckpointReset()
        {
            yield return Swap();
            var console = (Component)Get(level, "gravitySwitch");
            var colours = Component(console.gameObject, "ColourSwapper");
            var renderer = console.GetComponent<Renderer>();
            var initial = renderer.sharedMaterial;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Call(console, "onInteract", interaction);
                Assert.That((float)Get(state, "gravity"), Is.Zero);
                Assert.That(renderer.sharedMaterial, Is.EqualTo(Get(colours, "mat2")));
                Call(console, "onInteract", interaction);
                Assert.That(renderer.sharedMaterial, Is.EqualTo(initial));
            }
            Call(console, "onInteract", interaction);
            Call(level, "ResetCurrentPuzzle");
            Assert.That(renderer.sharedMaterial, Is.EqualTo(initial));
            Assert.That((float)Get(state, "gravity"), Is.EqualTo(14.715f));
        }

        [UnityTest]
        public IEnumerator RapidTimelineRequestsAndPauseDoNotDuplicateOrLoseTransitions()
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                string before = Get(state, "now").ToString();
                Call(state, "SwapTime");
                for (int request = 0; request < 8; request++) Call(state, "SwapTime");
                GameSettingsMenu.Instance.Open();
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(Get(state, "now").ToString(), Is.EqualTo(before));
                GameSettingsMenu.Instance.Close();
                yield return new WaitForSeconds(.65f);
                Assert.That(Get(state, "now").ToString(), Is.Not.EqualTo(before));
                var cubes = Cubes("introCubes");
                Assert.That(cubes[0].gameObject.activeSelf, Is.EqualTo(before == "Future"));
                Assert.That(cubes[1].gameObject.activeSelf, Is.EqualTo(before == "Past"));
                yield return new WaitForSeconds(.45f);
            }
        }

        [UnityTest]
        public IEnumerator LooseCubeSafetyHoldClearsThroughRapidGravityRequests()
        {
            yield return SolveIntro(); yield return ReleaseStorage();
            var interlock = Get(level, "storageInterlock");
            var cube = Cubes("mainCubes")[0];
            Place(cube, new Vector3(-6, -.48f, 12));
            var holograms = Component(GameObject.Find("Holographic guidance"), "LevelHolograms");
            for (int cycle = 0; cycle < 6; cycle++)
            {
                Call(Get(level, "gravitySwitch"), "onInteract", interaction);
                yield return null; yield return null;
                Assert.That((bool)Prop(interlock, "ClosingDeferred"), Is.True);
                Assert.That(Door("storageDoor").enabled, Is.False);
                Assert.That(Prop(Get(holograms, "storageText"), "text"), Does.Contain("SAFETY HOLD"));
                Call(Get(level, "gravitySwitch"), "onInteract", interaction);
                yield return null; yield return null;
                Assert.That((bool)Prop(interlock, "ClosingDeferred"), Is.False);
                Assert.That(Door("storageDoor").enabled, Is.False);
            }
            Call(Get(level, "gravitySwitch"), "onInteract", interaction);
            Place(cube, new Vector3(-5.5f, -.25f, 5));
            yield return Ticks(5);
            Assert.That(Door("storageDoor").enabled, Is.True);
            Assert.That(Prop(Get(holograms, "storageText"), "text"), Does.Not.Contain("SAFETY HOLD"));
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
            yield return new WaitForSecondsRealtime(.15f); Assert.That(Flag("Completed"), Is.True);
            // This physics test exercises checkpoint reset independently of the completion modal.
            Component(level.gameObject, "TutorialCompletionScreen").enabled = false;
            playerBody.isKinematic = false; // Restore real player physics after Aim's fixture override.
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
            playerBody.isKinematic = false;
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
            Assert.That(projections.Count(r => r.enabled), Is.EqualTo(2), "Only the two task chevrons are enabled; legacy panels stay hidden.");
            foreach (var renderer in projections.Where(r => !r.enabled))
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
