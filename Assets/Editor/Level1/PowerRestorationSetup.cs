using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Authors Power Restoration's power path into its own scene. Never opens the tutorial or Cargo Bay.</summary>
public static class PowerRestorationSetup
{
    public const string ScenePath = "Assets/Scenes/Level1_PowerRestoration.unity";
    static Transform group;
    static Material casing;
    static int interactableLayer;

    // Re-runnable: rebuilds only the "09 / Power path" group and rewires the Level 1 components to it.
    public static void BuildPowerPath()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var old = GameObject.Find("09 / Power path");
        if (old != null) Object.DestroyImmediate(old);
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        var live = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramReady.mat");
        var dead = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramLocked.mat");
        var moduleA = Object.FindFirstObjectByType<PowerModuleA>();
        var moduleB = Object.FindFirstObjectByType<PowerModuleB>();
        var presentation = Object.FindFirstObjectByType<PowerRestorationPresentation>();
        var interact = GameObject.Find("Player").GetComponent<Interact>();
        interactableLayer = Object.FindFirstObjectByType<PowerReactor>().gameObject.layer;
        group = new GameObject("09 / Power path").transform;
        var circuit = group.gameObject.AddComponent<PowerCircuit>();

        // Gravity console beside module A's cradle: live in 2026, seized by 2176 (as in the tutorial).
        Box("Gravity console pedestal", new Vector3(-8.3f, .55f, -2), new Vector3(.8f, 1.1f, 1.2f), casing, true);
        var lever = Box("Gravity switch", new Vector3(-8.3f, 1.25f, -2), new Vector3(.3f, .3f, .7f), live, true);
        lever.layer = interactableLayer;
        var gravityTrigger = lever.AddComponent<GravityTrigger>();
        var gravitySwitch = lever.AddComponent<Switch>();
        Set(gravitySwitch, "triggers", new Object[] { gravityTrigger });
        var seized = Box("Seized gravity switch", new Vector3(-8.3f, 1.2f, -2), new Vector3(.3f, .2f, .7f), casing, false);
        seized.transform.rotation = Quaternion.Euler(18, 0, 0);
        var gravityLabel = Label("Gravity console label", "STATION GRAVITY\nON", new Vector3(-8.6f, 2.1f, -2), -90, .045f);
        Label("Module A mass label", "MODULE A / 380 KG\nZERO-G HANDLING ONLY", moduleA.transform.position + new Vector3(0, 1.25f, 0), 0, .04f);

        // Floor feed from A's socket to the junction at the foot of B's wall.
        Box("A feed cable", new Vector3(-6, .03f, 2), new Vector3(.12f, .04f, 2), casing, false);
        Box("A feed cable", new Vector3(1.2f, .03f, 1), new Vector3(14.4f, .04f, .12f), casing, false);
        var feedLamps = new[] { new Vector3(-6, .07f, 2.2f), new Vector3(-3, .07f, 1), new Vector3(0, .07f, 1), new Vector3(3, .07f, 1), new Vector3(6, .07f, 1) }
            .Select(p => Lamp("A feed lamp", p, dead)).ToArray();
        var junction = Box("B feed junction", new Vector3(8.55f, 1, 1), new Vector3(.5f, 1.2f, 1.4f), casing, true);
        junction.layer = interactableLayer;
        var selector = junction.AddComponent<PowerRouteSelector>();
        var pivot = new GameObject("Route lever pivot").transform; pivot.SetParent(junction.transform, false);
        pivot.position = new Vector3(8.25f, 1.15f, 1);
        var handle = Box("Route lever", pivot.position + new Vector3(0, .25f, 0), new Vector3(.08f, .5f, .08f), live, false);
        handle.transform.SetParent(pivot, true);
        var selectorLabel = Label("Route selector label", "B FEED / ROUTE 1", new Vector3(8.2f, 2, 1), 90, .04f);
        Label("Route 1 label", "ROUTE 1", new Vector3(8.6f, 2, 4), 90, .04f);
        Label("Route 2 label", "ROUTE 2", new Vector3(8.6f, 2, -2), 90, .04f);

        // Route 1 climbs at z 4 and is cut by the 2176 truss collapse at 6.8-8.2 m; route 2 climbs at z -2 intact.
        Box("Route 1 cable", new Vector3(8.85f, 1, 2.5f), new Vector3(.1f, .1f, 3), casing, false);
        Box("Route 1 cable", new Vector3(8.85f, 3.9f, 4), new Vector3(.1f, 5.8f, .1f), casing, false);
        var severed = Box("Route 1 cable (severed by 2176)", new Vector3(8.85f, 7.5f, 4), new Vector3(.1f, 1.4f, .1f), casing, false);
        Box("Route 1 cable", new Vector3(8.85f, 10.55f, 4), new Vector3(.1f, 4.7f, .1f), casing, false);
        Box("Route 1 cable", new Vector3(8.85f, 12.9f, 2.7f), new Vector3(.1f, .1f, 2.6f), casing, false);
        Box("Route 2 cable", new Vector3(8.85f, 1, -.5f), new Vector3(.1f, .1f, 3), casing, false);
        Box("Route 2 cable", new Vector3(8.85f, 6.95f, -2), new Vector3(.1f, 11.9f, .1f), casing, false);
        Box("Route 2 cable", new Vector3(8.85f, 12.9f, -.3f), new Vector3(.1f, .1f, 3.4f), casing, false);
        float[] heights = { 2.5f, 4.5f, 6.3f, 9.5f, 11.5f };
        var routeOne = heights.Select(y => Lamp("Route 1 lamp", new Vector3(8.75f, y, 4), dead)).ToArray();
        var routeTwo = heights.Select(y => Lamp("Route 2 lamp", new Vector3(8.75f, y, -2), dead)).ToArray();
        // Visual only, like Cargo Bay's timeline dressing, so switching time beside the wall cannot trap the player.
        var collapse = new GameObject("2176 truss collapse").transform; collapse.SetParent(group, false);
        Box("Collapsed truss", new Vector3(8.3f, 7.5f, 4), new Vector3(.35f, .35f, 4.5f), casing, false).transform.SetParent(collapse, true);
        collapse.GetChild(0).rotation = Quaternion.Euler(35, 0, 0);
        Box("Torn cable end", new Vector3(8.85f, 6.6f, 4.15f), new Vector3(.1f, .4f, .1f), dead, false).transform.SetParent(collapse, true);

        // Control rods around the reactor plinth: seated under station gravity, drifting without it.
        var rods = new GameObject("Control rods").transform; rods.SetParent(group, false);
        var rodPositions = new[] { new Vector3(1.4f, 0, 3.1f), new Vector3(-1.4f, 0, 3.1f), new Vector3(1.4f, 0, 5.9f), new Vector3(-1.4f, 0, 5.9f) };
        var sockets = rodPositions.Select(p =>
        {
            var socket = Box("Rod socket", p + new Vector3(0, .5f, 0), new Vector3(.55f, .12f, .55f), dead, false);
            socket.transform.SetParent(rods, true); return socket.GetComponent<Renderer>();
        }).ToArray();
        var rodList = rodPositions.Select(p =>
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rod.name = "Control rod";
            Object.DestroyImmediate(rod.GetComponent<Collider>());
            rod.transform.SetParent(rods, false); rod.transform.position = p + new Vector3(0, 1.55f, 0);
            rod.transform.localScale = new Vector3(.32f, 1, .32f); rod.GetComponent<Renderer>().sharedMaterial = casing;
            return rod.transform;
        }).ToArray();
        // Facing the spawn, under the reactor's status display.
        var rodLabel = Label("Control rod status", "CONTROL RODS\nSEATED", new Vector3(0, 1.05f, 1.95f), 0, .045f);
        var warning = new GameObject("Control rod warning light", typeof(Light)); warning.transform.SetParent(rods, false);
        warning.transform.position = new Vector3(0, 2.6f, 2.2f);
        var warningLight = warning.GetComponent<Light>(); warningLight.type = LightType.Point; warningLight.color = new Color(1, .55f, .1f); warningLight.range = 8;

        // Reactor console: A feed, B feed and control rod lamps.
        var consoleLamps = new[] { 3.05f, 3.5f, 3.95f }.Select(x => Lamp("Reactor console lamp", new Vector3(x, 2.02f, 2.8f), dead)).ToArray();
        Label("Reactor console lamp labels", "A FEED   B FEED   RODS", new Vector3(3.5f, 2.16f, 2.78f), 0, .022f);

        SetRef(circuit, "moduleA", moduleA); SetRef(circuit, "moduleB", moduleB);
        Set(circuit, "feedLamps", feedLamps); Set(circuit, "routeOneLamps", routeOne); Set(circuit, "routeTwoLamps", routeTwo);
        Set(circuit, "consoleLamps", consoleLamps); Set(circuit, "controlRods", rodList);
        Set(circuit, "rodSockets", sockets); SetRef(circuit, "rodLabel", rodLabel); SetRef(circuit, "rodWarning", warningLight);
        SetRef(circuit, "floatingRodMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramLocked.mat"));
        Set(circuit, "pastOnly", new Object[] { lever, severed }); Set(circuit, "futureOnly", new Object[] { seized, collapse.gameObject });
        SetRef(circuit, "selectorLabel", selectorLabel); SetRef(circuit, "gravityLabel", gravityLabel);
        SetRef(circuit, "liveMaterial", live); SetRef(circuit, "deadMaterial", dead);
        using (var data = new SerializedObject(circuit)) { data.FindProperty("routeOneBreak").intValue = 3; data.ApplyModifiedPropertiesWithoutUndo(); }
        SetRef(selector, "player", interact); SetRef(selector, "circuit", circuit); SetRef(selector, "lever", pivot);
        SetRef(moduleB, "circuit", circuit); SetRef(presentation, "circuit", circuit);
        // The guide marker now outlines the socket itself, not a target floating above it.
        var marker = (GameObject)new SerializedObject(moduleA).FindProperty("alignmentMarker").objectReferenceValue;
        marker.transform.position = new Vector3(-6, 1.05f, 2.95f);
        // The scene opens in 2176.
        lever.SetActive(false); severed.SetActive(false); seized.SetActive(true); collapse.gameObject.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        // The environment registers its timeline dressing with this circuit, so it is rebuilt after it.
        BuildEnvironment();
    }

    const string Kit = "Assets/3D Modular Kit/3D Free Modular Kit/Prefabs/";

    // Re-runnable: rebuilds only "10 / Environment". Kit cladding, three readable work areas (maintenance bay,
    // reactor hall, distribution wall with a gantry under B) and collider-free 2026/2176 dressing. Every gameplay
    // object keeps its position; nothing solid is placed on the routes the puzzle uses.
    public static void BuildEnvironment()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var old = GameObject.Find("10 / Environment");
        if (old != null) Object.DestroyImmediate(old);
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        var steel = Level1Material("Level1_DarkSteel", new Color(.16f, .17f, .18f), .55f, .45f);
        var hazard = Level1Material("Level1_Hazard", new Color(.86f, .62f, .1f), .1f, .3f);
        var scorch = Level1Material("Level1_Scorch", new Color(.04f, .04f, .04f), 0, .05f);
        var coolant = Level1Material("Level1_Coolant", new Color(.18f, .4f, .55f), .6f, .6f);
        var ready = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramReady.mat");
        var root = new GameObject("10 / Environment").transform;
        var architecture = Child(root, "Architecture");

        // Kit cladding on the three walls without wiring; the distribution wall stays bare so its cables read.
        group = architecture;
        foreach (float z in new[] { -8.25f, -2.75f, 2.75f, 8.25f }) Fit("Wall_Simple_01", new Vector3(-8.85f, 3, z), new Vector3(5.5f, 6, .3f), 90);
        foreach (float x in new[] { -6.25f, 6.25f }) Fit("Wall_Simple_01", new Vector3(x, 3, -10.85f), new Vector3(5.5f, 6, .3f), 0);
        foreach (float x in new[] { -6f, 0f, 6f }) Fit("Wall_Simple_01", new Vector3(x, 3, 10.85f), new Vector3(6, 6, .3f), 180);
        foreach (var p in new[] { new Vector3(-6, 14.3f, 1), new Vector3(6, 14.3f, 1), new Vector3(0, 14.3f, -6), new Vector3(0, 14.3f, 8) })
            Fit("Light_01", p, new Vector3(2, .15f, .6f), 0);

        // Maintenance bay: hazard border around cradle, socket and gravity console; skids under the module.
        foreach (var (center, size) in new[] { (new Vector3(-6.05f, .012f, -3.6f), new Vector3(4.9f, .02f, .12f)), (new Vector3(-6.05f, .012f, 4.6f), new Vector3(4.9f, .02f, .12f)),
            (new Vector3(-3.6f, .012f, .5f), new Vector3(.12f, .02f, 8.2f)), (new Vector3(-8.5f, .012f, .5f), new Vector3(.12f, .02f, 8.2f)) })
            Box("Maintenance bay marking", center, size, hazard, false);
        foreach (float x in new[] { -6.25f, -5.35f }) Box("Module A cradle skid", new Vector3(x, .04f, -.8f), new Vector3(.12f, .08f, 1.1f), steel, false);
        Box("Socket A header", new Vector3(-6, 1.88f, 3.5f), new Vector3(1.9f, .1f, .08f), hazard, false);
        Label("Maintenance bay sign", "MAINTENANCE BAY", new Vector3(-8.6f, 4.4f, 1), -90, .05f);

        // Reactor hall: floor ring, coolant runs to the rear wall, entrance header.
        foreach (var (center, size) in new[] { (new Vector3(0, .012f, 1.6f), new Vector3(5.8f, .02f, .12f)), (new Vector3(0, .012f, 7.4f), new Vector3(5.8f, .02f, .12f)),
            (new Vector3(-2.9f, .012f, 4.5f), new Vector3(.12f, .02f, 5.8f)), (new Vector3(2.9f, .012f, 4.5f), new Vector3(.12f, .02f, 5.8f)) })
            Box("Reactor floor marking", center, size, hazard, false);
        foreach (float x in new[] { -1.1f, 1.1f })
        {
            var pipe = Cylinder("Coolant line", new Vector3(x, 6.5f, 8.6f), new Vector3(.35f, 2.3f, .35f), coolant);
            pipe.transform.rotation = Quaternion.Euler(90, 0, 0);
            Box("Coolant flange", new Vector3(x, 6.5f, 10.7f), new Vector3(.6f, .6f, .15f), steel, false);
        }
        Label("Reactor hall sign", "REACTOR HALL 01", new Vector3(0, 5.2f, -10.7f), 0, .07f);

        // Distribution wall: cable trays behind both routes, a framed junction, and a gantry to stand at B.
        Box("Route 1 tray", new Vector3(8.97f, 6.85f, 4), new Vector3(.04f, 12.7f, .5f), steel, false);
        Box("Route 2 tray", new Vector3(8.97f, 6.85f, -2), new Vector3(.04f, 12.7f, .5f), steel, false);
        Box("Junction frame", new Vector3(8.95f, 1.85f, 1), new Vector3(.06f, .1f, 1.8f), hazard, false);
        Box("Junction frame", new Vector3(8.95f, .15f, 1), new Vector3(.06f, .1f, 1.8f), hazard, false);
        Label("Distribution wall sign", "DISTRIBUTION WALL", new Vector3(8.9f, 4.4f, 1), 90, .05f);
        // B's panel faces the room at x 8.25, z 0.25-2.55. The deck (top 10 m) stands just south of the free climb
        // column in front of it (z 0.9-1.9), so a player can land and repair without hovering.
        Box("B gantry deck", new Vector3(7.3f, 9.85f, -.55f), new Vector3(1.8f, .3f, 1.5f), steel, true);
        Box("B gantry wall beam", new Vector3(8.6f, 9.85f, -.55f), new Vector3(.8f, .2f, .3f), steel, false);
        Box("B gantry edge marking", new Vector3(6.43f, 10.01f, -.55f), new Vector3(.06f, .02f, 1.5f), hazard, false);
        Box("B gantry rail", new Vector3(6.43f, 11, -.55f), new Vector3(.06f, .06f, 1.5f), hazard, false);
        Box("B gantry rail", new Vector3(7.3f, 11, -1.27f), new Vector3(1.8f, .06f, .06f), hazard, false);
        foreach (var p in new[] { new Vector3(6.43f, 10.5f, -1.27f), new Vector3(6.43f, 10.5f, .17f), new Vector3(8.17f, 10.5f, -1.27f) })
            Box("B gantry rail post", p, new Vector3(.06f, 1, .06f), steel, false);
        foreach (float z in new[] { -1.15f, .05f })
        {
            var brace = Box("B gantry brace", new Vector3(8.35f, 9.1f, z), new Vector3(.12f, 1.6f, .12f), steel, false);
            brace.transform.rotation = Quaternion.Euler(0, 0, 32);
        }

        // 2026: the intact roof truss over route 1, working lights. 2176: what fell, dim emergency light.
        var past = Child(root, "2026 only"); var future = Child(root, "2176 only");
        group = past;
        Box("Route 1 roof truss (intact)", new Vector3(8.4f, 9.2f, 4), new Vector3(.35f, .35f, 3.2f), steel, false);
        foreach (float z in new[] { 2.6f, 5.4f }) Box("Truss bracket", new Vector3(8.75f, 9.2f, z), new Vector3(.4f, .5f, .15f), steel, false);
        Box("Gantry beacon", new Vector3(8.95f, 11.1f, -1.55f), new Vector3(.12f, .12f, .12f), ready, false);
        PointLight("Maintenance light", new Vector3(-6, 6.5f, 1), new Color(.92f, .95f, 1), 1.8f, 11);
        PointLight("Distribution light", new Vector3(6.5f, 9, 0), new Color(.92f, .95f, 1), 1.6f, 12);
        PointLight("Entrance light", new Vector3(0, 6.5f, -7), new Color(.92f, .95f, 1), 1.4f, 10);
        group = future;
        Box("Route 1 scorch", new Vector3(8.97f, 7.5f, 4), new Vector3(.02f, 1.8f, 1.1f), scorch, false);
        foreach (var (p, e) in new[] { (new Vector3(8.1f, .15f, 4.7f), new Vector3(10, 20, 0)), (new Vector3(7.7f, .12f, 3.7f), new Vector3(-8, 50, 5)) })
            Box("Truss debris", p, new Vector3(.5f, .25f, .7f), steel, false).transform.rotation = Quaternion.Euler(e);
        foreach (var (p, e) in new[] { (new Vector3(-2, .05f, -5), new Vector3(2, 25, 0)), (new Vector3(5.5f, .05f, 7.3f), new Vector3(0, -40, 3)), (new Vector3(-7.4f, .05f, 7.6f), new Vector3(3, 70, 0)) })
            Box("Fallen ceiling panel", p, new Vector3(1.8f, .06f, 1.1f), steel, false).transform.rotation = Quaternion.Euler(e);
        var fallen = Fit("Light_01", new Vector3(-6, 12.6f, 1), new Vector3(2, .15f, .6f), 0); fallen.transform.rotation = Quaternion.Euler(0, 0, 38);
        PointLight("Maintenance light (failing)", new Vector3(-6, 6.5f, 1), new Color(1, .62f, .3f), .7f, 9);
        PointLight("Distribution light (failing)", new Vector3(6.5f, 9, 0), new Color(1, .62f, .3f), .6f, 10);
        PointLight("Entrance light (failing)", new Vector3(0, 6.5f, -7), new Color(1, .62f, .3f), .5f, 9);

        // Dressing is visual only, so switching time where it appears can never trap the player.
        foreach (var collider in past.GetComponentsInChildren<Collider>(true).Concat(future.GetComponentsInChildren<Collider>(true)))
            Object.DestroyImmediate(collider);
        past.gameObject.SetActive(false); // The scene opens in 2176.

        // Kit pieces weather in 2176 like the rest of the room: an own instance of the shared swap, unchanged.
        var pastKit = AssetDatabase.LoadAssetAtPath<Material>("Assets/3D Modular Kit/3D Free Modular Kit/Meshes/Materials/Diffuse_01.mat");
        var futureKit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FutureDecay_Diffuse_01.mat");
        var surfaces = root.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Contains(pastKit))
            .Select(r => new FutureDecayEnvironment.Surface { renderer = r, past = r.sharedMaterials,
                future = r.sharedMaterials.Select(m => m == pastKit ? futureKit : m).ToArray() }).ToArray();
        var decay = root.gameObject.AddComponent<FutureDecayEnvironment>(); decay.Configure(surfaces);
        EditorUtility.SetDirty(decay);

        // The power path's stand-in housings take the bay's dark steel.
        foreach (string name in new[] { "Gravity console pedestal", "Seized gravity switch", "B feed junction" })
            GameObject.Find(name).GetComponent<Renderer>().sharedMaterial = steel;
        var circuit = Object.FindFirstObjectByType<PowerCircuit>();
        Append(circuit, "pastOnly", past.gameObject); Append(circuit, "futureOnly", future.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        ApplyPresentation();
    }

    // Re-runnable presentation pass: the HUD hint replaces the wall's key sign, and the core gets its online finish.
    public static void ApplyPresentation()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        var sign = GameObject.Find("Module B navigation marker").transform.Cast<Transform>().First(t => t.name.Contains("JETPACK"));
        sign.gameObject.SetActive(false);
        var online = Level1Material("Level1_CoreOnline", new Color(.25f, .85f, .95f), .2f, .8f);
        online.EnableKeyword("_EMISSION"); online.SetColor("_EmissionColor", new Color(.2f, .9f, 1) * 2.2f);
        online.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(online); AssetDatabase.SaveAssets();
        var reactor = Object.FindFirstObjectByType<PowerReactor>();
        SetRef(reactor, "core", GameObject.Find("Inactive core").GetComponent<Renderer>());
        SetRef(reactor, "coreOnline", online);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Audit();
    }

    static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
    }

    static GameObject Cylinder(string name, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder); obj.name = name;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.transform.SetParent(group, false); obj.transform.position = position; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
    }

    static void PointLight(string name, Vector3 position, Color colour, float intensity, float range)
    {
        var lamp = new GameObject(name, typeof(Light)); lamp.transform.SetParent(group, false); lamp.transform.position = position;
        var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.color = colour; light.intensity = intensity; light.range = range;
    }

    // Kit prefab scaled to a world size and centred; colliders removed, since the room shell already bounds the player.
    static GameObject Fit(string prefab, Vector3 center, Vector3 size, float yaw)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + prefab + ".prefab"));
        obj.transform.SetParent(group); obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); obj.transform.localScale = Vector3.one;
        var renderers = obj.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        obj.transform.localScale = new Vector3(size.x / Mathf.Max(.02f, bounds.size.x), size.y / Mathf.Max(.02f, bounds.size.y), size.z / Mathf.Max(.02f, bounds.size.z));
        bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        obj.transform.position += center - bounds.center;
        obj.transform.RotateAround(center, Vector3.up, yaw);
        foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        return obj;
    }

    // Level 1 finishes are copies; the tutorial's casing material is never edited.
    static Material Level1Material(string name, Color colour, float metallic, float smoothness)
    {
        string path = "Assets/Material/Level1/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        if (!AssetDatabase.IsValidFolder("Assets/Material/Level1")) AssetDatabase.CreateFolder("Assets/Material", "Level1");
        material = new Material(casing) { name = name, color = colour };
        material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void Append(Object component, string name, Object value)
    {
        using var data = new SerializedObject(component);
        var property = data.FindProperty(name);
        var items = Enumerable.Range(0, property.arraySize).Select(i => property.GetArrayElementAtIndex(i).objectReferenceValue)
            .Where(o => o != null && o != value).Append(value).ToArray();
        property.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool solid)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
        box.transform.SetParent(group, false); box.transform.position = position; box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    static Renderer Lamp(string name, Vector3 position, Material material) =>
        Box(name, position, new Vector3(.2f, .2f, .2f), material, false).GetComponent<Renderer>();

    // Clones the scene's own legacy TextMesh labels so font, material and anchoring match.
    static TextMesh Label(string name, string text, Vector3 position, float yaw, float size)
    {
        var label = Object.Instantiate(GameObject.Find("A / INSTALLATION SLOT"), group);
        label.name = name; label.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var mesh = label.GetComponent<TextMesh>(); mesh.text = text; mesh.characterSize = size; mesh.color = Color.white;
        return mesh;
    }

    static void SetRef(Object component, string name, Object value)
    {
        using var data = new SerializedObject(component);
        data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(Object component, string name, Object[] values)
    {
        using var data = new SerializedObject(component);
        var property = data.FindProperty(name); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
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
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/power-restoration-audit.txt", $"PASS: opened {scene.path}; {count} objects; no missing scripts/references.\n");
    }
}
