using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit, repeatable editor / batch validation. Does not save scenes or change game input bindings.
[InitializeOnLoad]
public static class PowerRestorationValidation
{
    public const string ScenePath = "Assets/Scenes/Level1_PowerRestoration.unity";
    private const string Running = "PARALLAX.Stage1.Smoke";
    private static int step;
    private static float deadline;
    private static double timeout;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static Rigidbody body;
    private static ArtificialGravity gravity;
    private static Vector3 before;

    static PowerRestorationValidation()
    {
        if (SessionState.GetBool(Running, false))
            EditorApplication.delayCall += ResumeSmoke;
    }

    public static void Validate()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Require(scene.IsValid() && scene.isLoaded, "Scene opens in Unity");
        var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
        foreach (var go in objects)
        {
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0, "No missing scripts: " + go.name, false);
            foreach (var component in go.GetComponents<Component>())
            {
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                            "No broken reference: " + go.name + "." + property.propertyPath, false);
            }
        }
        Require(objects.Count(g => g.GetComponent<PlayerController>() != null) == 1, "Exactly one existing player controller");
        Require(objects.Count(g => g.GetComponent<GlobalStateCompanion>() != null) == 1, "Exactly one existing time-state companion");
        var player = GameObject.Find("Player");
        var controller = new SerializedObject(player.GetComponent<PlayerController>());
        foreach (string field in new[] { "body", "grav", "cam", "feet", "interactionController" })
            Require(controller.FindProperty(field).objectReferenceValue != null, "Player reference: " + field);
        Require(!player.GetComponent<Rigidbody>().useGravity && player.GetComponent<ArtificialGravity>().effective,
            "Existing artificial gravity owns player gravity");
        Physics.SyncTransforms();
        var feet = (Transform)controller.FindProperty("feet").objectReferenceValue;
        Require(Physics.Raycast(feet.position,Vector3.down,0.5f,1 << LayerMask.NameToLayer("Structure")), "Spawn grounded on Structure layer");
        var capsule = player.GetComponent<CapsuleCollider>();
        Require(!Physics.CheckCapsule(player.transform.position + Vector3.up*0.5f, player.transform.position - Vector3.up*0.5f,
            capsule.radius-0.01f,1 << LayerMask.NameToLayer("Structure")), "Spawn clear of room collisions");
        var camera = player.GetComponentInChildren<Camera>();
        var reactor = GameObject.Find("Inactive core");
        var point = camera.WorldToViewportPoint(reactor.transform.position);
        Require(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "Reactor inside spawn camera frustum");
        Require(Physics.Raycast(camera.transform.position,(reactor.transform.position-camera.transform.position).normalized,out var sight,30)
            && sight.transform.root.name.StartsWith("02 /"), "Unobstructed reactor sightline from entrance");
        foreach (var direction in new[] {Vector3.left,Vector3.right,Vector3.forward,Vector3.back,Vector3.up,Vector3.down})
            Require(Physics.Raycast(new Vector3(0,8,-4),direction,30,1 << LayerMask.NameToLayer("Structure")), "Room enclosed: " + direction);
        Require(GameObject.Find("Repair panel").GetComponent<Collider>().bounds.min.y > 10.5f,
            "High repair station beyond floor jump plus current interaction range");
        Require(objects.Where(g => g.GetComponent<Renderer>() != null).All(g => g.GetComponent<Renderer>().sharedMaterials.All(m => m != null)),
            "Every renderer has materials");
        Require(objects.Single(g => g.name == "Activation effects anchor — reserved").GetComponent<Light>().enabled == false,
            "Reactor activation light disabled");
        Require(!objects.Any(g => g.GetComponent<Rigidbody>() != null && g != player), "Modules are static Stage 1 placeholders");
        Debug.Log("STAGE1 EDIT VALIDATION PASSED: " + objects.Length + " objects; scene opened; references and geometry checked.");
    }

    public static void Build()
    {
        Validate();
        Directory.CreateDirectory("Builds/Stage1");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = "Builds/Stage1/ParallaxStage1.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        Require(report.summary.result == BuildResult.Succeeded, "Standalone macOS development build");
        Debug.Log("STAGE1 BUILD PASSED: " + report.summary.totalSize + " bytes; " + report.summary.totalErrors + " errors.");
    }

    // Run without -quit: this method exits after its actual Play Mode smoke sequence completes.
    public static void Smoke()
    {
        Validate();
        SessionState.SetBool(Running,true);
        EditorApplication.isPlaying = true;
    }

    private static void ResumeSmoke()
    {
        if (!SessionState.GetBool(Running,false)) return;
        step = 0;
        timeout = EditorApplication.timeSinceStartup + 120;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        // Unity 6's search index can fail during batch startup; it is unrelated to scene execution.
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        {
            Debug.LogWarning("STAGE1: Editor search-index startup exception (excluded from gameplay checks): " + message);
            return;
        }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > timeout) { Finish(false,"Play Mode smoke timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (step != 0 && Time.time < deadline) return;
            switch (step)
            {
                case 0:
                    if (Time.time < 0.5f) return;
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    mouse = InputSystem.AddDevice<Mouse>();
                    body = GameObject.Find("Player").GetComponent<Rigidbody>();
                    gravity = body.GetComponent<ArtificialGravity>();
                    Require(GlobalState.get().now == TimeState.Future,"Play: starts in Future");
                    Require(gravity.IsGravityActive && body.position.y > 0.9f && body.position.y < 1.2f,"Play: safe grounded spawn");
                    Capture("future-entrance");
                    before = body.position;
                    Keys(Key.W); Advance(0.8f); break;
                case 1:
                    Require(body.position.z > before.z + 3,"Play: W moves toward reactor");
                    Keys(); Warp(new Vector3(7.7f,1.05f,-4)); Advance(0.2f); break;
                case 2:
                    Keys(Key.D); Advance(0.7f); break;
                case 3:
                    Require(body.position.x < 8.6f && body.position.x > 8.2f,"Play: right wall blocks movement");
                    Keys(); Warp(new Vector3(6.5f,1.05f,1.4f)); Advance(0.2f); break;
                case 4:
                    Keys(Key.Q); Advance(0.15f); break;
                case 5:
                    Keys(); Advance(0.2f); break;
                case 6:
                    Require(!gravity.effective,"Play: Q disables gravity / enables existing jetpack");
                    Keys(Key.Space); Advance(1.6f); break;
                case 7:
                    Require(body.position.y > 8,"Play: Space ascends through the service volume");
                    Keys(); Advance(0.6f); break;
                case 8:
                    Require(body.position.y <= 14.1f && body.position.y > 9,"Play: ceiling contains Zero-G player");
                    var panel = GameObject.Find("Repair panel").transform.position;
                    Require(Vector3.Distance(body.position,panel) < 4,"Play: elevated B station reachable in Zero-G");
                    Keys(Key.Q); Advance(0.15f); break;
                case 9:
                    Keys(); Advance(0.2f); break;
                case 10:
                    Require(gravity.IsGravityActive,"Play: Q restores gravity");
                    before = body.position; Advance(1.0f); break;
                case 11:
                    Require(body.position.y < before.y - 1,"Play: restored gravity pulls player down");
                    Warp(new Vector3(0,1.05f,-8.2f));
                    Keys(Key.T); Advance(0.15f); break;
                case 12:
                    Keys(); Advance(0.8f); break;
                case 13:
                    Require(GlobalState.get().now == TimeState.Past,"Play: T switches Future to Past");
                    Require(GameObject.Find("Floor_PowerRoom").GetComponent<Renderer>().sharedMaterial.name == "Diffuse_01","Play: Past uses intact material");
                    Capture("past-entrance"); Advance(0.4f); break;
                case 14:
                    Keys(Key.T); Advance(0.15f); break;
                case 15:
                    Keys(); Advance(0.8f); break;
                case 16:
                    Require(GlobalState.get().now == TimeState.Future,"Play: T switches Past back to Future");
                    Require(GameObject.Find("Floor_PowerRoom").GetComponent<Renderer>().sharedMaterial.name == "FutureDecay_Diffuse_01","Play: Future restores decay material");
                    Warp(new Vector3(-7.9f,1.05f,-8.5f));
                    Keys(); Advance(0.2f); break;
                case 17:
                    var camera = Camera.main;
                    Require(Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, 4f)
                        && hit.collider.GetComponent<DistortableObject>() != null, "Play: settled camera targets diagnostic display");
                    Keys(Key.G, Key.E); Advance(0.25f); break;
                case 18:
                    var props = new MaterialPropertyBlock();
                    GameObject.Find("Diagnostic distortion display").GetComponent<Renderer>().GetPropertyBlock(props);
                    Require(props.GetFloat("_EffectAmount") > 0,"Play: existing G raycast activates diagnostic distortion");
                    Keys(); Finish(true,"Movement, collisions, Q gravity, Zero-G ascent, T time travel and G distortion passed."); break;
            }
        }
        catch (Exception e) { Finish(false,e.ToString()); }
    }

    private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static void Warp(Vector3 position)
    {
        body.position = position;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }
    private static void Advance(float seconds) { step++; deadline=Time.time+seconds; }
    private static void Capture(string name)
    {
        // Camera render is a scene preview, not proof of the IMGUI HUD or human input testing.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Directory.CreateDirectory("Builds/Stage1");
        var camera = Camera.main;
        var target = new RenderTexture(1280,720,24);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0);
        image.Apply();
        File.WriteAllBytes("Builds/Stage1/"+name+".png",image.EncodeToPNG());
        camera.targetTexture = previous;
        RenderTexture.active = active;
        Object.DestroyImmediate(image);
        target.Release();
        Object.DestroyImmediate(target);
    }
    private static void Finish(bool success,string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Running,false);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        Debug.Log("STAGE1 PLAY " + (success ? "PASSED: " : "FAILED: ") + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    private static void Require(bool condition,string description,bool log=true)
    {
        if (!condition) throw new InvalidOperationException("STAGE1: " + description);
        if (log) Debug.Log("STAGE1 PASS: " + description);
    }
}
