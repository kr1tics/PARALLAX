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
    // Power Restoration's environment pass: timeline dressing, the gantry under B and the routes the puzzle walks.
    public class PowerRestorationEnvironmentTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Rigidbody player;
        MonoBehaviour look;
        object state;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static MonoBehaviour Component(GameObject go, string name) => go.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == name);
        static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        string Now => state.GetType().GetField("now").GetValue(state).ToString();
        GameObject Environment => GameObject.Find("10 / Environment");

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
            player = GameObject.Find("Player").GetComponent<Rigidbody>();
            look = Component(Camera.main.gameObject, "Look");
            state = player.GetComponents<MonoBehaviour>().First().GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            yield return new WaitForSeconds(.3f);
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

        IEnumerator Swap(string expected)
        {
            yield return new WaitForSeconds(1.05f);
            Call(state, "SwapTime"); yield return new WaitForSeconds(.7f);
            Assert.That(Now, Is.EqualTo(expected));
        }
        void Face(float yaw, float pitch = 0) => Call(look, "Rotate", new Vector2(yaw, pitch));
        void Place(Vector3 position) { player.position = position; player.transform.position = position; player.linearVelocity = Vector3.zero; Physics.SyncTransforms(); }

        IEnumerator WalkTo(float x, float z)
        {
            var delta = new Vector3(x, 0, z) - player.position;
            Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            float deadline = Time.time + 6;
            while (Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z)) > .35f && Time.time < deadline)
                yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z)), Is.LessThan(.8f),
                "Walking route blocked at " + player.position + " towards " + x + "," + z);
        }

        void Capture(string name)
        {
            Directory.CreateDirectory(".utmp/level1");
            var camera = Camera.main; var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            if (name.StartsWith("sparks-"))
            {
                var effect = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).FirstOrDefault();
                if (effect != null && Now == "Future") { foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) { ps.Simulate(0, true, false, false); ps.Play(); } }
            }
            camera.Render();
            RenderTexture.active = target; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/level1/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            Object.Destroy(image); Object.Destroy(target);
        }

        [UnityTest] public IEnumerator TimelineDressingIsColliderFreeAndFollowsTheTimeline()
        {
            var past = Environment.transform.Find("2026 only").gameObject; var future = Environment.transform.Find("2176 only").gameObject;
            Assert.That(past.GetComponentsInChildren<Collider>(true), Is.Empty, "Dressing can never trap a player during a switch.");
            Assert.That(future.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(GameObject.Find("2176 truss collapse").GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(future.activeSelf, Is.True); Assert.That(past.activeSelf, Is.False);
            yield return Swap("Past");
            Assert.That(past.activeSelf, Is.True); Assert.That(future.activeSelf, Is.False);
            Assert.That(GameObject.Find("Route 1 roof truss (intact)"), Is.Not.Null, "In 2026 the truss over route 1 is still up.");
            Assert.That(GameObject.Find("2176 truss collapse"), Is.Null);
            var wall = Environment.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Wall_Simple_01"));
            Assert.That(wall.sharedMaterial.name, Does.Not.Contain("FutureDecay"));
            yield return Swap("Future");
            Assert.That(wall.sharedMaterial.name, Does.Contain("FutureDecay"), "Kit cladding weathers in 2176.");
        }

        [UnityTest] public IEnumerator GantryHoldsThePlayerWithinReachOfB()
        {
            var moduleA = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
            moduleA.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(moduleA, true); // A's path has its own tests.
            yield return Swap("Past");
            Place(new Vector3(7.4f, 11.2f, -.5f)); Face(62);
            yield return new WaitForSeconds(1);
            Assert.That(player.position.y, Is.InRange(10.8f, 11.4f), "Standing on the gantry deck under station gravity.");
            var b = Component(GameObject.Find("Repair panel"), "PowerModuleB");
            Assert.That((bool)Prop(b, "CanRepair"), Is.True, "B is in reach from the gantry.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That((bool)Prop(b, "IsRepaired"), Is.True);
        }

        [UnityTest] public IEnumerator MainRoutesStayWalkableInBothTimelines()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Place(new Vector3(0, 1.05f, -8.2f)); yield return new WaitForSeconds(.3f);
                yield return WalkTo(-4.5f, -6); yield return WalkTo(-5.8f, -2.8f); // Maintenance bay, behind module A.
                yield return WalkTo(-6.6f, -2); // Gravity console approach.
                yield return WalkTo(-3, -4); yield return WalkTo(6.5f, -4); yield return WalkTo(7, 1); // Junction approach.
                yield return WalkTo(4.5f, 0); yield return WalkTo(3.5f, 1.2f); // Reactor console.
                if (pass == 0) yield return Swap("Past");
            }
        }

        IEnumerator Press(Key key) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; }
        IEnumerator Hold(Key key, System.Func<bool> done, float seconds)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            for (float end = Time.time + seconds; Time.time < end && !done();) yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.4f);
        }
        // Look at a point the way a mouse would: yaw from the horizontal offset, pitch from the eye height.
        void LookAt(Vector3 point)
        {
            var eye = Camera.main.transform.position; var d = point - eye;
            float flat = new Vector2(d.x, d.z).magnitude;
            Face(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, .25f * d.y / Mathf.Max(.01f, flat));
        }
        // Zero-G legs: the jetpack keeps momentum, so release W early and let its damping bring the player to rest.
        IEnumerator FlyTo(float x, float z)
        {
            float Gap() => Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z));
            for (int attempt = 0; attempt < 4 && Gap() > .6f; attempt++)
            {
                var delta = new Vector3(x, 0, z) - player.position;
                Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg); yield return null;
                // Measured: the jetpack coasts about 0.6 s worth of speed after W is released.
                yield return Hold(Key.W, () => Gap() < .3f + player.linearVelocity.magnitude * .6f, 8);
                yield return new WaitForSeconds(1.5f);
            }
            Assert.That(Gap(), Is.LessThan(.8f), "Flight route blocked at " + player.position + " towards " + x + "," + z);
        }
        float StationGravity => (float)state.GetType().GetField("gravity").GetValue(state);
        object Prop(string objectName, string component, string property) => Prop(Component(GameObject.Find(objectName), component), property);

        // The whole level with normal controls: keyboard for every action, the camera aimed as a mouse would.
        [UnityTest] public IEnumerator FullPowerRestorationWithNormalControls()
        {
            var moduleA = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
            var interact = Component(player.gameObject, "Interact");
            Component(GameObject.Find("08 / Scene presentation"), "TutorialCompletionScreen").enabled = true;
            yield return Press(Key.T); yield return new WaitForSeconds(.8f);
            Assert.That(Now, Is.EqualTo("Past"));
            // Gravity console: cut station gravity so the 380 kg module can be lifted.
            yield return WalkTo(-6.6f, -2);
            LookAt(GameObject.Find("Gravity switch").transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That(StationGravity, Is.LessThan(.1f), "Console pressed with E.");
            LookAt(moduleA.transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That(Prop(interact, "HeldObject"), Is.SameAs(moduleA), "Module A lifted in zero-G.");
            // Fly (W) to stand in front of the socket, face it, release into the guide.
            yield return FlyTo(-6, .9f);
            Face(90, -.03f); yield return new WaitForSeconds(.8f);
            Assert.That((bool)Prop(moduleA, "CanInstall"), Is.True, "Inside the forgiving guide. module=" + moduleA.transform.position);
            yield return Press(Key.E); yield return new WaitForSeconds(.5f);
            Assert.That((bool)Prop(moduleA, "IsRepaired"), Is.True);
            // Fly to the climb column, ascend with Space, repair B.
            yield return FlyTo(6.5f, 1.4f);
            yield return Hold(Key.Space, () => player.position.y + player.linearVelocity.y * .6f > 11.6f, 6);
            yield return new WaitForSeconds(1);
            var panel = GameObject.Find("Repair panel");
            LookAt(panel.transform.position); yield return null;
            Assert.That((bool)Prop("Repair panel", "PowerModuleB", "CanRepair"), Is.True, "At B. player=" + player.position);
            yield return Press(Key.E);
            Assert.That((bool)Prop("Repair panel", "PowerModuleB", "IsRepaired"), Is.True);
            // Descend with Ctrl, switch B's feed to the route that survives.
            yield return Hold(Key.LeftCtrl, () => player.position.y + player.linearVelocity.y * .6f < 1.4f, 8);
            yield return new WaitForSeconds(1);
            yield return FlyTo(7, 1);
            LookAt(GameObject.Find("B feed junction").transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That((int)Prop("09 / Power path", "PowerCircuit", "Route"), Is.EqualTo(2));
            // Back to the console: restore gravity for the control rods.
            yield return FlyTo(-6.6f, -2);
            LookAt(GameObject.Find("Gravity switch").transform.position); yield return null;
            yield return Press(Key.E);
            Assert.That(StationGravity, Is.GreaterThan(4.5f));
            yield return new WaitForSeconds(1);
            // 2176: start the reactor at its console.
            yield return Press(Key.T); yield return new WaitForSeconds(.8f);
            Assert.That(Now, Is.EqualTo("Future"));
            Assert.That((bool)Prop("08 / Scene presentation", "PowerRestorationPresentation", "ReactorReady"), Is.True);
            yield return WalkTo(3.5f, 1.2f);
            LookAt(GameObject.Find("Console housing").transform.position); yield return null;
            yield return Press(Key.E);
            var reactor = Component(GameObject.Find("Console housing"), "PowerReactor");
            Assert.That((bool)Prop(reactor, "IsOnline"), Is.True);
            var completion = Component(GameObject.Find("08 / Scene presentation"), "TutorialCompletionScreen");
            Assert.That((bool)Prop(completion, "IsOpen"), Is.False, "The start-up plays before the completion screen.");
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That((bool)Prop(completion, "IsOpen"), Is.True); Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(2.6f); // Its own one-second delay and fade.
            Assert.That((bool)Prop(completion, "IsReady"), Is.True);
            Assert.That(GameObject.Find("Power Restoration Completion Screen"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator EquipmentSignageReviewViews()
        {
            var views = new[] {
                ("overview", new Vector3(0, 1.05f, -8.2f), new Vector3(0, 2.3f, 4)),
                ("module-a", new Vector3(-4.8f, 1.05f, -3.3f), new Vector3(-5.8f, .78f, -1.34f)),
                ("socket", new Vector3(-4.2f, 1.05f, .5f), new Vector3(-6, 1.8f, 3.5f)),
                ("module-b", new Vector3(6.5f, 11.3f, 1.4f), new Vector3(8.24f, 11.8f, 1.4f)),
                ("reactor-console", new Vector3(3.5f, 1.05f, .7f), new Vector3(3.5f, 1.6f, 2.81f)),
                ("route-selector", new Vector3(6.6f, 1.05f, 1.2f), new Vector3(8.285f, 1.1f, 1)) };
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var (name, position, target) in views)
                {
                    Place(position);
                    var delta = target - Camera.main.transform.parent.position;
                    Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg, .25f * delta.y / new Vector2(delta.x, delta.z).magnitude);
                    yield return new WaitForSeconds(.15f);
                    Capture("signage-" + (pass == 0 ? "2176-" : "2026-") + name);
                }
                if (pass == 0) yield return Swap("Past");
            }
        }

        [UnityTest] public IEnumerator EquipmentSignsAreContainedAndFollowExistingDeviceState()
        {
            var panels = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.name == "Power Restoration Equipment Signs").ToArray();
            Assert.That(panels.Length, Is.EqualTo(13));
            foreach (var panel in panels)
            {
                Assert.That(panel.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(panel.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(panel.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                foreach (var label in panel.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    if (string.IsNullOrEmpty(label.text)) continue;
                    label.ForceMeshUpdate();
                    Assert.That(label.textBounds.size.x, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f), label.name);
                    Assert.That(label.textBounds.size.y, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), label.name);
                    Assert.That(label.raycastTarget, Is.False);
                }
            }
            Assert.That(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).All(t => !t.GetComponent<Renderer>().enabled), Is.True);
            var a = Component(GameObject.Find("Power module A — placement only"), "PowerModuleA");
            var b = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleB");
            var circuit = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerCircuit");
            TMPro.TMP_Text Label(string name) => panels.SelectMany(c => c.GetComponentsInChildren<TMPro.TMP_Text>()).Single(t => t.name == name);
            var bPlate = Label("Module B identification title").GetComponentInParent<Canvas>();
            var consolePlate = Label("Reactor console identification title").GetComponentInParent<Canvas>();
            var aPlate = Label("Module A identification title").GetComponentInParent<Canvas>();
            Assert.That(bPlate.transform.position.x, Is.LessThan(GameObject.Find("Repair hologram").GetComponent<Renderer>().bounds.min.x));
            Assert.That(consolePlate.transform.position.z, Is.LessThan(GameObject.Find("Inactive screen").GetComponent<Renderer>().bounds.min.z));
            Assert.That(aPlate.transform.parent, Is.EqualTo(a.transform));
            Assert.That(Label("Module A identification detail").text, Is.EqualTo("380 KG / HEAVY LOAD"));
            Assert.That(Label("Module B identification state").text, Is.EqualTo("CONNECTION LOST"));
            Assert.That((int)Prop(circuit, "Route"), Is.EqualTo(1));
            a.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(a, true);
            b.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(b, true);
            yield return null; yield return null;
            Assert.That(Label("Module A identification state").text, Is.EqualTo("ONLINE"));
            Assert.That(Label("Module B identification state").text, Is.EqualTo("NO FEED"));
            Call(circuit, "SelectRoute", 2); yield return null; yield return null;
            Assert.That(Label("Module B identification state").text, Is.EqualTo("REPAIR COMPLETE"));
            Assert.That(Label("Module B identification state").color.g, Is.GreaterThan(Label("Module B identification state").color.r));
            Assert.That(Label("Route selector identification state").text, Is.EqualTo("ROUTE 2"));
            state.GetType().GetField("gravity").SetValue(state, 0f); yield return null; yield return null;
            Assert.That(Label("Reactor console identification detail").text, Is.EqualTo("RODS / FLOATING"));
            Place(new Vector3(6.5f, 11.3f, 1.4f)); Face(0, .07f); yield return new WaitForSeconds(.15f); Capture("signage-operational-b");
        }

        ParticleSystem RouteSparks => Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(p => p.name == "Route 1 / damaged connection sparks");
        [UnityTest] public IEnumerator RouteSparksFollowTimelineClearImmediatelyAndResetOnReload()
        {
            var effect = RouteSparks;
            var upper = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(p => p.name == "Route 1 / upper connection sparks");
            Assert.That(upper.transform.position, Is.EqualTo(new Vector3(8.70f, 8.13f, 4)));
            Assert.That(Now, Is.EqualTo("Future")); Assert.That(effect.gameObject.activeInHierarchy, Is.True);
            Assert.That(effect.main.maxParticles, Is.EqualTo(40)); Assert.That(effect.emission.enabled, Is.False);
            Assert.That(effect.collision.enabled, Is.False); Assert.That(effect.trails.enabled, Is.False);
            Assert.That(effect.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(effect.GetComponentsInChildren<Light>(), Is.Empty);
            Assert.That(effect.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.Not.Null);
            Assert.That(effect.transform.position, Is.EqualTo(new Vector3(8.74f, 6.81f, 4.15f)));
            float end = Time.time + 3; bool seen = false, upperSeen = false;
            while (Time.time < end) { seen |= effect.particleCount > 0; upperSeen |= upper.particleCount > 0; Assert.That(effect.particleCount + upper.particleCount, Is.LessThanOrEqualTo(40)); yield return null; }
            Assert.That(upperSeen, Is.True);
            Assert.That(seen, Is.True);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                yield return Swap("Past"); Assert.That(effect.gameObject.activeInHierarchy, Is.False); Assert.That(effect.particleCount, Is.Zero); Assert.That(upper.particleCount, Is.Zero); Assert.That(upper.gameObject.activeInHierarchy, Is.False);
                yield return new WaitForSeconds(.4f); Assert.That(effect.particleCount, Is.Zero);
                yield return Swap("Future"); Assert.That(effect.gameObject.activeInHierarchy, Is.True);
                end = Time.time + 2.3f; seen = false; upperSeen = false;
                while (Time.time < end) { seen |= effect.particleCount > 0; upperSeen |= upper.particleCount > 0; yield return null; }
                Assert.That(upperSeen, Is.True);
                Assert.That(seen, Is.True);
            }
            Assert.That((int)Prop(Component(GameObject.Find("09 / Power path"), "PowerCircuit"), "Route"), Is.EqualTo(1));
            yield return SceneManager.LoadSceneAsync("Level1_PowerRestoration"); yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(2));
            Assert.That(RouteSparks.gameObject.activeInHierarchy, Is.True);
            Assert.That(RouteSparks.particleCount, Is.LessThanOrEqualTo(20));
        }
        [UnityTest] public IEnumerator RouteSparksRenderedAtPlayerViewsAndStayWithinBudget()
        {
            var effect = RouteSparks;
            float oldCaptureStep = Time.captureDeltaTime;
            Time.captureDeltaTime = .05f;
            try
            {
            Component(player.gameObject, "PlayerController").enabled = false;
            Component(player.gameObject, "ArtificialGravity").enabled = false;
            player.isKinematic = true;
            void Locate(Vector3 position) { player.position = position; player.transform.position = position; Physics.SyncTransforms(); }
            var diagnostics = new System.Text.StringBuilder();
            var particles = new ParticleSystem.Particle[40];
            var stamps = new System.Collections.Generic.List<string>();
            var views = new[] {
                ("floor", new Vector3(5.8f, 1.05f, 1.5f)),
                ("gantry", new Vector3(6.5f, 11.3f, 1.4f)),
                ("connection", new Vector3(6.5f, 6.3f, 3.4f)) };
            int maximum = 0;
            foreach (var (name, position) in views)
            {
                Locate(position); var d = effect.transform.position - Camera.main.transform.parent.position;
                Face(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, .25f * d.y / new Vector2(d.x, d.z).magnitude);
                float end = Time.time + 2.5f;
                while (effect.particleCount == 0 && Time.time < end) yield return null;
                yield return new WaitForSeconds(.05f);
                int count = effect.GetParticles(particles);
                diagnostics.AppendLine(name + ": " + count + " particles; renderer=" + effect.GetComponent<Renderer>().enabled + " bounds=" + effect.GetComponent<Renderer>().bounds + " scale=" + effect.transform.lossyScale);
                for (int i = 0; i < count; i++) diagnostics.AppendLine(particles[i].position + " velocity=" + particles[i].velocity + " life=" + particles[i].remainingLifetime + " size=" + particles[i].GetCurrentSize(effect) + " color=" + particles[i].GetCurrentColor(effect) + " screen=" + Camera.main.WorldToViewportPoint(particles[i].position));
                maximum = Mathf.Max(maximum, effect.particleCount); Capture("sparks-future-" + name);
            }
            Locate(new Vector3(6.5f, 7.8f, 3.4f));
            var upper = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Single(p => p.name == "Route 1 / upper connection sparks");
            var upDirection = upper.transform.position - Camera.main.transform.parent.position;
            Face(Mathf.Atan2(upDirection.z, upDirection.x) * Mathf.Rad2Deg, .25f * upDirection.y / new Vector2(upDirection.x, upDirection.z).magnitude);
            float upperEnd = Time.time + 2;
            while (upper.particleCount == 0 && Time.time < upperEnd) yield return null;
            yield return new WaitForSeconds(.05f); Capture("sparks-upper-end");
            Assert.That(maximum, Is.InRange(1, 20));
            var status = GameObject.Find("Route 1 connection status").GetComponent<CanvasGroup>();
            yield return new WaitForSeconds(.3f); Assert.That(status.alpha, Is.EqualTo(1));
            Assert.That(status.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("CONNECTION LOST"));
            Locate(new Vector3(0, 1.05f, -5)); yield return new WaitForSeconds(.3f);
            Assert.That(status.alpha, Is.Zero);

            Locate(new Vector3(6.5f, 6.3f, 3.4f)); var delta = effect.transform.position - Camera.main.transform.parent.position;
            Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg, .25f * delta.y / new Vector2(delta.x, delta.z).magnitude);
            for (int frame = 0; frame < 64; frame++)
            {
                stamps.Add(Time.time.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Capture("sparks-animation-" + frame.ToString("000")); yield return null;
            }
            System.IO.File.WriteAllLines(".utmp/level1/sparks-frame-times.txt", stamps);
            System.IO.File.WriteAllText(".utmp/level1/sparks-diagnostics.txt", diagnostics.ToString());
            yield return Swap("Past");
            Assert.That(effect.particleCount, Is.Zero);
            Locate(new Vector3(6.5f, 6.3f, 3.4f)); delta = effect.transform.position - Camera.main.transform.parent.position;
            Face(Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg, .25f * delta.y / new Vector2(delta.x, delta.z).magnitude);
            yield return null; Capture("sparks-past-intact");
            }
            finally { Time.captureDeltaTime = oldCaptureStep; }
        }

        // Visual review stills; not assertions.
        void CapturePowerUx(string name)
        {
            var canvas = GameObject.Find("Power Restoration Objective UI").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = .5f;
            Canvas.ForceUpdateCanvases(); Capture(name); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        [UnityTest] public IEnumerator PowerObjectiveCardsFitCommonGameplayResolutions()
        {
            var canvas = GameObject.Find("Power Restoration Objective UI").GetComponent<Canvas>();
            var camera = Camera.main;
            foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1024, 768) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .8f;
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))
                { text.ForceMeshUpdate(true); Assert.That(text.isTextOverflowing, Is.False, text.name + " at " + size); }
                var corners = new Vector3[4]; GameObject.Find("Expanded objective card").GetComponent<RectTransform>().GetWorldCorners(corners);
                Assert.That(camera.WorldToScreenPoint(corners[0]).x, Is.GreaterThan(0));
                Assert.That(camera.WorldToScreenPoint(corners[2]).x, Is.LessThan(size.x));
                camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay; Object.Destroy(target);
            }
        }
        [UnityTest] public IEnumerator PowerObjectivesUseTutorialCardsOnlyAtMajorTransitions()
        {
            var presentation = Component(GameObject.Find("08 / Scene presentation"), "PowerRestorationPresentation");
            Component(player.gameObject, "PlayerController").enabled = false;
            Component(player.gameObject, "ArtificialGravity").enabled = false; player.isKinematic = true;
            player.position = new Vector3(0, 1.05f, -.2f); player.transform.position = player.position; Face(90, .02f);
            Assert.That((bool)Prop(presentation, "ObjectiveExpanded"), Is.True);
            int revision = (int)Prop(presentation, "ObjectiveRevision");
            CapturePowerUx("rods-objective-expanded");
            yield return new WaitForSeconds(3.5f);
            Assert.That((bool)Prop(presentation, "ObjectiveExpanded"), Is.True);
            yield return new WaitForSeconds(1.9f);
            Assert.That((bool)Prop(presentation, "ObjectiveExpanded"), Is.False);
            Assert.That(GameObject.Find("Compact objective").GetComponent<TMPro.TMP_Text>().text, Does.Contain("A: pending · B: pending"));
            CapturePowerUx("rods-objective-compact");
            yield return Swap("Past");
            state.GetType().GetField("gravity").SetValue(state, 0f); yield return null; yield return null;
            Assert.That((int)Prop(presentation, "ObjectiveRevision"), Is.EqualTo(revision));
            var circuit = Component(GameObject.Find("09 / Power path"), "PowerCircuit");
            var a = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "PowerModuleA");
            var b = Component(GameObject.Find("Repair panel"), "PowerModuleB");
            a.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(a, true);
            yield return null; yield return null;
            Assert.That((int)Prop(presentation, "ObjectiveRevision"), Is.EqualTo(revision));
            Assert.That(GameObject.Find("Compact objective").GetComponent<TMPro.TMP_Text>().text, Does.Contain("A: repaired · B: pending"));
            b.GetType().GetField("<IsRepaired>k__BackingField", Fields).SetValue(b, true);
            yield return null; yield return null;
            Assert.That(GameObject.Find("Objective title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Restore the Power Flow"));
            Assert.That((int)Prop(presentation, "ObjectiveRevision"), Is.EqualTo(revision + 1));
            Call(circuit, "SelectRoute", 2); yield return null; yield return null;
            Assert.That(GameObject.Find("Objective title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Stabilise the Reactor"));
            yield return new WaitForSeconds(5.4f); CapturePowerUx("rods-floating");
            var frames = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.name == "Control rod docking frame").ToArray();
            Assert.That(frames.Length, Is.EqualTo(4));
            foreach (var frame in frames) Assert.That(frame.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(GameObject.Find("Control rod assembly state").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("RODS UNSTABLE"));
            state.GetType().GetField("gravity").SetValue(state, 9.81f); yield return null; yield return null;
            Assert.That(GameObject.Find("Control rod assembly state").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("RODS SECURED"));
            var indicator = frames[0].GetComponentInChildren<UnityEngine.UI.Image>(); Assert.That(indicator.color.g, Is.GreaterThan(indicator.color.r));
            Assert.That(GameObject.Find("Objective title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Start the Reactor"));
            yield return new WaitForSeconds(.3f); CapturePowerUx("rods-secured");
            foreach (var text in GameObject.Find("Power Restoration Objective UI").GetComponentsInChildren<TMPro.TMP_Text>())
            {
                text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.name);
            }
        }

        [UnityTest] public IEnumerator CaptureReviewViews()
        {
            var views = new[] { ("spawn", new Vector3(0, 1.05f, -8.2f), 90f, -.02f), ("bay", new Vector3(-2, 1.05f, -3), 140f, -.06f),
                ("distribution", new Vector3(1, 1.05f, -3), 30f, .22f), ("reactor", new Vector3(-3, 1.05f, -2), 60f, .1f) };
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var (name, position, yaw, pitch) in views)
                {
                    Place(position); Face(yaw, pitch); yield return new WaitForSeconds(.2f);
                    Capture((pass == 0 ? "2176-" : "2026-") + name);
                }
                if (pass == 0) yield return Swap("Past");
            }
            state.GetType().GetField("gravity").SetValue(state, 0f); // Station gravity off: the rods float.
            Place(new Vector3(0, 1.05f, -3.5f)); Face(90, .05f); yield return new WaitForSeconds(.6f);
            Capture("2026-reactor-rods-floating");
        }
    }
}
