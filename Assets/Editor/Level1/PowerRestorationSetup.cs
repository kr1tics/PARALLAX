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
        var rodList = new[] { new Vector3(1.4f, 0, 3.1f), new Vector3(-1.4f, 0, 3.1f), new Vector3(1.4f, 0, 5.9f), new Vector3(-1.4f, 0, 5.9f) }.Select(p =>
        {
            Box("Rod socket", p + new Vector3(0, .5f, 0), new Vector3(.4f, .1f, .4f), casing, false).transform.SetParent(rods, true);
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rod.name = "Control rod";
            Object.DestroyImmediate(rod.GetComponent<Collider>());
            rod.transform.SetParent(rods, false); rod.transform.position = p + new Vector3(0, 1.15f, 0);
            rod.transform.localScale = new Vector3(.2f, .7f, .2f); rod.GetComponent<Renderer>().sharedMaterial = casing;
            return rod.transform;
        }).ToArray();

        // Reactor console: A feed, B feed and control rod lamps.
        var consoleLamps = new[] { 3.05f, 3.5f, 3.95f }.Select(x => Lamp("Reactor console lamp", new Vector3(x, 2.02f, 2.8f), dead)).ToArray();
        Label("Reactor console lamp labels", "A FEED   B FEED   RODS", new Vector3(3.5f, 2.16f, 2.78f), 0, .022f);

        SetRef(circuit, "moduleA", moduleA); SetRef(circuit, "moduleB", moduleB);
        Set(circuit, "feedLamps", feedLamps); Set(circuit, "routeOneLamps", routeOne); Set(circuit, "routeTwoLamps", routeTwo);
        Set(circuit, "consoleLamps", consoleLamps); Set(circuit, "controlRods", rodList);
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
        Audit();
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
