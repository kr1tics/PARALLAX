using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Authored machinery meshes for Cold Storage (lathe and pipe-route generators from the original Puzzle 3 work)
/// plus main's modular kit pieces. Mesh assets live under Assets/Models/CoolantExchange.</summary>
public static class ColdStorageModels
{
    public const string Kit = "Assets/3D Modular Kit/3D Free Modular Kit/";
    const string Materials = "Assets/Material/CoolantExchange/";
    const string Meshes = "Assets/Models/CoolantExchange/";
    static List<FutureDecayEnvironment.Surface> weather;
    static Material kitPast, kitFuture;
    static int routeIndex;

    public static void Begin(List<FutureDecayEnvironment.Surface> surfaces)
    {
        weather = surfaces; routeIndex = 0;
        kitPast = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Meshes/Materials/Diffuse_01.mat");
        kitFuture = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FutureDecay_Diffuse_01.mat");
    }

    // A tinted copy of the kit atlas material, cropped to a panel region; glow > 0 makes it emissive.
    public static Material Finish(string name, Color tint, float metallic, float glow = 0, bool crop = true)
    {
        var path = Materials + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(kitPast); AssetDatabase.CreateAsset(material, path); }
        material.color = tint; material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", .34f); material.SetFloat("_BumpScale", .45f);
        material.DisableKeyword("_METALLICGLOSSMAP"); material.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * glow);
        if (glow > 0) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
        var scale = crop ? new Vector2(.45f, .44f) : Vector2.one; var offset = crop ? new Vector2(.025f, .535f) : Vector2.zero;
        foreach (string property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) { material.SetTextureScale(property, scale); material.SetTextureOffset(property, offset); }
        return material;
    }

    // Registers a 2176 aged version of a finish with the project's Future Environment Decay shader.
    public static void Weather(Renderer renderer, Material original)
    {
        if (original == kitPast) { weather.Add(new FutureDecayEnvironment.Surface { renderer = renderer, past = new[] { kitPast }, future = new[] { kitFuture } }); return; }
        var path = Materials + original.name + "Aged.mat"; var aged = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (aged == null) { aged = new Material(Shader.Find("COMP30019/Future Environment Decay")); AssetDatabase.CreateAsset(aged, path); }
        aged.mainTexture = original.mainTexture; aged.mainTextureScale = original.mainTextureScale; aged.mainTextureOffset = original.mainTextureOffset; aged.color = original.color;
        aged.SetFloat("_DecayAmount", .5f); aged.SetFloat("_DamageStrength", .35f); aged.SetFloat("_GrimeStrength", .4f); aged.SetFloat("_FlickerStrength", 0);
        weather.Add(new FutureDecayEnvironment.Surface { renderer = renderer, past = new[] { original }, future = new[] { aged } });
    }

    // Main's kit piece fitted to a box, visual only (colliders, audio, animators and scripts stripped), 2176 decay registered.
    public static GameObject KitPiece(string prefab, Vector3 center, Vector3 size, float yaw, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/" + prefab + ".prefab"));
        go.name = "Station kit / " + prefab;
        go.transform.SetParent(parent); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
        foreach (var animator in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
        foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) Object.DestroyImmediate(script);
        foreach (var child in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        var renderers = go.GetComponentsInChildren<Renderer>(); Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        // Kit floor meshes are planar: never divide by their near-zero render thickness.
        go.transform.localScale = new Vector3(size.x / Mathf.Max(.02f, bounds.size.x), size.y / Mathf.Max(.02f, bounds.size.y), size.z / Mathf.Max(.02f, bounds.size.z));
        bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.position += center - bounds.center; go.transform.RotateAround(center, Vector3.up, yaw);
        foreach (var r in renderers)
        {
            var past = r.sharedMaterials; var future = (Material[])past.Clone();
            for (int i = 0; i < future.Length; i++) if (future[i] == kitPast) future[i] = kitFuture;
            weather.Add(new FutureDecayEnvironment.Surface { renderer = r, past = past, future = future });
        }
        return go;
    }

    public static GameObject Box(string name, Vector3 p, Vector3 size, Material material, Transform parent, bool aged = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = p; go.transform.localScale = size;
        go.transform.SetParent(parent, true); Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
        if (aged) Weather(go.GetComponent<Renderer>(), material);
        return go;
    }

    public static GameObject Shape(string name, Mesh mesh, Vector3 p, Material material, Transform parent, bool aged = true, Quaternion? rotation = null)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.position = p; go.transform.rotation = rotation ?? Quaternion.identity;
        go.transform.SetParent(parent, true); go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = material;
        if (aged) Weather(go.GetComponent<Renderer>(), material); return go;
    }

    public static void Ring(string name, Vector3 p, float radius, float thickness, Material material, Transform parent, Quaternion? rotation = null, bool aged = true)
    {
        var profile = new Vector2[13]; for (int i = 0; i < profile.Length; i++) { float a = i * Mathf.PI * 2 / 12; profile[i] = V(radius + Mathf.Cos(a) * thickness, Mathf.Sin(a) * thickness); }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Shape(name, Lathe("Ring_" + radius.ToString("0.000", inv) + "_" + thickness.ToString("0.000", inv), profile), p, material, parent, aged, rotation);
    }

    // A rounded pipe through the given corners.
    public static GameObject Route(string name, Vector3[] corners, float radius, Material material, Transform parent, bool aged = true)
    {
        var points = new List<Vector3> { corners[0] };
        for (int i = 1; i < corners.Length - 1; i++)
        {
            float trim = Mathf.Min(.24f, (corners[i] - corners[i - 1]).magnitude * .3f, (corners[i + 1] - corners[i]).magnitude * .3f);
            Vector3 a = corners[i] + (corners[i - 1] - corners[i]).normalized * trim, b = corners[i] + (corners[i + 1] - corners[i]).normalized * trim;
            points.Add(a); for (int n = 1; n <= 6; n++) { float t = n / 6f; points.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * corners[i] + t * t * b); }
        }
        points.Add(corners[corners.Length - 1]);
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); float distance = 0, length = 0;
        for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i], points[i - 1]);
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) distance += Vector3.Distance(points[i], points[i - 1]);
            Vector3 tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
            Vector3 side = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > .95f ? Vector3.forward : Vector3.up).normalized, up = Vector3.Cross(side, tangent).normalized;
            for (int n = 0; n <= 12; n++) { float a = n * Mathf.PI * 2 / 12; vertices.Add(points[i] + radius * (Mathf.Cos(a) * side + Mathf.Sin(a) * up)); uv.Add(new Vector2(n / 12f, distance / Mathf.Max(.01f, length))); }
            if (i == 0) continue; for (int n = 0; n < 12; n++) { int a = (i - 1) * 13 + n, b = i * 13 + n; triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
        }
        var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return Shape(name, SaveMesh("Cold_" + name.Replace(' ', '_') + "_" + routeIndex++, mesh), Vector3.zero, material, parent, aged);
    }

    public static Vector2 V(float radius, float height) => new Vector2(radius, height);
    public static Mesh Lathe(string name, Vector2[] profile)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); const int sides = 40;
        for (int ring = 0; ring < profile.Length; ring++)
        {
            for (int n = 0; n <= sides; n++) { float a = n * Mathf.PI * 2 / sides; vertices.Add(new Vector3(Mathf.Cos(a) * profile[ring].x, profile[ring].y, Mathf.Sin(a) * profile[ring].x)); uv.Add(new Vector2(n / (float)sides, ring / (float)(profile.Length - 1))); }
            if (ring == 0) continue; for (int n = 0; n < sides; n++) { int a = (ring - 1) * (sides + 1) + n, b = ring * (sides + 1) + n; triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
        }
        var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return SaveMesh(name, mesh);
    }
    static Mesh SaveMesh(string name, Mesh mesh)
    {
        mesh.RecalculateTangents();
        string path = Meshes + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); return existing;
    }

    // The primary heat exchanger, built from the original lathe meshes; there is no second reactor in this room.
    public static Transform Exchanger(Vector3 p, Material casing, Material steel, Material coolant, Transform parent)
    {
        var root = new GameObject("Primary heat exchanger").transform; root.SetParent(parent); root.position = p;
        Shape("Exchanger plinth", Lathe("ReactorPlinth", new[] { V(0, 0), V(1.3f, 0), V(1.3f, .2f), V(1.1f, .4f), V(1.1f, .58f), V(0, .58f) }), p, steel, root);
        Shape("Exchanger shell", Lathe("ReactorCore", new[] { V(0, 0), V(.72f, 0), V(.92f, .18f), V(.92f, 3.2f), V(.72f, 3.4f), V(0, 3.4f) }), p + Vector3.up * .65f, casing, root);
        Shape("Exchanger sight glass", Lathe("PortableWindow", new[] { V(.405f, -.25f), V(.405f, .25f) }), p + Vector3.up * 2.3f, coolant, root, false, null);
        root.Find("Exchanger sight glass").localScale = new Vector3(2.35f, 1.6f, 2.35f);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4; var at = p + new Vector3(Mathf.Cos(angle) * 1.08f, 2.3f, Mathf.Sin(angle) * 1.08f);
            Box("Exchanger tie rod", at, new Vector3(.12f, 3.4f, .15f), steel, root, true).transform.rotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
        }
        foreach (float y in new[] { .65f, 1.03f, 3.9f, 4.3f }) Ring("Exchanger flange", p + Vector3.up * y, 1.15f, .10f, steel, root);
        Shape("Exchanger head", Lathe("ReactorCap", new[] { V(0, 0), V(1.15f, 0), V(1.15f, .16f), V(.82f, .36f), V(.45f, .5f), V(0, .5f) }), p + Vector3.up * 4.3f, casing, root);
        var collider = root.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 2.4f, 0); collider.size = new Vector3(2.4f, 4.8f, 2.4f);
        root.gameObject.layer = LayerMask.NameToLayer("Structure");
        return root;
    }

    // Loop expansion tank; returns the visible coolant column so the controller can show the loop's state.
    public static GameObject Tank(Vector3 p, Material steel, Material casing, Material glass, Material coolant, Transform parent)
    {
        var root = new GameObject("Loop expansion tank").transform; root.SetParent(parent); root.position = p;
        Shape("Tank foot", Lathe("TankFoot", new[] { V(0, 0), V(.72f, 0), V(.72f, .18f), V(.58f, .34f), V(0, .34f) }), p, steel, root);
        Shape("Tank glass jacket", Lathe("TankGlass", new[] { V(.58f, .34f), V(.58f, 2.2f) }), p, glass, root, false);
        var liquid = Shape("Tank coolant", Lathe("TankLiquid", new[] { V(0, .35f), V(.53f, .35f), V(.53f, 1.78f), V(0, 1.78f) }), p, coolant, root, false);
        Shape("Tank pressure dome", Lathe("TankDome", new[] { V(0, 0), V(.62f, 0), V(.62f, .17f), V(.48f, .37f), V(.18f, .48f), V(0, .48f) }), p + Vector3.up * 2.18f, steel, root);
        foreach (float y in new[] { .35f, .8f, 1.4f, 2.18f }) Ring("Tank retaining band", p + Vector3.up * y, .60f, .055f, casing, root);
        var collider = root.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.3f, 0); collider.size = new Vector3(1.45f, 2.6f, 1.45f);
        return liquid;
    }

    // Portable cartridge visuals around a 0.9 m cell body; returns the coolant window renderer.
    public static Renderer Cell(Transform cell, Material casing, Material steel, Material coolant, Material stripe, Material rubber)
    {
        var p = cell.position;
        Shape("Cell vessel", Lathe("PortableCell", new[] { V(0, -.42f), V(.34f, -.42f), V(.40f, -.31f), V(.40f, .31f), V(.34f, .42f), V(0, .42f) }), p, casing, cell, false);
        foreach (float y in new[] { -.38f, .38f }) Ring("Cell coupling ring", p + Vector3.up * y, .40f, .045f, steel, cell, null, false);
        var window = Shape("Cell coolant window", Lathe("PortableWindow", new[] { V(.405f, -.25f), V(.405f, .25f) }), p, coolant, cell, false);
        Ring("Cell identity band", p + Vector3.up * .3f, .41f, .03f, stripe, cell, null, false);
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.PI * .25f + i * Mathf.PI * .5f;
            Box("Cell protective corner", p + new Vector3(Mathf.Cos(a) * .37f, 0, Mathf.Sin(a) * .37f), new Vector3(.12f, .86f, .12f), steel, cell);
        }
        Box("Cell carry handle", p + Vector3.up * .45f, new Vector3(.64f, .06f, .13f), rubber, cell);
        return window.GetComponent<Renderer>();
    }

    public static Transform Handwheel(Vector3 p, Material wheel, Material steel, Transform parent)
    {
        var root = new GameObject("Valve handwheel").transform; root.SetParent(parent); root.position = p;
        Ring("Valve grip wheel", p, .29f, .038f, wheel, root, Quaternion.Euler(90, 0, 0));
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5;
            Route("Valve wheel spoke", new[] { p, p + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .27f }, .022f, steel, root);
        }
        Shape("Valve centre spindle", Lathe("ValveHub", new[] { V(0, 0), V(.075f, 0), V(.075f, .08f), V(0, .08f) }), p, steel, root, true, Quaternion.Euler(90, 0, 0));
        return root;
    }
}
