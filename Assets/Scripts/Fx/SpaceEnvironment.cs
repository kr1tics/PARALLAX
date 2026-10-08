using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Lightweight orbital backdrop using built-in unlit materials and generated textures.</summary>
public sealed class SpaceEnvironment : MonoBehaviour
{
    [SerializeField] private Vector3 earthDirection = new Vector3(1f, 0.30f, -0.20f);
    [SerializeField] private float earthDistance = 450f;
    [SerializeField] private float earthRadius = 65f;
    [SerializeField] private int starCount = 1800;

    private Transform backdrop, earth, atmosphere;
    private Renderer earthRenderer, atmosphereRenderer;
    private Material livingEarth, deadEarth, livingAtmosphere, deadAtmosphere;
    private Camera view;
    private TimeState? applied;
    private readonly List<Object> generatedAssets = new List<Object>();
    private CameraClearFlags previousClearFlags;
    private Color previousBackground;

    // Approximate longitude/latitude silhouettes. Both epochs use exactly the same geography.
    private static readonly Vector2[][] Continents = {
        Polygon(-168,72,-140,70,-128,58,-125,49,-123,40,-116,30,-106,24,-97,19,-87,16,-83,10,-78,8,-80,24,-81,30,-75,35,-66,45,-56,51,-62,60,-80,66,-100,75,-135,74),
        Polygon(-81,12,-70,12,-60,7,-50,0,-35,-6,-40,-20,-48,-29,-55,-35,-64,-55,-72,-51,-75,-35,-80,-15,-78,-1),
        Polygon(-17,36,-5,36,10,37,25,33,33,31,35,22,44,12,51,11,43,1,40,-14,34,-26,20,-35,12,-27,10,-5,-2,5,-15,10,-17,22),
        Polygon(-10,36,-9,44,-1,49,8,54,5,59,18,71,33,70,43,62,65,70,90,77,130,71,170,65,179,54,156,50,140,40,130,35,121,22,108,20,106,9,119,-5,105,-8,98,5,90,22,80,8,73,20,66,25,57,25,50,12,43,13,35,30,28,41,15,39,8,44,1,42),
        Polygon(113,-22,121,-15,132,-12,141,-11,145,-19,153,-26,150,-37,137,-39,129,-33,116,-35,112,-26),
        Polygon(-53,59,-43,61,-20,72,-25,83,-45,84,-61,77),
        Polygon(47,-13,50,-17,49,-25,44,-24,43,-18),
        Polygon(130,31,136,35,142,43,145,44,142,36,135,32),
        Polygon(166,-34,179,-38,174,-45,166,-47,168,-41),
        Polygon(-180,-74,-130,-72,-90,-70,-65,-64,-50,-73,0,-70,50,-68,100,-66,150,-72,180,-74,180,-90,-180,-90)
    };

    private void Awake()
    {
        view = Camera.main;
        if (view == null) { enabled = false; return; }
        previousClearFlags = view.clearFlags;
        previousBackground = view.backgroundColor;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(0.001f, 0.002f, 0.006f);

        backdrop = new GameObject("Orbital backdrop").transform;
        backdrop.SetParent(transform, false);
        CreateStars();
        CreateEarthTextures(out Texture2D live, out Texture2D dead);
        livingEarth = MaterialFor("Living Earth", live);
        deadEarth = MaterialFor("Barren Earth", dead);
        earth = MeshObject("Distant Earth", Sphere(96, 48), livingEarth);
        earth.localPosition = earthDirection.normalized * earthDistance;
        earth.localScale = Vector3.one * earthRadius;
        earth.localRotation = Quaternion.LookRotation(-earthDirection.normalized, Vector3.up)
            * Quaternion.Euler(0f, 15f, -18f);
        earthRenderer = earth.GetComponent<Renderer>();

        livingAtmosphere = MaterialFor("Blue atmosphere", AtmosphereTexture(new Color(0.15f, 0.57f, 1f), 0.8f), true);
        deadAtmosphere = MaterialFor("Thin dust atmosphere", AtmosphereTexture(new Color(0.55f, 0.49f, 0.40f), 0.16f), true);
        atmosphere = MeshObject("Earth atmosphere", Quad(), livingAtmosphere);
        atmosphere.localPosition = earth.localPosition + earthDirection.normalized * earthRadius * 0.15f;
        atmosphere.localScale = Vector3.one * earthRadius * 2.5f;
        atmosphere.localRotation = Quaternion.LookRotation(earthDirection.normalized, Vector3.up);
        atmosphereRenderer = atmosphere.GetComponent<Renderer>();
        livingAtmosphere.renderQueue = deadAtmosphere.renderQueue = 2990;
        ApplyTimeState();
    }

    private void LateUpdate()
    {
        if (view == null) return;
        // These objects are distant scenery, so station movement does not make them drift or approach.
        backdrop.position = view.transform.position;
        ApplyTimeState();
    }

    private void ApplyTimeState()
    {
        TimeState now = GlobalState.get().now;
        if (applied == now) return;
        bool alive = now == TimeState.Past;
        earthRenderer.sharedMaterial = alive ? livingEarth : deadEarth;
        atmosphereRenderer.sharedMaterial = alive ? livingAtmosphere : deadAtmosphere;
        applied = now;
    }

    private void CreateStars()
    {
        const int atlasSize = 96;
        var texture = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
        var pixels = new Color[atlasSize * atlasSize];
        Color[] hues = {new Color(0.68f, 0.81f, 1f), new Color(1f, 0.95f, 0.86f), Color.white};
        for (int y = 0; y < atlasSize; y++)
        for (int x = 0; x < atlasSize; x++)
        {
            float r = new Vector2((x % 32 + 0.5f) / 16f - 1f, (y % 32 + 0.5f) / 16f - 1f).magnitude;
            Color color = hues[x / 32];
            color.a = Mathf.Exp(-r * r * 8f) * (0.5f + 0.25f * (y / 32));
            pixels[y * atlasSize + x] = color;
        }
        texture.SetPixels(pixels); texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = "Generated star atlas"; generatedAssets.Add(texture);
        var vertices = new Vector3[starCount * 4];
        var uv = new Vector2[vertices.Length];
        var indices = new int[starCount * 6];
        var random = new System.Random(150);
        for (int i = 0; i < starCount; i++)
        {
            float z = (float)random.NextDouble() * 2f - 1f;
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float ring = Mathf.Sqrt(1f - z * z);
            Vector3 direction = new Vector3(ring * Mathf.Cos(angle), z, ring * Mathf.Sin(angle));
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, direction);
            float size = Mathf.Lerp(0.8f, 3.5f, Mathf.Pow((float)random.NextDouble(), 2f));
            Vector3 center = direction * 750f;
            int v = i * 4;
            vertices[v] = center - right * size - up * size;
            vertices[v+1] = center + right * size - up * size;
            vertices[v+2] = center + right * size + up * size;
            vertices[v+3] = center - right * size + up * size;
            float u = random.Next(3) / 3f, t = random.Next(3) / 3f;
            uv[v] = new Vector2(u, t); uv[v+1] = new Vector2(u+1f/3f, t);
            uv[v+2] = new Vector2(u+1f/3f, t+1f/3f); uv[v+3] = new Vector2(u, t+1f/3f);
            int j = i * 6;
            indices[j] = v; indices[j+1] = v+1; indices[j+2] = v+2;
            indices[j+3] = v; indices[j+4] = v+2; indices[j+5] = v+3;
        }
        var mesh = new Mesh {name = "Generated starfield", vertices = vertices, uv = uv, triangles = indices};
        mesh.RecalculateBounds(); generatedAssets.Add(mesh);
        MeshObject("Stars", mesh, MaterialFor("Starlight", texture, true));
    }

    private void CreateEarthTextures(out Texture2D live, out Texture2D dead)
    {
        const int width = 512, height = 256;
        var livePixels = new Color[width * height];
        var deadPixels = new Color[livePixels.Length];
        Vector3 sun = new Vector3(-0.55f, 0.4f, 0.8f).normalized;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            float lon = u * 360f - 180f, lat = v * 180f - 90f;
            float noise = Mathf.PerlinNoise(u * 35f + 9f, v * 25f + 4f);
            var point = new Vector2(lon + (noise - 0.5f) * 2f, lat + (noise - 0.5f) * 1.5f);
            bool land = false;
            foreach (Vector2[] polygon in Continents)
                if (Inside(point, polygon)) { land = true; break; }
            float latitude = lat * Mathf.Deg2Rad, longitude = lon * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(Mathf.Cos(latitude) * Mathf.Sin(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Cos(longitude));
            float sunlight = Mathf.Clamp01(Vector3.Dot(normal, sun) * 2.2f + 0.08f);
            float illumination = Mathf.Lerp(0.018f, 1f, sunlight);
            Color ocean = Color.Lerp(new Color(0.018f, 0.085f, 0.24f), new Color(0.045f, 0.27f, 0.47f), noise);
            Color vegetation = Color.Lerp(new Color(0.06f, 0.18f, 0.08f), new Color(0.31f, 0.43f, 0.18f), noise);
            bool desert = lat > 12f && lat < 33f && lon > -18f && lon < 70f;
            Color liveColor = land ? (desert ? Color.Lerp(new Color(0.47f, 0.37f, 0.20f), new Color(0.72f, 0.62f, 0.39f), noise) : vegetation) : ocean;
            if (Mathf.Abs(lat) > 70f) liveColor = Color.Lerp(liveColor, new Color(0.81f, 0.88f, 0.91f), Mathf.InverseLerp(70f, 85f, Mathf.Abs(lat)));
            float weather = Mathf.PerlinNoise(u * 13f + Mathf.Sin(v * 24f) * 0.55f, v * 10f + 37f);
            float detail = Mathf.PerlinNoise(u * 67f, v * 53f);
            float clouds = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.51f, 0.72f, weather + (detail - 0.5f) * 0.17f));
            liveColor = Color.Lerp(liveColor, new Color(0.92f, 0.94f, 0.94f), clouds * 0.86f);
            liveColor *= illumination; liveColor.a = 1f;
            Color deadColor = land ? Color.Lerp(new Color(0.25f, 0.22f, 0.18f), new Color(0.48f, 0.42f, 0.34f), noise)
                : Color.Lerp(new Color(0.12f, 0.13f, 0.14f), new Color(0.27f, 0.26f, 0.24f), noise);
            deadColor *= illumination; deadColor.a = 1f;
            livePixels[y * width + x] = liveColor; deadPixels[y * width + x] = deadColor;
        }
        live = Texture("Living Earth surface", width, height, livePixels);
        dead = Texture("Dead Earth surface", width, height, deadPixels);
    }

    private Texture2D AtmosphereTexture(Color color, float strength)
    {
        const int size = 128;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
            float rim = Mathf.Exp(-Mathf.Pow((r - 0.80f) / 0.037f, 2f));
            float glow = Mathf.Exp(-Mathf.Pow((r - 0.81f) / 0.11f, 2f)) * 0.18f;
            color.a = (rim + glow) * strength;
            pixels[y * size + x] = color;
        }
        var texture = Texture("Atmospheric rim", size, size, pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private Texture2D Texture(string name, int width, int height, Color[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) {name = name};
        texture.SetPixels(pixels); texture.Apply();
        texture.wrapModeU = TextureWrapMode.Repeat;
        texture.wrapModeV = TextureWrapMode.Clamp;
        generatedAssets.Add(texture);
        return texture;
    }

    private Material MaterialFor(string name, Texture2D texture, bool transparent = false)
    {
        // Resources materials keep built-in shaders included in WebGL builds.
        var material = new Material(Resources.Load<Material>(transparent ? "Space/Transparent" : "Space/Opaque"))
            {name = name, mainTexture = texture};
        generatedAssets.Add(material);
        return material;
    }

    private Transform MeshObject(string name, Mesh mesh, Material material)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(backdrop, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return obj.transform;
    }

    private Mesh Sphere(int longitudeSegments, int latitudeSegments)
    {
        int stride = longitudeSegments + 1;
        var vertices = new Vector3[stride * (latitudeSegments + 1)];
        var uv = new Vector2[vertices.Length];
        var indices = new int[longitudeSegments * latitudeSegments * 6];
        for (int y = 0; y <= latitudeSegments; y++)
        for (int x = 0; x <= longitudeSegments; x++)
        {
            float u = (float)x / longitudeSegments, v = (float)y / latitudeSegments;
            float lat = (v - 0.5f) * Mathf.PI, lon = (u - 0.5f) * Mathf.PI * 2f;
            int i = y * stride + x;
            vertices[i] = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
            uv[i] = new Vector2(u, v);
            if (y == latitudeSegments || x == longitudeSegments) continue;
            int t = (y * longitudeSegments + x) * 6;
            indices[t] = i; indices[t+1] = i+1; indices[t+2] = i+stride;
            indices[t+3] = i+1; indices[t+4] = i+stride+1; indices[t+5] = i+stride;
        }
        var mesh = new Mesh {name = "Earth globe", vertices = vertices, uv = uv, triangles = indices};
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); generatedAssets.Add(mesh);
        return mesh;
    }

    private Mesh Quad()
    {
        var mesh = new Mesh {name = "Atmosphere billboard",
            vertices = new[] {new Vector3(-0.5f,-0.5f,0),new Vector3(0.5f,-0.5f,0),new Vector3(0.5f,0.5f,0),new Vector3(-0.5f,0.5f,0)},
            uv = new[] {Vector2.zero, Vector2.right, Vector2.one, Vector2.up},
            triangles = new[] {0,2,1,0,3,2}};
        mesh.RecalculateBounds(); generatedAssets.Add(mesh); return mesh;
    }

    private static Vector2[] Polygon(params float[] coordinates)
    {
        var points = new Vector2[coordinates.Length / 2];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector2(coordinates[i*2], coordinates[i*2+1]);
        return points;
    }

    private static bool Inside(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length-1; i < polygon.Length; j = i++)
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x-polygon[i].x) * (point.y-polygon[i].y) / (polygon[j].y-polygon[i].y) + polygon[i].x)
                inside = !inside;
        return inside;
    }

    private void OnDestroy()
    {
        if (view != null) { view.clearFlags = previousClearFlags; view.backgroundColor = previousBackground; }
        foreach (Object asset in generatedAssets) if (asset != null) Destroy(asset);
    }
}
