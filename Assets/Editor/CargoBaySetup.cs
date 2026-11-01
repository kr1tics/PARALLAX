using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            Fit("Floor_01", new Vector3(x, -1.15f, z), new Vector3(6, .3f, 6));
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
