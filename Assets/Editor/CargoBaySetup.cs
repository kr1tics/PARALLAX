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
        EditorSceneManager.SaveScene(scene, ScenePath); PolishPuzzle();
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
        if (!Physics.Raycast(new Vector3(0, 0, -10), Vector3.down, 2)) throw new InvalidOperationException("No spawn floor.");
        if (!Physics.Raycast(new Vector3(0, 0, -10), Vector3.up, 6)) throw new InvalidOperationException("No sealed roof.");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/cargo-scene-audit.txt", $"PASS: opened {scene.path}; {count} objects; no missing scripts/references; spawn floor and roof present.\n");
    }
}
