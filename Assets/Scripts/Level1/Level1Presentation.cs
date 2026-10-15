using System.Collections.Generic;
using Parallax.Settings;
using TMPro;
using UnityEngine;

/// <summary>Scene-only observer. Never changes physics, puzzle state, or time travel.</summary>
[DefaultExecutionOrder(300)]
public sealed class Level1Presentation : MonoBehaviour
{
    [Header("Existing scene references")]
    [SerializeField] Level1Controller level;
    [SerializeField] Interact interaction;
    [SerializeField] PressureButton accessPlate, engineeringPlate;
    [SerializeField] Rigidbody[] accessCubes, engineeringCubes;
    [SerializeField] Light keyLight;
    [SerializeField] Renderer[] decaySurfaces;
    [Header("Typography (Intro is independent)")]
    [SerializeField] TMP_FontAsset titleFont, supportingFont;
    [SerializeField] float titleSize = 23, instructionSize = 17;
    [Header("Industrial finishes")]
    [SerializeField] Material casing, plateFinish, inactiveDetail, activeDetail;
    [SerializeField] Color pastLight = new Color(.94f, .94f, .91f);
    [SerializeField] Color futureLight = new Color(.72f, .77f, .79f);
    [SerializeField, Range(.1f, 2f)] float pastIntensity = .85f, futureIntensity = .55f;
    [SerializeField, Range(0, 1)] float futureDecay = .85f;
    [Header("Optional audio — unassigned clips stay silent")]
    [SerializeField] AudioClip pickup, placement, plateActivation, timelineSwitch, relayRestoration, doorUnlock;
    [SerializeField, Range(0, 1)] float interactionVolume = .25f, systemVolume = .2f;
    [Header("Holographic placement cues")]
    [SerializeField] Material hologramAmber;
    readonly List<Renderer>[] hologramEdges = { new(), new() };
    readonly List<Mesh> hologramMeshes = new();
    readonly float[] hologramOpacity = { 1f, 1f };
    GameplayHud hud;
    TMP_Text instruction, status, prompt;
    AudioSource audioSource;
    MovableObject previousHeld;
    TimeState previousTime;
    bool previousAccess, previousEngineering, previousIntroSolved, previousMainSolved;
    readonly List<Renderer> accessDetails = new(), engineeringDetails = new();
    readonly Dictionary<MovableObject, List<Renderer>> cubeDetails = new();
    readonly Dictionary<Renderer, bool> detailStates = new();
    MaterialPropertyBlock properties;
    readonly List<Light> localLights = new();
    public string CurrentInstruction => instruction == null ? "" : instruction.text;
    public string CurrentPrompt => prompt == null ? "" : prompt.text;

    void Start()
    {
        properties = new MaterialPropertyBlock();
        BuildHud();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
        DecorateCubes(accessCubes); DecorateCubes(engineeringCubes);
        DecoratePlate(accessPlate, accessDetails); DecoratePlate(engineeringPlate, engineeringDetails);
        BuildHolograms();
        // Local pools of light reveal the placement zones without flooding the room.
        AddLight(accessPlate.transform.position + Vector3.up * 3.8f);
        AddLight(engineeringPlate.transform.position + Vector3.up * 3.8f);
        previousTime = GlobalState.get().now;
        previousHeld = interaction.HeldObject;
        previousAccess = accessPlate.IsPressed; previousEngineering = engineeringPlate.IsPressed;
        previousIntroSolved = level.IntroSolved; previousMainSolved = level.MainSolved;
        ApplyTimeline();
    }

    void LateUpdate()
    {
        UpdateHolograms();
        if (hud == null) return;
        bool blocked = GameSettingsMenu.GameplayInputBlocked;
        hud.SetVisible(!blocked);
        if (blocked) return;
        var state = GlobalState.get();
        if (state.now != previousTime)
        {
            previousTime = state.now; ApplyTimeline(); Play(timelineSwitch, systemVolume);
        }
        var held = interaction.HeldObject;
        if (held != previousHeld) { Play(held != null ? pickup : placement, interactionVolume); previousHeld = held; }
        bool access = accessPlate.IsPressed, engineering = engineeringPlate.IsPressed;
        if ((access && !previousAccess) || (engineering && !previousEngineering)) Play(plateActivation, systemVolume);
        previousAccess = access; previousEngineering = engineering;
        if ((level.IntroSolved && !previousIntroSolved) || (level.MainSolved && !previousMainSolved))
        { Play(relayRestoration, systemVolume); Play(doorUnlock, systemVolume); }
        previousIntroSolved = level.IntroSolved; previousMainSolved = level.MainSolved;
        UpdateObjective();
        var cast = interaction.fireInteractRay(true);
        Interactable target = cast.Item1 ? cast.Item2.collider.GetComponentInParent<Interactable>() : null;
        // Reuse the exact occlusion-aware interaction ray, rather than a proximity-only prompt.
        bool actionableCube = target is MovableObject movable && !movable.GetComponent<Rigidbody>().isKinematic;
        prompt.text = held != null ? "E — Place" : actionableCube ? "E — Pick up" : target is Switch ?
            (state.now == TimeState.Past ? "E — Toggle gravity" : "F — Switch Timeline") :
            instruction.text.StartsWith("Switch") || instruction.text == "Return to the Future." ||
            (state.now == TimeState.Future && instruction.text.StartsWith("Return to")) ? "F — Switch Timeline" : "";
        foreach (var item in cubeDetails) SetDetail(item.Value, item.Key == held || (actionableCube && item.Key == target));
        SetDetail(accessDetails, access); SetDetail(engineeringDetails, engineering);
        status.text = (state.now == TimeState.Past ? "2026 / PAST" : "2176 / FUTURE") +
            (state.gravity < .1f ? "  ·  GRAVITY OFF" : "  ·  GRAVITY ON");
    }

    void UpdateObjective()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        bool gravityOn = GlobalState.get().gravity > 4.5f;
        if (level.Completed) { SetObjective("COMPLETE", "Access restored", "Tutorial complete. R — Replay"); return; }
        if (!level.IntroSolved)
        {
            bool placed = interaction.HeldObject == null && CubeOnPlate(accessCubes[0], accessPlate);
            string next = !past ? "Switch to the Past." : placed ?
                (gravityOn ? "Switch to the Future." : "Restore station gravity.") :
                interaction.HeldObject == accessCubes[0].GetComponent<MovableObject>() ?
                "Place the cube on the relay plate." : "Locate the power cube.";
            SetObjective("OBJECTIVE 01", "Restore the Access Relay", next); return;
        }
        if (!level.StorageAccessGranted)
        { SetObjective("OBJECTIVE 02", "Release the Engineering Cube", past ? "Turn gravity OFF at the console." : "Switch to the Past."); return; }
        if (!level.MainSolved)
        {
            bool placed = interaction.HeldObject == null && CubeOnPlate(engineeringCubes[past ? 0 : 1], engineeringPlate);
            string next = !placed ? (interaction.HeldObject == engineeringCubes[0].GetComponent<MovableObject>() ? "Place the cube on relay plate 02." :
                level.StorageReleased ? "Take the cube out of storage." : "Return to the Past; turn gravity OFF.") :
                !gravityOn ? "Return to the Past console.\nTurn station gravity ON." : past ? "Switch to the Future." : "Wait for the relay to engage.";
            SetObjective("OBJECTIVE 02", "Restore the Engineering Relay", next); return;
        }
        SetObjective("OBJECTIVE 03", "Access restored", past ? "Return to the Future." : "Proceed through the west exit.");
    }

    public static bool CubeOnPlate(Rigidbody cube, PressureButton plate)
    {
        if (cube == null || plate == null) return false;
        Bounds b = plate.GetComponent<Collider>().bounds;
        Vector3 p = cube.worldCenterOfMass;
        // Observation only. Actual activation still belongs to PressureButton's contact/gravity checks.
        return Mathf.Abs(p.x - b.center.x) < b.extents.x && Mathf.Abs(p.z - b.center.z) < b.extents.z &&
            p.y > b.max.y && p.y < b.max.y + 1.25f;
    }

    void BuildHolograms()
    {
        if (hologramAmber == null) return;
        var plates = new[] { accessPlate, engineeringPlate };
        var cubes = new[] { accessCubes[0], engineeringCubes[0] };
        for (int i = 0; i < plates.Length; i++)
        {
            var zone = new GameObject("Relay target hologram " + (i + 1)).transform;
            zone.SetParent(transform, false);
            Bounds plate = plates[i].GetComponent<Collider>().bounds;
            // Collider world bounds are empty for the inactive Past cube at Future startup.
            var cubeBox = cubes[i].GetComponent<BoxCollider>();
            Vector3 cubeScale = cubes[i].transform.lossyScale;
            Vector3 size = Vector3.Scale(cubeBox.size, new Vector3(Mathf.Abs(cubeScale.x), Mathf.Abs(cubeScale.y), Mathf.Abs(cubeScale.z)));
            zone.position = new Vector3(plate.center.x, plate.max.y + size.y * .5f + .015f, plate.center.z);
            var corners = new[] {
                new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f),
                new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f),
                new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            int[][] faces = { new[]{0,3,2,1}, new[]{4,5,6,7}, new[]{0,4,7,3},
                new[]{1,2,6,5}, new[]{3,7,6,2}, new[]{0,1,5,4} };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            foreach (var face in faces)
            {
                int start = vertices.Count;
                foreach (int c in face)
                {
                    Vector3 v = corners[c]; vertices.Add(Vector3.Scale(v, size));
                    // Side faces share height-based scans; horizontal faces scan along depth.
                    uv.Add(new Vector2(v.x + .5f, Mathf.Abs(corners[face[0]].y - corners[face[2]].y) > .1f ? v.y + .5f : v.z + .5f));
                }
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            hologramEdges[i].Add(HologramSurface(zone, "Hologram surface", vertices.ToArray(), uv.ToArray(), triangles.ToArray()));
            const float thickness = .008f;
            for (int axis = 0; axis < 3; axis++)
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                Vector3 position = Vector3.zero, scale = Vector3.one * thickness;
                position[u] = a * size[u] * .5f; position[v] = b * size[v] * .5f;
                scale[axis] = size[axis];
                hologramEdges[i].Add(HologramBar(zone, position, scale));
            }
            // Two local approach chevrons supplement the existing green route.
            var marker = new GameObject("Relay approach hologram " + (i + 1)).transform;
            marker.SetParent(transform, false);
            marker.position = new Vector3(plate.center.x + (i == 0 ? -1.35f : 1.35f), -.945f, plate.center.z);
            marker.rotation = Quaternion.Euler(0, i == 0 ? 90 : -90, 0);
            var arrow = new[] {
                new Vector3(-.14f, .025f, -.18f), new Vector3(0, .025f, .06f),
                new Vector3(.14f, .025f, -.18f) };
            var arrowUV = new[] { new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(1, 0) };
            hologramEdges[i].Add(HologramSurface(marker, "Hologram direction", arrow, arrowUV, new[]{0,1,2}));
        }
    }

    Renderer HologramSurface(Transform parent, string name, Vector3[] vertices, Vector2[] uv, int[] triangles)
    {
        var mesh = new Mesh { name = name }; mesh.vertices = vertices; mesh.uv = uv;
        hologramMeshes.Add(mesh);
        mesh.triangles = triangles; mesh.RecalculateBounds(); mesh.RecalculateNormals();
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = hologramAmber;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    void OnDestroy()
    {
        foreach (var mesh in hologramMeshes) if (mesh != null) Destroy(mesh);
    }

    Renderer HologramBar(Transform parent, Vector3 position, Vector3 scale)
    {
        // Reuse built-in cube geometry without adding physics components.
        var go = new GameObject("Hologram edge", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = accessPlate.GetComponent<MeshFilter>().sharedMesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = hologramAmber;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false; return renderer;
    }

    void UpdateHolograms()
    {
        if (hologramAmber == null || properties == null) return;
        bool past = GlobalState.get().now == TimeState.Past;
        var cubes = new[] { accessCubes[past ? 0 : 1], engineeringCubes[past ? 0 : 1] };
        var plates = new[] { accessPlate, engineeringPlate };
        var solved = new[] { level.IntroSolved, level.MainSolved };
        for (int i = 0; i < 2; i++)
        {
            bool placed = interaction.HeldObject != cubes[i].GetComponent<MovableObject>() && CubeOnPlate(cubes[i], plates[i]);
            float target = solved[i] || placed ? 0 : 1;
            hologramOpacity[i] = Mathf.MoveTowards(hologramOpacity[i], target, Time.deltaTime / .6f);
            foreach (var edge in hologramEdges[i])
            {
                edge.enabled = hologramOpacity[i] > .001f;
                properties.Clear(); properties.SetFloat("_Alpha", hologramAmber.GetFloat("_Alpha") * hologramOpacity[i] * (edge.name == "Hologram edge" ? 1.15f : edge.name == "Hologram direction" ? .75f : 1f));
                if (edge.name == "Hologram direction") properties.SetFloat("_ScanDensity", 4f);
                edge.SetPropertyBlock(properties);
            }
        }
    }

    void SetObjective(string id, string title, string detail)
    { hud.SetObjective(id, title, detail); }
    void Play(AudioClip clip, float volume) { if (clip != null) audioSource.PlayOneShot(clip, volume); }

    void ApplyTimeline()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        if (keyLight != null) { keyLight.color = past ? pastLight : futureLight; keyLight.intensity = past ? pastIntensity : futureIntensity; }
        foreach (var light in localLights) { light.color = past ? pastLight : new Color(.76f, .66f, .49f); light.intensity = past ? .9f : .55f; }
        // Preserve the existing shader and stable weather seed; only override artistic parameters.
        foreach (var surface in decaySurfaces)
        {
            if (surface == null) continue;
            properties.Clear(); surface.GetPropertyBlock(properties);
            properties.SetColor("_Color", past ? new Color(.68f, .69f, .68f) : new Color(.62f, .64f, .63f));
            properties.SetFloat("_DecayAmount", futureDecay);
            properties.SetFloat("_GrimeStrength", .7f);
            properties.SetFloat("_FlickerStrength", .025f);
            properties.SetColor("_DecayColor", new Color(.22f, .23f, .22f));
            surface.SetPropertyBlock(properties);
        }
    }

    void DecorateCubes(Rigidbody[] cubes)
    {
        for (int index = 0; index < cubes.Length; index++)
        {
            var cube = cubes[index]; if (cube == null) continue;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (index == 0) renderer.sharedMaterial = casing;
            else
            { // Retain the existing fault shader and its damage cues on the Future cube.
                properties.Clear(); renderer.GetPropertyBlock(properties);
                properties.SetColor("_Color", new Color(.25f, .24f, .22f));
                properties.SetColor("_EmissionColor", new Color(.30f, .16f, .055f));
                properties.SetFloat("_FlickerStrength", .06f); renderer.SetPropertyBlock(properties);
            }
            var details = new List<Renderer>();
            // One narrow inset on each face communicates a manufactured power module.
            for (int face = 0; face < 4; face++)
            {
                var root = new GameObject("Power module inset").transform;
                root.SetParent(cube.transform, false); root.localRotation = Quaternion.Euler(0, face * 90, 0);
                details.Add(Bar(root, new Vector3(0, .15f, -.506f), new Vector3(.32f, .025f, .012f), inactiveDetail));
                Bar(root, new Vector3(0, -.2f, -.506f), new Vector3(.32f, .075f, .012f), plateFinish);
            }
            cubeDetails[cube.GetComponent<MovableObject>()] = details;
        }
    }

    void DecoratePlate(PressureButton plate, List<Renderer> details)
    {
        plate.GetComponent<Renderer>().sharedMaterial = plateFinish;
        // Four inset strips define the placement area. No new colliders or rigidbodies.
        foreach (float sign in new[] { -1f, 1f })
        {
            details.Add(Bar(plate.transform, new Vector3(sign * .43f, .56f, 0), new Vector3(.018f, .025f, .78f), inactiveDetail));
            details.Add(Bar(plate.transform, new Vector3(0, .56f, sign * .43f), new Vector3(.78f, .025f, .018f), inactiveDetail));
        }
    }
    static Renderer Bar(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Industrial inset (visual only)";
        var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return renderer;
    }
    void SetDetail(List<Renderer> details, bool active)
    {
        foreach (var detail in details)
        {
            if (detailStates.TryGetValue(detail, out bool old) && old == active) continue;
            detail.sharedMaterial = active ? activeDetail : inactiveDetail; detailStates[detail] = active;
        }
    }
    void AddLight(Vector3 position)
    {
        var root = new GameObject("Relay task light"); root.transform.SetParent(transform, false); root.transform.position = position;
        var light = root.AddComponent<Light>(); light.type = LightType.Point; light.range = 6; light.shadows = LightShadows.None; localLights.Add(light);
    }

    void BuildHud()
    {
        hud = new GameplayHud(transform, "Scene 01 Objective UI", titleFont, supportingFont, titleSize, instructionSize);
        instruction = hud.Instruction; status = hud.Status; prompt = hud.Prompt;
    }
}
