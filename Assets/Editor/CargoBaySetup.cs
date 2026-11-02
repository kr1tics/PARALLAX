using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authors Cargo Bay using the shipped player and modular kit. Never edits other levels.</summary>
public static class CargoBaySetup
{
    public const string ScenePath = "Assets/Scenes/Level2_CargoBay.unity";
    const string Kit = "Assets/3D Modular Kit/3D Free Modular Kit/Prefabs/";
    static Transform environment;
    static Material casing, amber, ready;

    public static void CreateLayout()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Cargo Bay already exists; edit the scene instead of overwriting it.");
        var source = EditorSceneManager.OpenScene("Assets/StartScene.unity", OpenSceneMode.Single);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        foreach (string name in new[] { "Player", "GlobalStateController", "Settings Menu", "EventSystem", "Directional Light" })
        {
            var original = source.GetRootGameObjects().Single(g => g.name == name);
            var clone = Object.Instantiate(original); clone.name = name;
            SceneManager.MoveGameObjectToScene(clone, scene);
        }
        EditorSceneManager.CloseScene(source, true);
        GameObject.Find("Player").transform.position = new Vector3(0, .02f, -10);
        Camera.main.GetComponent<Look>().Rotate(new Vector2(90, 0));
        environment = new GameObject("Cargo Bay - modular freight hall").transform;
        var gravity = environment.gameObject.AddComponent<StationGravity>();
        SetFloat(gravity, "heightAboveDeck", 7);
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        amber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramLocked.mat");
        ready = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramReady.mat");
        // A broad hall with two freight islands and a north-east exit, rather than tutorial rooms.
        for (int x = -9; x <= 9; x += 6)
        for (int z = -9; z <= 9; z += 6)
            Fit("Floor_01", new Vector3(x, -.99f, z), new Vector3(6, .02f, 6));
        Solid("Floor_Cargo backing", new Vector3(0, -1.25f, 0), new Vector3(24, .5f, 24), casing);
        Solid("Sealed cargo ceiling", new Vector3(0, 5.2f, 0), new Vector3(24.5f, .4f, 24.5f), casing);
        for (int i = -9; i <= 9; i += 6)
        {
            Fit("Wall_Simple_01", new Vector3(i, 2, -12), new Vector3(6, 6, .4f));
            Fit("Wall_Simple_01", new Vector3(i, 2, 12), new Vector3(6, 6, .4f), 180);
            Fit("Wall_Simple_01", new Vector3(-12, 2, i), new Vector3(6, 6, .4f), 90);
            Fit("Wall_Simple_01", new Vector3(12, 2, i), new Vector3(6, 6, .4f), -90);
        }
        Solid("South hull", new Vector3(0, 2, -12.2f), new Vector3(24.8f, 6.4f, .4f), casing);
        Solid("North hull", new Vector3(0, 2, 12.2f), new Vector3(24.8f, 6.4f, .4f), casing);
        Solid("West hull", new Vector3(-12.2f, 2, 0), new Vector3(.4f, 6.4f, 24.8f), casing);
        Solid("East hull", new Vector3(12.2f, 2, 0), new Vector3(.4f, 6.4f, 24.8f), casing);
        foreach (float x in new[] { -2.8f, 2.8f })
        {
            Solid("Freight stack", new Vector3(x, .6f, 0), new Vector3(3.6f, 3.2f, 8), casing);
            for (float z = -3; z <= 3; z += 3)
                Fit("Column_01_Top", new Vector3(x, 3.9f, z), new Vector3(.3f, 2.2f, .3f));
        }
        // Full height bulkhead leaves one three metre doorway; a jetpack cannot fly over it.
        Solid("Exit bulkhead west", new Vector3(-3.25f, 2, 9), new Vector3(17.5f, 6.4f, .5f), casing);
        Solid("Exit bulkhead east", new Vector3(10.25f, 2, 9), new Vector3(3.5f, 6.4f, .5f), casing);
        Solid("Exit lintel", new Vector3(7, 3.6f, 9), new Vector3(3, 3.2f, .5f), casing);
        for (int x = -8; x <= 8; x += 8)
        for (int z = -8; z <= 8; z += 8)
        {
            Fit("Light_01", new Vector3(x, 4.8f, z), new Vector3(2, .15f, .6f));
            var lamp = new GameObject("Cargo work light", typeof(Light)); lamp.transform.SetParent(environment);
            lamp.transform.position = new Vector3(x, 3.8f, z);
            var light = lamp.GetComponent<Light>(); light.type = LightType.Point;
            light.range = 12; light.intensity = 1.5f; light.color = new Color(.72f, .85f, 1);
        }
        Sign("Cargo arrival", "02 / CARGO BAY\nTEMPORAL FREIGHT TRANSFER", new Vector3(0, 2.5f, -5), 0, 6);
        Sign("Exit sign", "MAINTENANCE ACCESS", new Vector3(7, 2.5f, 8.68f), 0, 4);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.25f, .28f, .32f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Audit();
    }

    public static void ConnectProgression()
    {
        var tutorialScene = EditorSceneManager.OpenScene("Assets/StartScene.unity", OpenSceneMode.Single);
        var tutorialScreen = Object.FindFirstObjectByType<TutorialCompletionScreen>();
        using (var data = new SerializedObject(tutorialScreen))
        {
            data.FindProperty("nextScene").stringValue = "Level2_CargoBay";
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(tutorialScene);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var root = Object.FindFirstObjectByType<CargoBayController>().gameObject;
        var screen = root.GetComponent<TutorialCompletionScreen>() ?? root.AddComponent<TutorialCompletionScreen>();
        using (var data = new SerializedObject(screen))
        {
            data.FindProperty("nextScene").stringValue = "Level3_MaintenanceShaft";
            data.FindProperty("completionTitle").stringValue = "CARGO BAY COMPLETE";
            data.FindProperty("completionDescription").stringValue = "The cargo transfer restored maintenance access.";
            data.FindProperty("finalMessage").stringValue = "Continue to the maintenance shaft.";
            data.FindProperty("replayLabel").stringValue = "REPLAY CARGO BAY";
            data.FindProperty("allowCompletionShortcut").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        SetObject(screen, "primaryFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Oxanium/Oxanium-Regular SDF.asset"));
        SetObject(screen, "secondaryFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Space_Mono/SpaceMono-Regular SDF.asset"));
        EditorSceneManager.SaveScene(scene);
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == ScenePath);
        int index = scenes.FindIndex(s => s.path == "Assets/StartScene.unity");
        scenes.Insert(index + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Audit();
    }

    static GameObject Fit(string prefab, Vector3 center, Vector3 size, float yaw = 0)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + prefab + ".prefab"));
        obj.transform.SetParent(environment); obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        obj.transform.localScale = Vector3.one;
        var renderers = obj.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        // Kit floor meshes are planar: never divide by their near-zero render thickness.
        obj.transform.localScale = new Vector3(size.x / Mathf.Max(.02f, bounds.size.x), size.y / Mathf.Max(.02f, bounds.size.y), size.z / Mathf.Max(.02f, bounds.size.z));
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        obj.transform.position += center - bounds.center;
        obj.transform.RotateAround(center, Vector3.up, yaw);
        return obj;
    }

    public static GameObject Solid(string name, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
        if (environment != null) obj.transform.SetParent(environment);
        obj.transform.position = position; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
    }

    public static TMP_Text Sign(string name, string value, Vector3 position, float yaw, float width)
    {
        var obj = new GameObject(name, typeof(TextMeshPro));
        if (environment != null) obj.transform.SetParent(environment);
        obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var text = obj.GetComponent<TextMeshPro>(); text.text = value;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Oxanium/Oxanium-Regular SDF.asset");
        text.fontSize = 4; text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(width, 1.2f); text.color = new Color(.75f, .9f, 1);
        return text;
    }

    public static void SetObject(Object component, string name, Object value)
    {
        using var data = new SerializedObject(component);
        data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void BuildPuzzle()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<CargoBayController>() != null) throw new InvalidOperationException("Cargo puzzle already authored.");
        environment = GameObject.Find("Cargo Bay - modular freight hall").transform;
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        amber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramLocked.mat");
        ready = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramReady.mat");
        var source = EditorSceneManager.OpenScene("Assets/StartScene.unity", OpenSceneMode.Additive);
        var tutorial = source.GetRootGameObjects().Single(g => g.name == "Level 1 - Engineering Access").GetComponent<Level1Controller>();
        using var original = new SerializedObject(tutorial);
        var originalCargo = (Entangler)original.FindProperty("introEntangler").objectReferenceValue;
        SceneManager.SetActiveScene(scene);
        var cargo = Object.Instantiate(originalCargo); cargo.name = "Temporal cargo";
        SceneManager.MoveGameObjectToScene(cargo.gameObject, scene);
        EditorSceneManager.CloseScene(source, true);
        using var entangled = new SerializedObject(cargo);
        var past = ((GameObject)entangled.FindProperty("pastObject").objectReferenceValue).GetComponent<Rigidbody>();
        var future = ((GameObject)entangled.FindProperty("futureObject").objectReferenceValue).GetComponent<Rigidbody>();
        past.name = "Cargo - Past"; future.name = "Cargo - Future";
        past.transform.position = new Vector3(-7, -.54f, -6);
        future.transform.position = new Vector3(-8, -.54f, 5);
        foreach (var cube in new[] { past, future })
        {
            cube.transform.localScale = Vector3.one * .9f;
            cube.GetComponent<Renderer>().sharedMaterial = casing;
            cube.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var stripe = Solid("Cargo amber inset", Vector3.zero, new Vector3(.95f, .08f, 1.01f), amber);
            stripe.transform.SetParent(cube.transform, false); stripe.transform.localPosition = Vector3.zero;
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }
        past.gameObject.SetActive(false); future.gameObject.SetActive(true);
        var root = new GameObject("Level 2 - Cargo Bay"); var level = root.AddComponent<CargoBayController>();
        cargo.transform.SetParent(root.transform, true);
        var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/3D Modular Kit/3D Free Modular Kit/Meshes/Materials/Glass.mat");
        Solid("Sealed freight cage front", new Vector3(-8, 1, 3.5f), new Vector3(3.2f, 4, .15f), glass);
        Solid("Sealed freight cage back", new Vector3(-8, 1, 6.5f), new Vector3(3.2f, 4, .15f), casing);
        Solid("Sealed freight cage left", new Vector3(-9.5f, 1, 5), new Vector3(.15f, 4, 3), glass);
        Solid("Sealed freight cage right", new Vector3(-6.5f, 1, 5), new Vector3(.15f, 4, 3), glass);
        Solid("Sealed freight cage roof", new Vector3(-8, 3, 5), new Vector3(3.2f, .15f, 3.2f), casing);
        Sign("Sealed cargo instructions", "FREIGHT SEALED / 2176\nRELOCATE ITS PAST COUNTERPART", new Vector3(-8, 2.2f, 3.38f), 0, 3);
        Sign("Past cargo marker", "CARGO / 2026\nE: CARRY TO THE AMBER PAD", new Vector3(-7, 1.6f, -6.2f), 0, 3);
        var plateObject = Solid("Cargo receiving plate", new Vector3(7, -.88f, 6.5f), new Vector3(2, .24f, 2),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/RelayPlate.mat"));
        var plate = plateObject.AddComponent<PressureButton>(); SetObject(plate, "requiredBody", future);
        var indicator = Solid("Cargo pad indicator", new Vector3(7, -.75f, 6.5f), new Vector3(2.1f, .02f, 2.1f), amber);
        Object.DestroyImmediate(indicator.GetComponent<Collider>());
        var target = new GameObject("Cargo target hologram"); target.transform.SetParent(root.transform);
        var hologram = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/HologramAmber.mat");
        for (int axis = 0; axis < 3; axis++)
        for (int a = -1; a <= 1; a += 2)
        for (int b = -1; b <= 1; b += 2)
        {
            var p = new Vector3(7, -.3f, 6.5f); var size = Vector3.one * .015f;
            p[(axis + 1) % 3] += a * .45f; p[(axis + 2) % 3] += b * .45f; size[axis] = .9f;
            var edge = Solid("Placement outline", p, size, hologram); edge.transform.SetParent(target.transform, true);
            Object.DestroyImmediate(edge.GetComponent<Collider>());
        }
        Sign("Cargo pad label", "CARGO RECEIVING PAD\nPAST POSITION / FUTURE ACCESS", new Vector3(7, 1.5f, 7.8f), 0, 3.7f);
        var doorObject = Solid("Cargo exit door", new Vector3(7, .5f, 9), new Vector3(3, 3, .5f), casing);
        var door = doorObject.AddComponent<Door>(); SetObject(door, "col", doorObject.GetComponent<Collider>()); SetObject(door, "ren", doorObject.GetComponent<Renderer>());
        var zone = new GameObject("Cargo completion zone", typeof(BoxCollider)); zone.transform.SetParent(root.transform);
        zone.transform.position = new Vector3(7, .25f, 10.6f);
        var exit = zone.GetComponent<BoxCollider>(); exit.isTrigger = true; exit.size = new Vector3(2, 2.5f, 1.4f);
        var hudObject = new GameObject("Cargo HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        hudObject.transform.SetParent(root.transform, false);
        hudObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = hudObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        var objective = HudText(hudObject.transform, "Cargo objective", new Vector2(28, -65), new Vector2(620, 105), 23);
        var status = HudText(hudObject.transform, "Cargo timeline", new Vector2(28, -25), new Vector2(1100, 30), 17);
        var prompt = HudText(hudObject.transform, "Cargo interaction", new Vector2(440, -600), new Vector2(600, 35), 20);
        var controls = HudText(hudObject.transform, "Cargo controls", new Vector2(28, -675), new Vector2(1200, 28), 16);
        controls.text = "WASD  MOVE     E / CLICK  CARRY     F / T  TIME     Q  JETPACK     R  RESET CARGO & PLAYER     ESC  SETTINGS";
        SetObject(level, "player", GameObject.Find("Player").GetComponent<PlayerController>());
        SetObject(level, "cargo", cargo); SetObject(level, "pastCrate", past); SetObject(level, "futureCrate", future);
        SetObject(level, "plate", plate); SetObject(level, "exitDoor", door); SetObject(level, "exitZone", exit);
        SetObject(level, "hud", hudObject.GetComponent<CanvasGroup>());
        SetObject(level, "objective", objective); SetObject(level, "status", status); SetObject(level, "prompt", prompt);
        SetObject(level, "doorLabel", GameObject.Find("Exit sign").GetComponent<TMP_Text>());
        SetObject(level, "plateIndicator", indicator.GetComponent<Renderer>());
        SetObject(level, "waitingMaterial", amber); SetObject(level, "readyMaterial", ready);
        SetObject(level, "placementTarget", target);
        SetObject(level, "sounds", Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette"));
        var pastMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/3D Modular Kit/3D Free Modular Kit/Meshes/Materials/Diffuse_01.mat");
        var futureMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FutureDecay_Diffuse_01.mat");
        var surfaces = environment.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterials.Contains(pastMaterial))
            .Select(r => new FutureDecayEnvironment.Surface { renderer = r, past = r.sharedMaterials,
                future = r.sharedMaterials.Select(m => m == pastMaterial ? futureMaterial : m).ToArray() }).ToArray();
        environment.gameObject.AddComponent<FutureDecayEnvironment>().Configure(surfaces);
        EditorSceneManager.SaveScene(scene, ScenePath); PolishPuzzle(); ExpandPuzzle(); RedesignEnvironment();
    }

    // Re-runnable: rebuilds only the groups it owns, after removing the 1.5x stretch from the expanded 24m kit once.
    public static void RedesignEnvironment()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hall = GameObject.Find("Cargo Bay - modular freight hall").transform;
        var level = Object.FindFirstObjectByType<CargoBayController>();
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        amber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramLocked.mat");
        ready = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramReady.mat");
        var steel = Level2Material("Level2_DarkSteel", new Color(.16f, .17f, .18f), .55f, .45f);
        var hazard = Level2Material("Level2_Hazard", new Color(.86f, .62f, .1f), .1f, .3f);
        var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/3D Modular Kit/3D Free Modular Kit/Meshes/Materials/Glass.mat");
        var crate = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Examples & Extras/Materials/Small Crate_diffuse.mat");
        if (hall.localScale != Vector3.one)
        {
            // Keep every authored world transform; only the stretched kit tiles, columns and lamp grid are replaced.
            var saved = hall.Cast<Transform>().Select(c => (c, c.position, c.rotation, c.lossyScale)).ToArray();
            hall.localScale = Vector3.one;
            foreach (var (child, position, rotation, scale) in saved) { child.SetPositionAndRotation(position, rotation); child.localScale = scale; }
            foreach (var (child, _, _, _) in saved)
                if (new[] { "Floor_01", "Wall_Simple_01", "Column_01_Top", "Light_01", "Cargo work light", "Freight stack" }.Contains(child.name))
                    Object.DestroyImmediate(child.gameObject);
            foreach (var text in hall.GetComponentsInChildren<TextMeshPro>(true))
            {
                text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x * text.transform.localScale.x, text.rectTransform.sizeDelta.y);
                text.transform.localScale = Vector3.one;
            }
        }
        foreach (var old in new[] { hall.Find("Cargo Bay - architecture"), level.transform.Find("Cargo Bay - timeline set dressing") })
            if (old != null) Object.DestroyImmediate(old.gameObject);
        environment = new GameObject("Cargo Bay - architecture").transform; environment.SetParent(hall, false);

        // Unstretched 6m modular deck and perimeter.
        for (int x = -15; x <= 15; x += 6)
        for (int z = -15; z <= 15; z += 6)
        {
            var floor = Fit("Floor_01", new Vector3(x, -.99f, z), new Vector3(6, .02f, 6)).transform;
            var mesh = floor.GetComponentInChildren<Renderer>();
            floor.localScale = new Vector3(floor.localScale.x, 1, floor.localScale.z);
            var center = mesh.bounds.center; center.y = -.99f; floor.position += center - mesh.bounds.center;
        }
        for (int i = -15; i <= 15; i += 6)
        {
            Fit("Wall_Simple_01", new Vector3(i, 2, -18), new Vector3(6, 6, .4f));
            Fit("Wall_Simple_01", new Vector3(i, 2, 18), new Vector3(6, 6, .4f), 180);
            Fit("Wall_Simple_01", new Vector3(-18, 2, i), new Vector3(6, 6, .4f), 90);
            Fit("Wall_Simple_01", new Vector3(18, 2, i), new Vector3(6, 6, .4f), -90);
        }

        // Loading: sealed dock shutter behind the arrival point.
        Solid("Dock shutter", new Vector3(0, 1.2f, -17.75f), new Vector3(6, 4.4f, .1f), steel);
        for (float y = -.6f; y < 3.3f; y += .55f) Solid("Dock shutter slat", new Vector3(0, y, -17.69f), new Vector3(6, .05f, .03f), casing);
        foreach (float x in new[] { -3.25f, 3.25f })
        {
            Solid("Dock frame", new Vector3(x, 1.4f, -17.7f), new Vector3(.5f, 4.8f, .3f), hazard);
            Solid("Dock bumper", new Vector3(x * .62f, -.55f, -17.55f), new Vector3(.5f, .6f, .3f), steel);
        }
        Solid("Dock header", new Vector3(0, 3.65f, -17.7f), new Vector3(7, .5f, .3f), hazard);
        Solid("Dock edge marking", new Vector3(0, -.97f, -16.6f), new Vector3(7, .02f, .25f), hazard);

        // Freight hall: two container islands keep the central, north and south lanes open.
        foreach (float x in new[] { -4.2f, 4.2f })
        {
            foreach (float z in new[] { -4.55f, 1.35f }) Solid("Freight container", new Vector3(x, .3f, z), new Vector3(5.4f, 2.6f, 5.9f), crate);
            foreach (float z in new[] { -7.52f, 4.32f }) Solid("Container doors", new Vector3(x, .3f, z), new Vector3(5.2f, 2.4f, .06f), steel);
            Solid("Freight container", new Vector3(x, 2.7f, x < 0 ? -1.5f : 1), new Vector3(5, 2.2f, 5.6f), crate);
        }
        // Gantry crane over the loading lane: a landmark visible from arrival, above head height.
        foreach (float x in new[] { -8.7f, 8.7f }) Solid("Gantry runway", new Vector3(x, 4.75f, -8), new Vector3(.4f, .3f, 10), steel);
        Solid("Gantry bridge", new Vector3(0, 4.3f, -8.5f), new Vector3(17.8f, .45f, .6f), hazard);
        Solid("Gantry hoist", new Vector3(1.5f, 3.8f, -8.5f), new Vector3(.8f, .55f, .9f), steel);

        // West archive mezzanine: 3m up, reached with the jetpack through a gap in the railing.
        Solid("Mezzanine deck", new Vector3(-15.5f, 1.85f, -.5f), new Vector3(5, .3f, 12), steel);
        Solid("Mezzanine edge marking", new Vector3(-13.04f, 2.01f, -.5f), new Vector3(.08f, .02f, 12), hazard);
        foreach (float z in new[] { -6.2f, -2.5f, 1.5f, 5.2f }) Solid("Mezzanine column", new Vector3(-13.3f, .35f, z), new Vector3(.35f, 2.7f, .35f), steel);
        foreach (var (from, to) in new[] { (-6.5f, -3.2f), (-.2f, 5.5f) })
        {
            float middle = (from + to) / 2, length = to - from;
            Solid("Mezzanine rail", new Vector3(-13.15f, 3, middle), new Vector3(.08f, .08f, length), hazard);
            Solid("Mezzanine rail", new Vector3(-13.15f, 2.5f, middle), new Vector3(.06f, .06f, length), steel);
            for (float z = from; z <= to + .01f; z += length / Mathf.Ceil(length / 1.6f))
                Solid("Mezzanine rail post", new Vector3(-13.15f, 2.5f, z), new Vector3(.08f, 1, .08f), steel);
        }
        foreach (float shelf in new[] { 3, 4 }) Solid("Archive rack shelf", new Vector3(-16.9f, shelf, -.5f), new Vector3(1.6f, .08f, 11.6f), steel);
        for (float z = -6.2f; z <= 5.3f; z += 2.875f)
        foreach (float x in new[] { -17.65f, -16.15f })
            Solid("Archive rack upright", new Vector3(x, 3.45f, z), new Vector3(.1f, 2.9f, .1f), steel);
        foreach (var p in new[] { new Vector3(-16.9f, 3.44f, -5), new Vector3(-16.9f, 3.44f, 3.6f), new Vector3(-16.9f, 4.44f, -3.6f),
            new Vector3(-16.9f, 4.44f, 2.2f), new Vector3(-16.9f, 2.44f, -5.3f), new Vector3(-17.2f, -.55f, -4), new Vector3(-17.2f, -.55f, 3) })
            Solid("Archived freight", p, Vector3.one * .8f, crate);

        // Gate frames make both progression doors recognisable from across the hall.
        foreach (float z in new[] { 7.5f, 13.5f })
        {
            foreach (float x in new[] { 8.9f, 12.1f }) Solid("Gate frame", new Vector3(x, .5f, z), new Vector3(.2f, 3, .9f), hazard);
            Solid("Gate header", new Vector3(10.5f, 2.1f, z), new Vector3(3.4f, .2f, .9f), hazard);
        }

        // Receiving: glazed control booth in the west corner.
        Solid("Booth glass", new Vector3(-12, .6f, 11.5f), new Vector3(.1f, 3.2f, 3.2f), glass);
        Solid("Booth roof", new Vector3(-15, 2.3f, 10.45f), new Vector3(6, .2f, 5.4f), steel);
        Solid("Booth console", new Vector3(-16.5f, -.45f, 10.5f), new Vector3(1, 1.1f, 1.6f), steel);
        Object.DestroyImmediate(Solid("Booth console screen", new Vector3(-15.98f, .25f, 10.5f), new Vector3(.04f, .5f, 1.2f), amber).GetComponent<Collider>());
        Sign("Booth sign", "RECEIVING CONTROL", new Vector3(-11.93f, 2.0f, 11.5f), -90, 3).fontSize = 1.8f;
        Sign("Archive sign", "ARCHIVE RACKS", new Vector3(-12.93f, 3.6f, -4.8f), -90, 3).fontSize = 1.8f;

        // Maintenance lift shaft beyond the exit door.
        Solid("Lift platform", new Vector3(10.5f, -.97f, 16), new Vector3(3, .02f, 3), steel);
        Solid("Lift back panel", new Vector3(10.5f, 2, 17.75f), new Vector3(3.6f, 6, .1f), steel);
        foreach (float x in new[] { 8.6f, 12.4f }) Solid("Lift frame", new Vector3(x, 2, 17.6f), new Vector3(.3f, 6, .3f), hazard);

        // Zone lighting replaces the uniform lamp grid; CargoBayController grades it per timeline.
        var lights = new[] {
            ("Loading light", new Vector3(0, 4.2f, -13), new Color(.85f, .9f, 1), 14f, 1.6f, true),
            ("Cage light", new Vector3(-12, 4.2f, -9), new Color(.95f, .85f, .7f), 10f, 1.3f, true),
            ("Freight light", new Vector3(0, 4.4f, -1), new Color(.75f, .85f, 1), 14f, 1.4f, true),
            ("Freight east light", new Vector3(11, 4.2f, -3), new Color(.75f, .85f, 1), 12f, 1.2f, true),
            ("Mezzanine light", new Vector3(-15.5f, 4.6f, -.5f), new Color(1, .85f, .6f), 9f, 1.4f, true),
            ("Receiving light", new Vector3(3, 4.2f, 10.5f), new Color(.8f, .9f, 1), 12f, 1.3f, true),
            ("Receiving east light", new Vector3(13, 4.2f, 10.5f), new Color(.8f, .9f, 1), 10f, 1.3f, true),
            ("Booth light", new Vector3(-15, 1.9f, 10.5f), new Color(.6f, .9f, 1), 6f, 1f, false),
            ("Lift beacon", new Vector3(10.5f, 3.4f, 16.5f), new Color(.35f, 1, .65f), 7f, 1.5f, false) }
            .Select(l =>
            {
                if (l.Item6) Fit("Light_01", new Vector3(l.Item2.x, 4.85f, l.Item2.z), new Vector3(2, .15f, .6f));
                var lamp = new GameObject(l.Item1, typeof(Light)); lamp.transform.SetParent(environment); lamp.transform.position = l.Item2;
                var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.color = l.Item3; light.range = l.Item4; light.intensity = l.Item5;
                return light;
            }).ToArray();

        var signs = environment.parent.GetComponentsInChildren<TMP_Text>(true).ToDictionary(t => t.name, t => t, StringComparer.Ordinal);
        signs["Freight area"].text = "02 / FREIGHT HALL";
        signs["Sealed cargo instructions"].text = "FREIGHT CAGE C1\nSEALED / 2176";
        signs["Past cargo marker"].text = "MANIFEST C1 / 2026";

        // Timeline set dressing is visual only and lives outside the hull, so switching time inside it cannot trap the player.
        var footprint = new GameObject("Pallet footprint", typeof(BoxCollider)); footprint.transform.SetParent(environment);
        footprint.transform.position = new Vector3(15.2f, .2f, -14.4f); footprint.GetComponent<BoxCollider>().size = new Vector3(3, 2.4f, 3.2f);
        var dressing = new GameObject("Cargo Bay - timeline set dressing").transform; dressing.SetParent(level.transform, false);
        var past = new GameObject("Past only").transform; past.SetParent(dressing, false);
        var future = new GameObject("Future only").transform; future.SetParent(dressing, false);
        environment = past;
        Visual("Wrapped pallet", new Vector3(14.6f, -.4f, -14.6f), Vector3.zero, Vector3.one * 1.2f, crate);
        Visual("Wrapped pallet", new Vector3(15.9f, -.4f, -14.6f), Vector3.zero, Vector3.one * 1.2f, crate);
        Visual("Wrapped pallet", new Vector3(15.2f, .8f, -14.6f), new Vector3(0, 8, 0), Vector3.one * 1.2f, crate);
        Visual("Pallet strapping", new Vector3(15.25f, -.4f, -15.22f), Vector3.zero, new Vector3(2.6f, .08f, .02f), hazard);
        environment = future;
        Visual("Toppled pallet", new Vector3(14.4f, -.45f, -14.2f), new Vector3(0, 25, 0), Vector3.one * 1.1f, crate);
        Visual("Toppled pallet", new Vector3(16, -.42f, -15.5f), new Vector3(8, 60, 80), Vector3.one * 1.1f, crate);
        Visual("Toppled pallet", new Vector3(15.6f, -.45f, -13.3f), new Vector3(0, -20, 90), Vector3.one * 1.1f, crate);
        Visual("Fallen ceiling panel", new Vector3(14.5f, -.95f, 0), new Vector3(0, 30, 4), new Vector3(2, .06f, 1.2f), steel);
        Visual("Fallen ceiling panel", new Vector3(-9, -.95f, 12.2f), new Vector3(3, -15, 0), new Vector3(1.8f, .06f, 1.1f), steel);
        Visual("Fallen ceiling panel", new Vector3(5.5f, -.95f, -15.2f), new Vector3(0, 70, 3), new Vector3(1.6f, .06f, 1), steel);
        Visual("Collapsed rack shelf", new Vector3(-16.9f, 2.6f, 4.3f), new Vector3(0, 0, 25), new Vector3(1.6f, .08f, 2.2f), steel);
        Visual("Spilled freight", new Vector3(-14.8f, 2.42f, 4.4f), new Vector3(0, 35, 0), Vector3.one * .8f, crate);
        environment = hall;

        SetArray(level, "zoneLights", lights);
        SetArray(level, "pastOnly", new Object[] { past.gameObject });
        SetArray(level, "futureOnly", new Object[] { future.gameObject });
        var pastMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/3D Modular Kit/3D Free Modular Kit/Meshes/Materials/Diffuse_01.mat");
        var futureMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FutureDecay_Diffuse_01.mat");
        var surfaces = hall.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Contains(pastMaterial))
            .Select(r => new FutureDecayEnvironment.Surface { renderer = r, past = r.sharedMaterials,
                future = r.sharedMaterials.Select(m => m == pastMaterial ? futureMaterial : m).ToArray() }).ToArray();
        hall.GetComponent<FutureDecayEnvironment>().Configure(surfaces);
        EditorUtility.SetDirty(hall.GetComponent<FutureDecayEnvironment>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath); Audit();
    }

    static void Visual(string name, Vector3 position, Vector3 euler, Vector3 scale, Material material)
    {
        var obj = Solid(name, position, scale, material); obj.transform.rotation = Quaternion.Euler(euler);
        Object.DestroyImmediate(obj.GetComponent<Collider>());
    }

    // New Level 2 finishes are copies; the tutorial's casing material is never edited.
    static Material Level2Material(string name, Color colour, float metallic, float smoothness)
    {
        string path = "Assets/Material/Level2/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        if (!AssetDatabase.IsValidFolder("Assets/Material/Level2")) AssetDatabase.CreateFolder("Assets/Material", "Level2");
        material = new Material(casing) { name = name, color = colour };
        material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void SetArray(Object component, string name, Object[] values)
    {
        using var data = new SerializedObject(component);
        var property = data.FindProperty(name); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // Run after BuildPuzzle for a newly authored scene, or once to upgrade the shipped 24m bay.
    public static void ExpandPuzzle()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        environment = GameObject.Find("Cargo Bay - modular freight hall").transform;
        var level = Object.FindFirstObjectByType<CargoBayController>();
        using var data = new SerializedObject(level);
        if (data.FindProperty("stagingPlate").objectReferenceValue != null)
        { PositionReceiving(data); EditorSceneManager.SaveScene(scene); Audit(); return; }
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        amber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramLocked.mat");
        ready = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramReady.mat");
        environment.localScale = new Vector3(1.5f, 1, 1.5f);
        // Move the sealed Future store to loading; it must not overlap the new freight bulkhead.
        foreach (Transform child in environment)
        {
            if (child.name.StartsWith("Sealed freight cage") || child.name == "Sealed cargo instructions")
                child.position += new Vector3(-2, 0, -18.5f);
            if (child.name == "Freight stack") child.position += Vector3.back;
        }
        var past = (Rigidbody)data.FindProperty("pastCrate").objectReferenceValue;
        var future = (Rigidbody)data.FindProperty("futureCrate").objectReferenceValue;
        past.transform.position = new Vector3(-10.5f, -.54f, -9);
        future.transform.position = new Vector3(-14, -.54f, -11);
        data.FindProperty("arrival").vector3Value = new Vector3(0, .02f, -15);
        data.ApplyModifiedPropertiesWithoutUndo();
        ((PlayerController)data.FindProperty("player").objectReferenceValue).transform.position = new Vector3(0, .02f, -15);
        var exit = (BoxCollider)data.FindProperty("exitZone").objectReferenceValue;
        exit.transform.position = new Vector3(10.5f, .25f, 16);
        Box("Freight bulkhead west", new Vector3(-4.5f, 2, 7.5f), new Vector3(27, 6.4f, .5f), casing);
        Box("Freight bulkhead east", new Vector3(15, 2, 7.5f), new Vector3(6, 6.4f, .5f), casing);
        Box("Freight lintel", new Vector3(10.5f, 3.6f, 7.5f), new Vector3(3, 3.2f, .5f), casing);
        var gate = Box("Freight access door", new Vector3(10.5f, .5f, 7.5f), new Vector3(3, 3, .5f), casing);
        var door = gate.AddComponent<Door>(); SetObject(door, "col", gate.GetComponent<Collider>()); SetObject(door, "ren", gate.GetComponent<Renderer>());
        var pad = Box("A - Archive staging plate", new Vector3(-10.5f, -.88f, -3), new Vector3(2.4f, .24f, 2.4f),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/RelayPlate.mat"));
        var pressure = pad.AddComponent<PressureButton>(); SetObject(pressure, "requiredBody", future);
        var rim = Box("A - Staging indicator", new Vector3(-10.5f, -.75f, -3), new Vector3(2.5f, .02f, 2.5f), amber);
        Object.DestroyImmediate(rim.GetComponent<Collider>());
        var label = Sign("Archive staging instructions", "A / ARCHIVE STAGING\nLEAVE THE PAST CRATE HERE", new Vector3(-10.5f, 1.8f, -1.5f), 0, 3.5f);
        label.fontSize = 1.8f;
        SetObject(level, "stagingPlate", pressure); SetObject(level, "freightDoor", door);
        SetObject(level, "stagingLabel", label); SetObject(level, "stagingIndicator", rim.GetComponent<Renderer>());
        GameObject.Find("Cargo pad label").GetComponent<TMP_Text>().text = "B / FUTURE RECEIVING\nPAST MUST REMAIN AT A";
        GameObject.Find("Past cargo marker").GetComponent<TMP_Text>().text = "LOADING / PAST CARGO\nE: CARRY TO STAGING PAD A";
        foreach (var info in new[] {
            ("Loading area", "01 / LOADING", new Vector3(0, 2.8f, -12)),
            ("Freight area", "02 / FREIGHT\nNORTH AISLE / SOUTH DETOUR", new Vector3(0, 3.3f, 0)),
            ("Receiving area", "03 / RECEIVING", new Vector3(10.5f, 2.7f, 7.15f)) })
        {
            var sign = Sign(info.Item1, info.Item2, info.Item3, 0, 5); sign.fontSize = 1.8f;
        }
        // Two routes around the stacks: short northern aisle and wider southern loading detour.
        foreach (var route in new[] {
            (new Vector3(0, -.97f, 6.2f), new Vector3(19, .02f, .08f)),
            (new Vector3(0, -.97f, -10.7f), new Vector3(21, .02f, .08f)),
            (new Vector3(10.5f, -.97f, -2), new Vector3(.08f, .02f, 17)) })
        {
            var line = Box("Freight route strip", route.Item1, route.Item2, amber);
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }
        var completion = level.gameObject.AddComponent<TutorialCompletionScreen>();
        SetObject(completion, "cargoLevel", level);
        SetObject(completion, "primaryFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Oxanium/Oxanium-Regular SDF.asset"));
        SetObject(completion, "secondaryFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Space_Mono/SpaceMono-Regular SDF.asset"));
        using (var screen = new SerializedObject(completion))
        {
            screen.FindProperty("nextScene").stringValue = "Level3_MaintenanceShaft";
            screen.FindProperty("allowCompletionShortcut").boolValue = false;
            screen.ApplyModifiedPropertiesWithoutUndo();
        }
        PositionReceiving(data);
        EditorSceneManager.SaveScene(scene, ScenePath); Audit();
    }

    static void PositionReceiving(SerializedObject level)
    {
        ((PressureButton)level.FindProperty("plate").objectReferenceValue).transform.position = new Vector3(13.5f, -.88f, 10);
        ((Renderer)level.FindProperty("plateIndicator").objectReferenceValue).transform.position = new Vector3(13.5f, -.75f, 10);
        ((GameObject)level.FindProperty("placementTarget").objectReferenceValue).transform.position = new Vector3(6.5f, 0, 3.5f);
        GameObject.Find("Cargo pad label").transform.position = new Vector3(13.5f, 1.8f, 12);
    }

    static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
    {
        var obj = Solid(name, position, size, material);
        var scale = environment.lossyScale;
        obj.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
        return obj;
    }

    public static void PolishPuzzle()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        environment = GameObject.Find("Cargo Bay - modular freight hall").transform;
        foreach (Transform child in environment)
        {
            if (child.name != "Floor_01") continue;
            var floor = child.GetComponentInChildren<Renderer>();
            var center = floor.bounds.center; center.y = -.99f;
            child.localScale = new Vector3(child.localScale.x, 1, child.localScale.z);
            child.position += center - floor.bounds.center;
        }
        if (environment.GetComponent<StationHull>() == null) environment.gameObject.AddComponent<StationHull>();
        var crateFinish = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Examples & Extras/Materials/Small Crate_diffuse.mat");
        foreach (var renderer in environment.GetComponentsInChildren<Renderer>())
            if (renderer.name == "Freight stack") renderer.sharedMaterial = crateFinish;
        foreach (var text in environment.GetComponentsInChildren<TMP_Text>()) text.fontSize = 1.8f;
        var arrivalSign = GameObject.Find("Cargo arrival").GetComponent<TMP_Text>();
        arrivalSign.text = "02 / CARGO BAY"; arrivalSign.transform.position = new Vector3(0, 3.8f, -4.7f);
        var hud = GameObject.Find("Cargo HUD").transform;
        if (hud.Find("Objective backing") == null)
        {
            var panel = new GameObject("Objective backing", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(hud, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(16, -15); rect.sizeDelta = new Vector2(640, 145);
            panel.GetComponent<Image>().color = new Color(.015f, .025f, .035f, .86f); panel.GetComponent<Image>().raycastTarget = false;
            panel.transform.SetAsFirstSibling();
        }
        EditorSceneManager.SaveScene(scene, ScenePath); Audit();
    }

    static TMP_Text HudText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = obj.GetComponent<TextMeshProUGUI>(); text.fontSize = fontSize; text.raycastTarget = false;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Oxanium/Oxanium-Regular SDF.asset");
        text.color = new Color(.86f, .92f, .96f); return text;
    }
    static void SetFloat(Object component, string name, float value)
    {
        using var data = new SerializedObject(component);
        data.FindProperty(name).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Audit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                throw new InvalidOperationException("Missing script: " + transform.name);
            foreach (var component in transform.GetComponents<Component>())
            {
                using var data = new SerializedObject(component); var property = data.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        throw new InvalidOperationException("Broken reference: " + transform.name + "/" + property.propertyPath);
            }
            count++;
        }
        Physics.SyncTransforms();
        Vector3 spawn = GameObject.Find("Player").transform.position;
        if (!Physics.Raycast(spawn, Vector3.down, 2)) throw new InvalidOperationException("No spawn floor.");
        if (!Physics.Raycast(spawn, Vector3.up, 6)) throw new InvalidOperationException("No sealed roof.");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/cargo-scene-audit.txt", $"PASS: opened {scene.path}; {count} objects; no missing scripts/references; spawn floor and roof present.\n");
    }
}
