using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporarily applies the team's existing object shader to an interacted object.</summary>
public sealed class InteractionDistortion : MonoBehaviour
{
    private sealed class Surface
    {
        public Renderer renderer;
        public Material[] originals, effects;
        public MaterialPropertyBlock originalProperties;
        public MeshFilter filter;
        public Mesh originalMesh, denseMesh;
    }

    private readonly List<Surface> surfaces = new List<Surface>();
    private MaterialPropertyBlock properties;
    private static readonly int EffectAmount = Shader.PropertyToID("_EffectAmount");
    private Coroutine pulse;
    private Texture2D fallbackTexture;
    private bool held;
    private bool released;

    private void Awake() => properties = new MaterialPropertyBlock();

    public static void Play(GameObject target)
    {
        StartEffect(target, false);
    }

    public static void BeginHolding(GameObject target) => StartEffect(target, true);

    public static void EndHolding(GameObject target)
    {
        var effect = target.GetComponent<InteractionDistortion>();
        if (effect == null) return;
        effect.held = false;
        effect.released = true;
    }

    private static void StartEffect(GameObject target, bool hold)
    {
        if (!target.activeInHierarchy) return;
        var effect = target.GetComponent<InteractionDistortion>();
        if (effect == null) effect = target.AddComponent<InteractionDistortion>();
        effect.enabled = true;
        effect.held = hold;
        effect.released = false;
        effect.Restart();
    }

    private void Restart()
    {
        if (pulse != null) StopCoroutine(pulse);
        Restore();
        var template = Resources.Load<Material>("InteractionDistortion");
        if (template == null) return;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            var surface = new Surface { renderer = renderer, originals = renderer.sharedMaterials,
                originalProperties = new MaterialPropertyBlock() };
            renderer.GetPropertyBlock(surface.originalProperties);
            surface.effects = new Material[surface.originals.Length];
            for (int i = 0; i < surface.effects.Length; i++)
            {
                var source = surface.originals[i];
                var material = new Material(template);
                // Flat colours cannot show RGB sample offsets. Supply a subtle grid only
                // during the pulse; textured surfaces keep their own artwork and UV scale.
                material.mainTexture = GetFallbackTexture();
                if (source != null)
                {
                    if (source.HasProperty("_Color")) material.color = source.color;
                    if (source.HasProperty("_MainTex") && source.mainTexture != null &&
                        source.mainTexture != Texture2D.whiteTexture)
                    {
                        material.mainTexture = source.mainTexture;
                        material.mainTextureScale = source.mainTextureScale;
                        material.mainTextureOffset = source.mainTextureOffset;
                    }
                }
                surface.effects[i] = material;
            }
            surface.filter = renderer.GetComponent<MeshFilter>();
            if (surface.filter != null)
            {
                surface.originalMesh = surface.filter.sharedMesh;
                surface.denseMesh = SubdivideCoarseMesh(surface.originalMesh);
                if (surface.denseMesh != null) surface.filter.sharedMesh = surface.denseMesh;
            }
            renderer.sharedMaterials = surface.effects;
            surfaces.Add(surface);
        }
        pulse = StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        // Unscaled time allows the effect to finish while settings pause gameplay.
        float elapsed = 0f;
        while (elapsed < .2f)
        {
            SetAmount(Mathf.Clamp01(elapsed / .2f));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        SetAmount(1f);
        while (held || (!released && elapsed < 1.2f))
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < .5f)
        {
            SetAmount(1f - Mathf.Clamp01(elapsed / .5f));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        Restore();
        pulse = null;
    }

    private void SetAmount(float amount)
    {
        foreach (var surface in surfaces)
        {
            if (surface.renderer == null) continue;
            surface.renderer.GetPropertyBlock(properties);
            properties.SetFloat(EffectAmount, amount);
            surface.renderer.SetPropertyBlock(properties);
        }
    }

    private void Restore()
    {
        foreach (var surface in surfaces)
        {
            if (surface.renderer != null)
            {
                // Keep materials changed by puzzle/time-state logic during the pulse.
                var current = surface.renderer.sharedMaterials;
                for (int i = 0; i < current.Length && i < surface.effects.Length; i++)
                    if (current[i] == surface.effects[i]) current[i] = surface.originals[i];
                surface.renderer.sharedMaterials = current;
                // Preserve unrelated property changes (weather, flicker, etc.).
                surface.renderer.GetPropertyBlock(properties);
                properties.SetFloat(EffectAmount, surface.originalProperties.GetFloat(EffectAmount));
                surface.renderer.SetPropertyBlock(properties);
            }
            if (surface.filter != null && surface.filter.sharedMesh == surface.denseMesh)
                surface.filter.sharedMesh = surface.originalMesh;
            if (surface.denseMesh != null) Destroy(surface.denseMesh);
            foreach (var material in surface.effects) Destroy(material);
        }
        surfaces.Clear();
    }

    private void OnDisable()
    {
        if (pulse != null) StopCoroutine(pulse);
        pulse = null;
        Restore();
    }

    private Texture2D GetFallbackTexture()
    {
        if (fallbackTexture != null) return fallbackTexture;
        fallbackTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            name = "Interaction pulse grid", wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float value = x % 16 < 2 || y % 16 < 2 ? .45f : 1f;
            pixels[y * 64 + x] = new Color(value, value, value, 1f);
        }
        fallbackTexture.SetPixels(pixels);
        fallbackTexture.Apply(false, true);
        return fallbackTexture;
    }

    private void OnDestroy()
    {
        if (fallbackTexture != null) Destroy(fallbackTexture);
    }

    // Extra vertices support the existing vertex wobble. UVs and hard edges are retained;
    // physics continues using the original collider, and already-dense meshes are untouched.
    private static Mesh SubdivideCoarseMesh(Mesh source)
    {
        if (source == null || !source.isReadable || source.vertexCount >= 32 || source.vertexCount == 0)
            return null;
        for (int sub = 0; sub < source.subMeshCount; sub++)
            if (source.GetTopology(sub) != MeshTopology.Triangles) return null;
        Mesh mesh = Instantiate(source);
        mesh.name = source.name + " (interaction subdivisions)";
        for (int pass = 0; pass < 3 && mesh.vertexCount < 32; pass++)
        {
            var vertices = new List<Vector3>(mesh.vertices);
            var normals = new List<Vector3>(mesh.normals);
            var uv = new List<Vector2>(mesh.uv);
            bool hasNormals = normals.Count == vertices.Count, hasUV = uv.Count == vertices.Count;
            var edges = new Dictionary<ulong, int>();
            var triangles = new List<int>[mesh.subMeshCount];
            int Midpoint(int a, int b)
            {
                ulong key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                if (edges.TryGetValue(key, out int index)) return index;
                index = vertices.Count;
                vertices.Add((vertices[a] + vertices[b]) * .5f);
                if (hasNormals) normals.Add((normals[a] + normals[b]).normalized);
                if (hasUV) uv.Add((uv[a] + uv[b]) * .5f);
                edges.Add(key, index);
                return index;
            }
            for (int sub = 0; sub < triangles.Length; sub++)
            {
                var original = mesh.GetTriangles(sub);
                var result = triangles[sub] = new List<int>(original.Length * 4);
                for (int i = 0; i < original.Length; i += 3)
                {
                    int a = original[i], b = original[i + 1], c = original[i + 2];
                    int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                    result.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
            }
            mesh.SetVertices(vertices);
            if (hasNormals) mesh.SetNormals(normals);
            if (hasUV) mesh.SetUVs(0, uv);
            for (int sub = 0; sub < triangles.Length; sub++) mesh.SetTriangles(triangles[sub], sub);
        }
        mesh.RecalculateBounds();
        return mesh;
    }
}
