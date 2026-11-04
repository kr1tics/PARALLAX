using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class Scene01PresentationTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        MonoBehaviour presentation, level;
        object state;
        static object Field(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
        static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        static void Call(object value, string name, params object[] args) => value.GetType().GetMethod(name).Invoke(value, args);
        string Instruction => (string)Property(presentation, "CurrentInstruction");
        [UnitySetUp] public IEnumerator Setup()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            var root = GameObject.Find("Level 1 - Engineering Access");
            presentation = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Level1Presentation");
            level = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Level1Controller");
            state = level.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); } Time.timeScale = 1; yield return null; }
        IEnumerator Swap() { yield return new WaitForSeconds(1.05f); Call(state, "SwapTime"); yield return new WaitForSeconds(.65f); }

        [UnityTest] public IEnumerator ObjectiveFollowsActualPlacementAndReset()
        {
            Assert.That(Instruction, Is.EqualTo("Switch to the Past."));
            yield return Swap(); Assert.That(Instruction, Is.EqualTo("Locate the power cube."));
            var cube = ((Rigidbody[])Field(level, "introCubes"))[0];
            cube.position = new Vector3(2.5f, .1f, 13.45f); cube.transform.position = cube.position;
            cube.linearVelocity = Vector3.zero; Physics.SyncTransforms();
            yield return new WaitForSeconds(.8f);
            Assert.That(Instruction, Is.EqualTo("Switch to the Future."));
            Assert.That((bool)Property(level, "IntroSolved"), Is.False);
            yield return Swap(); yield return new WaitForSeconds(.8f);
            Assert.That((bool)Property(level, "IntroSolved"), Is.True);
            Assert.That(Instruction, Does.Contain("gravity console"));
            Call(level, "ResetCurrentPuzzle"); yield return null;
            Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("UnlockStorage"));
            Assert.That(Instruction, Does.Contain("Switch to Past"));
        }

        [UnityTest] public IEnumerator EngineeringPlacementRequestsGravityOnBeforeSwitchingTimeline()
        {
            yield return Swap();
            level.GetType().GetField("<IntroSolved>k__BackingField", Fields).SetValue(level, true);
            level.GetType().GetField("<StorageAccessGranted>k__BackingField", Fields).SetValue(level, true);
            state.GetType().GetField("gravity").SetValue(state, 0f);
            var cube = ((Rigidbody[])Field(presentation, "engineeringCubes"))[0];
            var plate = (Component)Field(presentation, "engineeringPlate");
            var bounds = plate.GetComponent<Collider>().bounds;
            cube.isKinematic = true;
            cube.position = new Vector3(bounds.center.x, bounds.max.y + .6f, bounds.center.z);
            cube.transform.position = cube.position; Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.That(Instruction, Is.EqualTo("Return to the gravity console.\nTurn station gravity ON."));
            var label = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Single(x => x.text == Instruction);
            label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False);
            Capture(Camera.main, "objective02-gravity-off", 1280, 720);
            state.GetType().GetField("gravity").SetValue(state, 14.715f);
            yield return null;
            Assert.That(Instruction, Is.EqualTo("Switch to the Future."));
            Assert.That((bool)Property(level, "MainSolved"), Is.False, "UI must not complete the puzzle.");
        }

        [UnityTest] public IEnumerator StorageGuidanceTracksLocationHoldingDiscoveryAndReset()
        {
            level.GetType().GetField("<IntroSolved>k__BackingField", Fields).SetValue(level, true);
            yield return null;
            Assert.That(Instruction, Does.Contain("gravity console"));
            yield return Swap();
            state.GetType().GetField("gravity").SetValue(state, 0f);
            yield return null; yield return null;
            Assert.That(Instruction, Is.EqualTo("Enter Storage and locate the power cube."));
            var cube = ((Rigidbody[])Field(presentation, "engineeringCubes"))[0];
            var interaction = (MonoBehaviour)Field(presentation, "interaction");
            var movable = cube.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "MovableObject");
            var player = interaction.GetComponent<Rigidbody>();
            player.isKinematic = true;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>())
                if (behaviour.GetType().Name == "PlayerController") behaviour.enabled = false;
            cube.isKinematic = true;
            cube.position = new Vector3(-6, -.48f, 15); cube.transform.position = cube.position;
            Physics.SyncTransforms();
            var rayOrigin = (Transform)Field(interaction, "rayOrigin");
            var probe = new GameObject("Guidance test interaction ray");
            try
            {
                probe.transform.position = cube.position + Vector3.back * 1.8f;
                probe.transform.LookAt(cube.position);
                interaction.GetType().GetField("rayOrigin").SetValue(interaction, probe.transform);
                yield return null;
                Assert.That(Instruction, Does.Contain("Retrieve the power cube"));
                interaction.GetType().GetField("<HeldObject>k__BackingField", Fields).SetValue(interaction, movable);
                yield return null;
                Assert.That(Instruction, Is.EqualTo("Move the cube to Relay Station 02."));
                interaction.GetType().GetField("<HeldObject>k__BackingField", Fields).SetValue(interaction, null);
                cube.position = new Vector3(-5, .2f, 9); cube.transform.position = cube.position;
                Physics.SyncTransforms(); yield return null;
                Assert.That(Instruction, Does.Contain("outside Storage").And.Not.Contain("Enter Storage"));
                state.GetType().GetField("gravity").SetValue(state, 14.715f);
                yield return null; yield return null;
                Assert.That(Instruction, Does.Contain("outside Storage"), "Closing the hatch must not send the player back inside.");
                cube.position = new Vector3(-6, -.48f, 15); cube.transform.position = cube.position;
                Physics.SyncTransforms(); yield return null;
                Assert.That(Instruction, Does.Contain("Storage locked").And.Contain("gravity OFF"));
                Call(level, "ResetCurrentPuzzle"); yield return null; yield return null;
                Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("UnlockStorage"));
                Assert.That(Instruction, Does.Contain("Switch to Past"));
                Assert.That((bool)Property(level, "MainSolved"), Is.False);
            }
            finally
            {
                interaction.GetType().GetField("rayOrigin").SetValue(interaction, rayOrigin);
                interaction.GetType().GetField("<HeldObject>k__BackingField", Fields).SetValue(interaction, null);
                Object.Destroy(probe);
            }
        }

        [UnityTest] public IEnumerator VisualDetailsDoNotAddPhysicsOrBlockPause()
        {
            var details = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(x => x.name == "Industrial inset (visual only)").ToArray();
            Assert.That(details.Length, Is.GreaterThan(8));
            Assert.That(details.All(x => x.GetComponent<Collider>() == null), Is.True);
            Assert.That(details.All(x => x.GetComponent<Rigidbody>() == null), Is.True);
            var hud = GameObject.Find("Scene 01 Objective UI").GetComponent<CanvasGroup>();
            Assert.That(hud.blocksRaycasts, Is.False);
            GameSettingsMenu.Instance.Open(); yield return null;
            Assert.That(hud.alpha, Is.Zero);
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            Assert.That(hud.alpha, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CapturesBothTimelinesAndChecksFontReferences()
        {
            Assert.That(((Object)Field(presentation, "titleFont")).name, Is.EqualTo("Oxanium-Regular SDF"));
            Assert.That(((Object)Field(presentation, "supportingFont")).name, Is.EqualTo("SpaceMono-Regular SDF"));
            var player = GameObject.Find("Player");
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) if (behaviour.GetType().Name == "PlayerController") behaviour.enabled = false;
            player.GetComponent<Rigidbody>().isKinematic = true;
            var camera = Camera.main;
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>()) if (behaviour.GetType().Name == "Look") behaviour.enabled = false;
            camera.transform.position = new Vector3(4.6f, 1.6f, 10.8f);
            camera.transform.LookAt(new Vector3(2.6f, .5f, 14));
            yield return null; Capture(camera, "future-relay", 1280, 720);
            yield return Swap(); Capture(camera, "past-relay", 1280, 720);
            Capture(camera, "past-relay-wide", 1920, 1080);
            Capture(camera, "past-relay-small", 960, 540);
            Assert.That((bool)Property(level, "IntroSolved"), Is.False);
        }
        [UnityTest] public IEnumerator ObjectiveCardCollapsesWithoutReopeningForTimelineCorrections()
        {
            yield return new WaitForSeconds(.35f);
            Assert.That((bool)Property(presentation, "ObjectiveExpanded"), Is.True);
            int revision = (int)Property(presentation, "ObjectiveRevision");
            Assert.That(GameObject.Find("Objective panel"), Is.Null, "The old top-left objective must not coexist.");
            Capture(Camera.main, "phase2-expanded", 1280, 720);
            yield return Swap();
            Assert.That((int)Property(presentation, "ObjectiveRevision"), Is.EqualTo(revision));
            yield return new WaitForSeconds(2.5f);
            Assert.That((bool)Property(presentation, "ObjectiveExpanded"), Is.False);
            var compact = GameObject.Find("Compact objective bar");
            Assert.That(compact.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Capture(Camera.main, "phase2-compact", 1280, 720);
            var before = (float)Field(Field(presentation, "hud"), "age");
            GameSettingsMenu.Instance.Open(); yield return new WaitForSecondsRealtime(.3f);
            Assert.That((float)Field(Field(presentation, "hud"), "age"), Is.EqualTo(before));
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            level.GetType().GetField("<IntroSolved>k__BackingField", Fields).SetValue(level, true);
            yield return null;
            Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("FindConsole"));
            Assert.That((int)Property(presentation, "ObjectiveRevision"), Is.GreaterThan(revision));
            Call(level, "ResetCurrentPuzzle"); yield return null; yield return null;
            Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("UnlockStorage"), "The checkpoint spawns beside the console.");
        }

        [UnityTest] public IEnumerator ProgressiveCuesFollowStorageCubePlacementAndExit()
        {
            var player = GameObject.Find("Player"); var body = player.GetComponent<Rigidbody>();
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) if (behaviour.GetType().Name == "PlayerController") behaviour.enabled = false;
            body.isKinematic = true;
            player.GetComponent<Renderer>().enabled = false;
            var camera = Camera.main;
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>()) if (behaviour.GetType().Name == "Look") behaviour.enabled = false;
            var interaction = (MonoBehaviour)Field(presentation, "interaction");
            var engineering = ((Rigidbody[])Field(presentation, "engineeringCubes"))[0];
            yield return new WaitForSeconds(.8f);
            Assert.That((bool)Property(presentation, "ShowEngineeringPlacement"), Is.False);
            Assert.That(GameObject.Find("Relay target hologram 2").GetComponentsInChildren<Renderer>().Any(x => x.enabled), Is.False);
            level.GetType().GetField("<IntroSolved>k__BackingField", Fields).SetValue(level, true);
            yield return null;
            Assert.That(Property(presentation, "CueLabel"), Is.EqualTo("GRAVITY CONSOLE"));
            body.position = new Vector3(-2, .02f, 8); body.transform.position = body.position; Physics.SyncTransforms();
            yield return null;
            Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("UnlockStorage"));
            yield return Swap(); state.GetType().GetField("gravity").SetValue(state, 0f);
            body.position = new Vector3(-6, .02f, 9.5f); body.transform.position = body.position;
            camera.transform.position = new Vector3(-6, 1.25f, 9); camera.transform.LookAt(new Vector3(-6, 1, 12));
            Physics.SyncTransforms(); yield return null; yield return new WaitForSeconds(.4f);
            Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("FindCube"));
            Assert.That(Property(presentation, "CueLabel"), Is.EqualTo("STORAGE AREA"));
            Capture(camera, "phase2-storage-discovery", 1280, 720);
            var holograms = GameObject.Find("Holographic guidance").GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "LevelHolograms");
            Assert.That((string)Property(holograms, "ActiveCue"), Is.EqualTo("STORAGE AREA"));
            var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.transform.position = new Vector3(-6, 1, 10.5f);
            obstruction.transform.localScale = new Vector3(3, 3, .15f);
            Physics.SyncTransforms(); yield return null; yield return null;
            Assert.That((string)Property(holograms, "ActiveCue"), Is.Empty, "A wall must occlude the world cue.");
            Object.Destroy(obstruction); yield return null; yield return null;
            body.position = new Vector3(-6, .02f, 13.5f); body.transform.position = body.position;
            camera.transform.position = new Vector3(-6, 1.1f, 13); camera.transform.LookAt(engineering.position);
            Physics.SyncTransforms(); yield return null; yield return null;
            Assert.That(Property(presentation, "CueLabel"), Is.EqualTo("POWER CUBE"));
            Capture(camera, "phase2-cube-discovery", 1280, 720);
            var movable = engineering.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "MovableObject");
            interaction.GetType().GetField("<HeldObject>k__BackingField", Fields).SetValue(interaction, movable);
            yield return null; yield return new WaitForSeconds(.8f);
            Assert.That((bool)Property(presentation, "ShowEngineeringPlacement"), Is.True);
            Assert.That(Property(presentation, "CueTarget"), Is.Null, "Held cubes should not retain a pickup marker.");
            camera.transform.position = new Vector3(-3.5f, 1.6f, 7.8f); camera.transform.LookAt(new Vector3(-5.5f, -.4f, 5));
            yield return null; yield return null;
            Assert.That(GameObject.Find("Interaction prompt backing").GetComponent<RectTransform>().rect.width, Is.LessThan(440));
            Assert.That(GameObject.Find("Interaction keycap"), Is.Not.Null);
            var actionLabel = GameObject.Find("Contextual interaction prompt").GetComponent<TMPro.TMP_Text>();
            Assert.That(actionLabel.color.r, Is.GreaterThan(actionLabel.color.b));
            Capture(camera, "phase2-relay-guidance", 1280, 720);
            interaction.GetType().GetField("<HeldObject>k__BackingField", Fields).SetValue(interaction, null);
            engineering.isKinematic = true;
            var plate = ((Component)Field(presentation, "engineeringPlate")).GetComponent<Collider>().bounds;
            engineering.position = new Vector3(plate.center.x, plate.max.y + .6f, plate.center.z); engineering.transform.position = engineering.position;
            Physics.SyncTransforms(); yield return null; yield return new WaitForSeconds(.8f);
            Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("RestoreGravity"));
            Assert.That(GameObject.Find("Relay target hologram 2").GetComponentsInChildren<Renderer>().Any(x => x.enabled), Is.False);
            state.GetType().GetField("gravity").SetValue(state, 14.715f); yield return null;
            Assert.That(Property(presentation, "Stage").ToString(), Is.EqualTo("ReturnFuture"));
            level.GetType().GetField("<MainSolved>k__BackingField", Fields).SetValue(level, true); yield return null;
            Assert.That(Property(presentation, "CueLabel"), Is.EqualTo("EXIT"));
            Call(level, "ResetCurrentPuzzle"); yield return null; yield return null;
            Assert.That(Property(presentation, "CueLabel"), Is.EqualTo("GRAVITY CONSOLE"));
            Assert.That((bool)Property(presentation, "ShowEngineeringPlacement"), Is.False);
        }

        [UnityTest] public IEnumerator TutorialFramedMarkerAndFloorArrowFollowProgress()
        {
            var holograms = GameObject.Find("Holographic guidance").GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "LevelHolograms");
            Assert.That(((TMPro.TMP_Text)Field(holograms, "storageText")).enabled, Is.False);
            Assert.That(((TMPro.TMP_Text)Field(holograms, "consoleText")).enabled, Is.False);
            Assert.That(((Transform)Field(holograms, "objectiveMarker")).gameObject.activeSelf, Is.False);
            Assert.That((bool)Property(holograms, "LeftArrowVisible"), Is.False);
            level.GetType().GetField("<IntroSolved>k__BackingField", Fields).SetValue(level, true);
            yield return null; yield return null;
            Assert.That((bool)Property(holograms, "LeftArrowVisible"), Is.True);
            var arrow = GameObject.Find("Console route left arrow");
            Assert.That(arrow.GetComponent<Collider>(), Is.Null);
            Assert.That(arrow.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Unlit/SimpleHologram"));
            var focus = holograms.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "InvestigationFocus");
            var marker = focus.transform.Find("Primary investigation marker");
            Assert.That(marker.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(8));
            Assert.That(marker.GetComponent<Collider>(), Is.Null);
            Assert.That(marker.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Unlit/SimpleHologram"));
            GameSettingsMenu.Instance.Open(); yield return null;
            Assert.That((int)Property(focus, "VisibleMarkerCount"), Is.Zero);
            Assert.That((bool)Property(holograms, "LeftArrowVisible"), Is.False);
            GameSettingsMenu.Instance.Close(); yield return new WaitForSeconds(.35f);
            Assert.That((bool)Property(holograms, "LeftArrowVisible"), Is.True);
            state.GetType().GetField("gravity").SetValue(state, 0f);
            yield return Swap(); yield return null;
            Assert.That((bool)Property(holograms, "LeftArrowVisible"), Is.False);
        }

        [UnityTest] public IEnumerator ExpandedAndCompactObjectivesNeverDrawTogether()
        {
            var hud=Field(presentation,"hud");
            var expanded=GameObject.Find("Expanded objective card").GetComponent<CanvasGroup>();
            var compact=GameObject.Find("Compact objective bar").GetComponent<CanvasGroup>();
            presentation.enabled=false;
            foreach(float age in new[]{3.6f,3.72f,3.84f,3.88f,4.0f,4.1f})
            {
                hud.GetType().GetField("age",Fields).SetValue(hud,age);
                hud.GetType().GetMethod("Tick").Invoke(hud,new object[]{0f});
                Assert.That(expanded.alpha>0 && compact.alpha>0,Is.False,"No duplicate text during collapse at "+age);
            }
            presentation.enabled=true;yield return null;
        }

        [UnityTest] public IEnumerator TopCentreHudFitsCommonWebGlAspectRatios()
        {
            var canvas = GameObject.Find("Scene 01 Objective UI").GetComponent<Canvas>();
            var camera = Camera.main;
            var panel = GameObject.Find("Expanded objective card").GetComponent<RectTransform>();
            var status = GameObject.Find("Timeline status").GetComponent<RectTransform>();
            foreach (var size in new[] { new Vector2Int(960,540), new Vector2Int(1280,720), new Vector2Int(1920,1080), new Vector2Int(1024,768), new Vector2Int(2560,1080) })
            {
                var rt = new RenderTexture(size.x, size.y, 24); camera.targetTexture = rt;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .8f;
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))
                { text.ForceMeshUpdate(true); Assert.That(text.isTextOverflowing, Is.False, text.name + " at " + size); }
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                float cardTop = camera.WorldToScreenPoint(corners[1]).y;
                float cardLeft = camera.WorldToScreenPoint(corners[0]).x;
                float cardRight = camera.WorldToScreenPoint(corners[2]).x;
                Assert.That(cardLeft, Is.GreaterThan(0)); Assert.That(cardRight, Is.LessThan(size.x));
                status.GetWorldCorners(corners);
                Assert.That(cardTop, Is.LessThan(camera.WorldToScreenPoint(corners[0]).y), "Status occupies a separate top row.");
                camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay; Object.Destroy(rt);
            }
        }

        internal static void Capture(Camera camera, string name, int width, int height)
        {
            Directory.CreateDirectory(".utmp/scene01");
            var canvas = GameObject.Find("Scene 01 Objective UI").GetComponent<Canvas>();
            var previousMode = canvas.renderMode; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .8f;
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/scene01/" + name + ".png", image.EncodeToPNG());
            canvas.renderMode = previousMode; camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Object.Destroy(image); Object.Destroy(rt);
        }
    }
}
