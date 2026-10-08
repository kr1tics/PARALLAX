using System.Collections.Generic;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    CanvasGroup hud;
    Canvas hudCanvas;
    TMP_Text number, heading, instruction, status, prompt;
    AudioSource audioSource;
    MovableObject previousHeld;
    TimeState previousTime;
    bool previousAccess, previousEngineering, previousIntroSolved, previousMainSolved;
    readonly List<Renderer> accessDetails = new(), engineeringDetails = new();
    readonly Dictionary<MovableObject, List<Renderer>> cubeDetails = new();
    readonly Dictionary<Renderer, bool> detailStates = new();
    MaterialPropertyBlock properties;
    GameObject uiRoot;
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
        // Keep the supporting copy readable at the smallest supported desktop resolution.
        hudCanvas.scaleFactor = Mathf.Max(.75f, hudCanvas.scaleFactor);
        bool blocked = GameSettingsMenu.GameplayInputBlocked;
        hud.alpha = blocked ? 0 : 1;
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

    void SetObjective(string id, string title, string detail)
    { number.text = id; heading.text = title; instruction.text = detail; }
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
        uiRoot = new GameObject("Scene 01 Objective UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        uiRoot.transform.SetParent(transform, false);
        var canvas = uiRoot.GetComponent<Canvas>(); hudCanvas = canvas; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = uiRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        hud = uiRoot.GetComponent<CanvasGroup>(); hud.blocksRaycasts = false; hud.interactable = false;
        var panel = new GameObject("Objective panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(uiRoot.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(28, -28); rect.sizeDelta = new Vector2(470, 148);
        panel.GetComponent<Image>().color = new Color(.035f, .044f, .05f, .82f); panel.GetComponent<Image>().raycastTarget = false;
        number = Label(panel.transform, "Objective number", titleFont, 14, new Vector2(16, -12), new Vector2(438, 22));
        number.color = new Color(.56f, .59f, .60f);
        heading = Label(panel.transform, "Objective title", titleFont, titleSize, new Vector2(16, -38), new Vector2(438, 32));
        instruction = Label(panel.transform, "Current instruction", supportingFont, instructionSize, new Vector2(16, -78), new Vector2(438, 64));
        status = Label(uiRoot.transform, "Timeline status", supportingFont, 16, new Vector2(-28, -28), new Vector2(450, 28));
        var sr = status.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(1, 1); sr.pivot = new Vector2(1, 1); status.alignment = TextAlignmentOptions.TopRight;
        prompt = Label(uiRoot.transform, "Contextual interaction prompt", supportingFont, 19, new Vector2(0, -90), new Vector2(600, 36));
        var pr = prompt.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(.5f, .5f); pr.pivot = new Vector2(.5f, .5f); prompt.alignment = TextAlignmentOptions.Center;
    }
    static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, Vector2 position, Vector2 dimensions)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var text = root.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = new Color(.86f, .89f, .91f);
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
        var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = dimensions; return text;
    }
}
