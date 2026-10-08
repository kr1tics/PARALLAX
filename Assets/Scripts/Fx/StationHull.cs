using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Gives the inward-facing modular kit solid exterior faces without changing its materials.</summary>
[DisallowMultipleComponent]
public sealed class StationHull : MonoBehaviour
{
    private readonly Dictionary<Mesh, Mesh> hullMeshes = new Dictionary<Mesh, Mesh>();
    private readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
    private readonly Dictionary<MeshCollider, Mesh> originalCollisionMeshes = new Dictionary<MeshCollider, Mesh>();
    private readonly List<MeshCollider> addedColliders = new List<MeshCollider>();

    private void Awake()
    {
        foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh source = filter.sharedMesh;
            if (source == null || filter.GetComponent<MeshRenderer>() == null) continue;
            if (!source.isReadable)
            {
                Debug.LogError("Station hull needs a readable mesh: " + source.name, filter);
                continue;
            }
            if (!hullMeshes.TryGetValue(source, out Mesh hull))
            {
                hull = WithExteriorFaces(source);
                hullMeshes.Add(source, hull);
            }
            originalMeshes.Add(filter, source);
            filter.sharedMesh = hull;
            var existingCollider = filter.GetComponent<MeshCollider>();
            if (existingCollider != null)
            {
                originalCollisionMeshes.Add(existingCollider, existingCollider.sharedMesh);
                existingCollider.sharedMesh = hull;
            }
            // Some kit roofs and curved walls have no collision in the supplied prefabs.
            if (filter.GetComponent<Collider>() == null)
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = hull;
                addedColliders.Add(collider);
            }
        }
    }

    private static Mesh WithExteriorFaces(Mesh source)
    {
        int count = source.vertexCount;
        var mesh = new Mesh { name = source.name + " with exterior faces",
            indexFormat = count * 2 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        var vertices = new Vector3[count * 2];
        source.vertices.CopyTo(vertices, 0);
        source.vertices.CopyTo(vertices, count);
        mesh.vertices = vertices;
        var normals = source.normals;
        if (normals.Length == count)
        {
            var both = new Vector3[count * 2];
            for (int i = 0; i < count; i++) { both[i] = normals[i]; both[i + count] = -normals[i]; }
            mesh.normals = both;
        }
        var tangents = source.tangents;
        if (tangents.Length == count)
        {
            var both = new Vector4[count * 2];
            for (int i = 0; i < count; i++)
            {
                both[i] = tangents[i];
                both[i + count] = new Vector4(tangents[i].x, tangents[i].y, tangents[i].z, -tangents[i].w);
            }
            mesh.tangents = both;
        }
        for (int channel = 0; channel < 8; channel++)
        {
            var coordinates = new List<Vector4>();
            source.GetUVs(channel, coordinates);
            if (coordinates.Count == count)
            {
                coordinates.AddRange(coordinates.ToArray());
                mesh.SetUVs(channel, coordinates);
            }
        }
        var colors = source.colors;
        if (colors.Length == count)
        {
            var both = new Color[count * 2];
            colors.CopyTo(both, 0); colors.CopyTo(both, count); mesh.colors = both;
        }
        mesh.subMeshCount = source.subMeshCount;
        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            int[] front = source.GetTriangles(submesh);
            var both = new int[front.Length * 2];
            front.CopyTo(both, 0);
            for (int i = 0; i < front.Length; i += 3)
            {
                both[front.Length + i] = front[i] + count;
                both[front.Length + i + 1] = front[i + 2] + count;
                both[front.Length + i + 2] = front[i + 1] + count;
            }
            mesh.SetTriangles(both, submesh);
        }
        if (normals.Length != count) mesh.RecalculateNormals();
        mesh.bounds = source.bounds;
        return mesh;
    }

    private void OnDestroy()
    {
        foreach (var entry in originalMeshes) if (entry.Key != null) entry.Key.sharedMesh = entry.Value;
        foreach (var entry in originalCollisionMeshes) if (entry.Key != null) entry.Key.sharedMesh = entry.Value;
        foreach (var collider in addedColliders) if (collider != null) Destroy(collider);
        foreach (var mesh in hullMeshes.Values) Destroy(mesh);
    }
}
