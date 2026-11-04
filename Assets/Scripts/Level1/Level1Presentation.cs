using System.Collections.Generic;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
    readonly float[] hologramOpacity = { 0f, 0f };
    TutorialObjectiveHud hud;
    Rigidbody playerBody;
    public enum GuidanceStage { AccessRelay, FindConsole, UnlockStorage, FindCube, MoveCube, RestoreGravity, ReturnFuture, Exit, Complete }
    public GuidanceStage Stage { get; private set; }
    public bool PlayerInStorage => InStorage(playerBody.position);
    public Rigidbody CurrentEngineeringCube => engineeringCubes[GlobalState.get().now == TimeState.Past ? 0 : 1];
    public bool EngineeringCubeInStorage => InStorage(CurrentEngineeringCube.position);
    public Transform CueTarget { get; private set; }
    public string CueLabel { get; private set; }
    public Material PlacementHologram => hologramAmber;
    public bool ShowAccessPlacement { get; private set; }
    public bool ShowEngineeringPlacement { get; private set; }
    bool consoleDiscovered, cubeDiscovered, hadStorageAccess;
    Transform consoleTarget, storageTarget, exitTarget;
    readonly List<Renderer> routeGuides = new();
    public string ObjectiveTitle => hud == null ? "" : hud.Title;
    public bool ObjectiveExpanded => hud != null && hud.Expanded;
    public int ObjectiveRevision => hud == null ? 0 : hud.Revision;
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
        playerBody = interaction.GetComponent<Rigidbody>();
        BuildHud();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
        DecorateCubes(accessCubes); DecorateCubes(engineeringCubes);
        DecoratePlate(accessPlate, accessDetails); DecoratePlate(engineeringPlate, engineeringDetails);
        BuildHolograms();
        consoleTarget = transform.Find("EntangledGravSwitch");
        storageTarget = transform.Find("Door - Gravity interlock");
        exitTarget = transform.Find("Level 1 Completion Zone");
        foreach (var item in GetComponentsInChildren<Renderer>(true))
            if (item.name == "Floor route guide") { routeGuides.Add(item); item.enabled = false; }
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
        if (hud == null) return;
        bool blocked = GameSettingsMenu.GameplayInputBlocked;
        hud.SetVisible(!blocked);
        if (blocked) return;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            consoleDiscovered = cubeDiscovered = false;
            hud.Reannounce();
        }
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
        UpdateHolograms();
        for (int i = 0; i < localLights.Count; i++)
            localLights[i].enabled = i == 0 ? ShowAccessPlacement : ShowEngineeringPlacement;
        var cast = interaction.fireInteractRay(true);
        Interactable target = cast.Item1 ? cast.Item2.collider.GetComponentInParent<Interactable>() : null;
        // Reuse the exact occlusion-aware interaction ray, rather than a proximity-only prompt.
        bool actionableCube = target is MovableObject movable && !movable.GetComponent<Rigidbody>().isKinematic;
        string timelinePrompt = state.now == TimeState.Past ? "T — Switch to Future" : "T — Switch to Past";
        prompt.text = held != null ? "E — Place" : actionableCube ? "E — Pick up" : target is Switch ?
            (state.now == TimeState.Past ? "E — Toggle gravity" : timelinePrompt) :
            instruction.text.StartsWith("Switch") || instruction.text.StartsWith("T —") || instruction.text == "Return to the Future." ? timelinePrompt : "";
        foreach (var item in cubeDetails)
        {
            bool taskCube = !level.IntroSolved ? item.Key == accessCubes[state.now == TimeState.Past ? 0 : 1].GetComponent<MovableObject>() :
                (Stage == GuidanceStage.FindCube && PlayerInStorage || Stage == GuidanceStage.MoveCube) &&
                item.Key == CurrentEngineeringCube.GetComponent<MovableObject>();
            SetDetail(item.Value, item.Key == held || (actionableCube && item.Key == target) || taskCube);
        }
        SetDetail(accessDetails, access); SetDetail(engineeringDetails, engineering);
        status.text = (state.now == TimeState.Past ? "2026 / PAST" : "2176 / FUTURE") +
            (state.gravity < .1f ? "  ·  GRAVITY OFF" : "  ·  GRAVITY ON");
        hud.Tick(Time.unscaledDeltaTime);
    }

    static readonly Bounds EngineeringStorage = new Bounds(new Vector3(-6, .9f, 15), new Vector3(4.8f, 4.2f, 6.6f));

    public static bool InStorage(Vector3 position)
    { position.y = EngineeringStorage.center.y; return EngineeringStorage.Contains(position); }

    void UpdateObjective()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        bool gravityOn = GlobalState.get().gravity > 4.5f;
        if (!level.IntroSolved || (hadStorageAccess && !level.StorageAccessGranted))
            consoleDiscovered = cubeDiscovered = false;
        hadStorageAccess = level.StorageAccessGranted;
        CueTarget = null; CueLabel = "";
        ShowAccessPlacement = ShowEngineeringPlacement = false;
        foreach (var route in routeGuides) route.enabled = false;
        if (level.Completed) { Present(GuidanceStage.Complete, "Access restored", "Tutorial complete. R — Replay"); return; }
        if (!level.IntroSolved)
        {
            var cube = accessCubes[past ? 0 : 1];
            bool held = interaction.HeldObject == cube.GetComponent<MovableObject>();
            bool placed = !held && CubeOnPlate(cube, accessPlate);
            string next = !past ? "Switch to the Past." : placed ?
                (gravityOn ? "Switch to the Future." : "Restore station gravity.") : held ?
                "Place the cube on the relay plate." : "Locate the power cube.";
            ShowAccessPlacement = past && held && !placed;
            if (past && !held && !placed) { CueTarget = cube.transform; CueLabel = "POWER CUBE"; }
            Present(GuidanceStage.AccessRelay, "Restore the Access Relay", next); return;
        }
        if (!level.StorageAccessGranted)
        {
            if (consoleTarget != null && Vector3.Distance(playerBody.position, consoleTarget.position) < 3.3f)
                consoleDiscovered = true;
            CueTarget = consoleTarget; CueLabel = "GRAVITY CONSOLE";
            if (!consoleDiscovered)
                Present(GuidanceStage.FindConsole, "Find the Gravity Console", "Storage is locked. Find the gravity console.");
            else Present(GuidanceStage.UnlockStorage, "Unlock the Storage Area", past ?
                "Turn station gravity OFF at the console." : "T — Switch to Past; turn gravity OFF.");
            return;
        }
        if (level.MainSolved)
        {
            CueTarget = exitTarget; CueLabel = "EXIT";
            Present(GuidanceStage.Exit, "Reach the West Exit", past ? "Return to the Future." : "Proceed through the west exit."); return;
        }
        var engineeringCube = CurrentEngineeringCube;
        var movable = engineeringCube.GetComponent<MovableObject>();
        bool carrying = interaction.HeldObject == movable;
        bool onPlate = !carrying && CubeOnPlate(engineeringCube, engineeringPlate);
        var cast = interaction.fireInteractRay(true);
        bool discovered = cast.Item1 && cast.Item2.collider.GetComponentInParent<MovableObject>() == movable;
        cubeDiscovered |= carrying || discovered || !EngineeringCubeInStorage;
        if (carrying)
        {
            ShowEngineeringPlacement = true;
            Present(GuidanceStage.MoveCube, "Power the Engineering Relay", "Move the cube to Relay Station 02.");
        }
        else if (onPlate)
        {
            if (!gravityOn)
            {
                CueTarget = consoleTarget; CueLabel = "GRAVITY CONSOLE";
                Present(GuidanceStage.RestoreGravity, "Restore Station Gravity", past ?
                    "Return to the gravity console.\nTurn station gravity ON." :
                    "T — Return to the Past console.\nTurn station gravity ON.");
            }
            else Present(GuidanceStage.ReturnFuture, "Activate the Engineering Relay", past ? "Switch to the Future." : "Wait for the relay to engage.");
        }
        else if (EngineeringCubeInStorage && !level.StorageReleased)
        {
            CueTarget = consoleTarget; CueLabel = "GRAVITY CONSOLE";
            Present(GuidanceStage.UnlockStorage, "Reopen the Storage Area", past ?
                "Storage locked: find the console.\nTurn station gravity OFF." :
                "Storage locked: T — Switch to Past.\nTurn gravity OFF at the console.");
        }
        else if (cubeDiscovered)
        {
            CueTarget = engineeringCube.transform; CueLabel = "POWER CUBE";
            ShowEngineeringPlacement = true;
            Present(GuidanceStage.MoveCube, "Power the Engineering Relay", EngineeringCubeInStorage ?
                "Retrieve the power cube for Relay Station 02." :
                "Locate the cube outside Storage.\nMove it to Relay Station 02.");
        }
        else
        {
            CueTarget = PlayerInStorage ? engineeringCube.transform : storageTarget;
            CueLabel = PlayerInStorage ? "POWER CUBE" : "STORAGE AREA";
            Present(GuidanceStage.FindCube, "Locate the Power Cube", PlayerInStorage ?
                "Locate the power cube inside Storage." : "Enter Storage and locate the power cube.");
        }
    }

    void Present(GuidanceStage stage, string title, string detail)
    {
        Stage = stage;
        hud.SetObjective(stage.ToString(), title, detail);
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
        renderer.receiveShadows = false; renderer.enabled = false;
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
        renderer.receiveShadows = false; renderer.enabled = false; return renderer;
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
            float target = solved[i] || placed || !(i == 0 ? ShowAccessPlacement : ShowEngineeringPlacement) ? 0 : 1;
            hologramOpacity[i] = Mathf.MoveTowards(hologramOpacity[i], target, Time.deltaTime / .6f);
            foreach (var edge in hologramEdges[i])
            {
                edge.enabled = hologramOpacity[i] > .001f && edge.name != "Hologram direction";
                properties.Clear(); properties.SetFloat("_Alpha", hologramAmber.GetFloat("_Alpha") * hologramOpacity[i] * (edge.name == "Hologram edge" ? .55f : .10f));
                if (edge.name == "Hologram direction") properties.SetFloat("_ScanDensity", 4f);
                edge.SetPropertyBlock(properties);
            }
        }
    }

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
            properties.SetFloat("_FlickerStrength", 0f);
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
        hud = new TutorialObjectiveHud(transform, titleFont, supportingFont);
        instruction = hud.Instruction; status = hud.Status; prompt = hud.Prompt;
    }
}

sealed class TutorialObjectiveHud
{
    readonly RectTransform root, expandedRect, compactRect;
    readonly CanvasGroup visibility, expanded, compact;
    readonly TMP_Text heading, number, compactText;
    readonly RectTransform promptPanel, promptKeycap;
    readonly Image accent;
    string stage;
    float age;
    readonly float expandedSeconds;
    readonly bool compactUsesTitle, compactUsesThought;
    public TMP_Text Instruction { get; }
    public TMP_Text Status { get; }
    public TMP_Text Prompt { get; }
    public string Title => heading.text;
    public bool Expanded => expanded.alpha > .5f;
    public int Revision { get; private set; }
    static readonly Color Cyan = new Color(.43f, .79f, .88f);

    public TutorialObjectiveHud(Transform parent, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, string rootName = "Scene 01 Objective UI", float expandedDuration = 3.6f, bool compactTitle = false, bool compactThought = false, Color? theme = null)
    {
        expandedSeconds = expandedDuration; compactUsesTitle = compactTitle; compactUsesThought = compactThought;
        var go = new GameObject(rootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        go.transform.SetParent(parent, false); root = go.GetComponent<RectTransform>();
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        if (compactTitle) go.AddComponent<PuzzleSettingsPlacement>();
        visibility = go.GetComponent<CanvasGroup>(); visibility.blocksRaycasts = false; visibility.interactable = false;
        expandedRect = Panel(root, "Expanded objective card", new Vector2(780, 158), new Color(.025f, .055f, .075f, .92f));
        expanded = expandedRect.gameObject.AddComponent<CanvasGroup>(); expanded.alpha = 0;
        number = Text(expandedRect, "Objective number", bodyFont, 15, -14, 24); number.color = Cyan;
        heading = Text(expandedRect, "Objective title", titleFont, 34, -43, 43);
        Instruction = Text(expandedRect, "Current instruction", bodyFont, 20, -94, 54);
        Instruction.color = new Color(.72f, .80f, .84f);
        accent = Strip(expandedRect, "Cyan objective accent", new Vector2(0, 0), new Vector2(1, 0), 2);
        compactRect = Panel(root, "Compact objective bar", new Vector2(780, 58), new Color(.025f, .055f, .075f, .86f));
        compact = compactRect.gameObject.AddComponent<CanvasGroup>(); compact.alpha = 0;
        compactText = Text(compactRect, "Compact objective", compactThought ? bodyFont : titleFont, compactThought ? 17 : 23, -10, compactThought ? 70 : 38);
        var compactAccent = Strip(compactRect, "Compact cyan accent", new Vector2(0, 0), new Vector2(0, 1), 2);
        if (theme.HasValue)
        {
            number.color = accent.color = compactAccent.color = theme.Value;
            expandedRect.GetComponent<Image>().color = new Color32(16, 15, 32, 235);
            compactRect.GetComponent<Image>().color = new Color32(16, 15, 32, 220);
        }
        Status = Text(root, "Timeline status", bodyFont, 17, -22, 32);
        Status.rectTransform.anchorMin = Status.rectTransform.anchorMax = Status.rectTransform.pivot = Vector2.one;
        Status.rectTransform.anchoredPosition = new Vector2(-28, -22); Status.rectTransform.sizeDelta = new Vector2(600, 32);
        Status.alignment = TextAlignmentOptions.TopRight;
        promptPanel = Panel(root, "Interaction prompt backing", new Vector2(240, 50), new Color(.055f, .045f, .03f, .58f));
        promptPanel.anchorMin = promptPanel.anchorMax = promptPanel.pivot = new Vector2(.5f, .5f);
        promptPanel.anchoredPosition = Vector2.zero;
        promptPanel.anchorMin = promptPanel.anchorMax = new Vector2(.5f, .20f);
        promptKeycap = Panel(promptPanel, "Interaction keycap", new Vector2(38, 38), new Color(.31f, .23f, .12f, .92f));
        promptKeycap.anchorMin = promptKeycap.anchorMax = new Vector2(0, .5f);
        promptKeycap.pivot = new Vector2(0, .5f); promptKeycap.anchoredPosition = new Vector2(8, 0);
        var outline = promptKeycap.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.80f, .62f, .33f, .65f); outline.effectDistance = new Vector2(1, -1);
        Prompt = Text(promptPanel, "Contextual interaction prompt", bodyFont, 22, 0, 38);
        Prompt.alignment = TextAlignmentOptions.MidlineLeft;
        Prompt.color = new Color(.98f, .91f, .77f);
        Prompt.rectTransform.anchorMin = new Vector2(0, .5f); Prompt.rectTransform.anchorMax = new Vector2(1, .5f);
        Prompt.rectTransform.pivot = new Vector2(.5f, .5f); Prompt.rectTransform.sizeDelta = new Vector2(-40, 38);
        var shadow = Prompt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(1, -1);
    }

    public void SetObjective(string key, string title, string detail, string compactDetail = null)
    {
        if (stage != key)
        {
            stage = key; age = 0; Revision++;
        }
        heading.text = title; Instruction.text = detail;
        number.text = key == "Complete" ? "OBJECTIVE COMPLETE" : key == "Exit" ? "RELAYS RESTORED" : "NEW OBJECTIVE";
        compactText.text = compactDetail ?? (compactUsesTitle ? title : detail.Split('\n')[0]);
    }

    public void Reannounce() => stage = null;
    public void SetVisible(bool value) => visibility.alpha = value ? 1 : 0;

    public void Tick(float delta)
    {
        age += delta;
        float enter = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .25f));
        float collapse = Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - expandedSeconds) / .25f));
        float compactEnter = Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - expandedSeconds - .28f) / .22f));
        expanded.alpha = enter * (1 - collapse); compact.alpha = compactEnter;
        expandedRect.anchoredPosition = new Vector2(0, -74 + (1 - enter) * 12 - collapse * 8);
        compactRect.anchoredPosition = new Vector2(0, -74 - (1 - compactEnter) * 8);
        float width = Mathf.Min(780, Mathf.Max(280, root.rect.width - 56));
        expandedRect.sizeDelta = new Vector2(width, 158); compactRect.sizeDelta = new Vector2(compactUsesThought ? Mathf.Min(width, 680) : compactUsesTitle ? Mathf.Min(width, 500) : width, compactUsesThought ? 90 : compactUsesTitle ? 48 : 58);
        float promptWidth = Mathf.Clamp(Prompt.GetPreferredValues(Prompt.text, float.PositiveInfinity, 38).x + 40, 140, 440);
        promptPanel.sizeDelta = new Vector2(Mathf.Min(promptWidth, root.rect.width - 56), 50);
        promptKeycap.gameObject.SetActive((Prompt.text ?? "").StartsWith("E —") || (Prompt.text ?? "").StartsWith("T —"));
        promptPanel.gameObject.SetActive(!string.IsNullOrEmpty(Prompt.text));
    }

    static RectTransform Panel(Transform parent, string name, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = size; rect.anchoredPosition = new Vector2(0, -74);
        go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false; return rect;
    }
    static Image Strip(Transform parent, string name, Vector2 min, Vector2 max, float thickness)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = Cyan; image.raycastTarget = false;
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        rect.sizeDelta = min.x == max.x ? new Vector2(thickness, 0) : new Vector2(0, thickness);
        return image;
    }
    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, float y, float height)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size;
        text.enableAutoSizing = true; text.fontSizeMin = size - 3; text.fontSizeMax = size;
        text.color = new Color(.89f, .94f, .96f); text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; text.richText = false;
        var rect = text.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(-48, height); rect.anchoredPosition = new Vector2(0, y);
        return text;
    }
}
