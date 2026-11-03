using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(650)]
public sealed class PowerRestorationSignage : MonoBehaviour
{
    [SerializeField] PowerModuleA moduleA;
    [SerializeField] PowerModuleB moduleB;
    [SerializeField] PowerCircuit circuit;
    [SerializeField] PowerReactor reactor;
    [SerializeField] TextMesh connectionFeedback, consoleFeedback;
    [SerializeField] Renderer[] legacyLabels;
    readonly List<GameObject> plates = new();
    readonly List<bool> originalVisibility = new();
    TMP_FontAsset titleFont, bodyFont;
    Plate identityA, socketA, nodeB, reactorStatus, console, selector, gravity, rodsStatus;
    readonly List<Image> dockIndicators = new();
    readonly List<RectTransform> docks = new();
    bool rodsWereSeated;
    float seatConfirmedAt = -10;
    AudioSource rodAudio;
    ParallaxAudioPalette palette;
    static readonly Color Ink = new Color32(220, 228, 232, 255);
    static readonly Color Muted = new Color32(149, 174, 182, 255);
    static readonly Color Amber = new Color32(232, 174, 105, 255);
    static readonly Color Green = new Color32(112, 222, 164, 255);
    sealed class Plate { public TMP_Text Detail, State; public Image Indicator; }

    void Start()
    {
        var fonts = Resources.Load<GameObject>("UI/ContextIntro").GetComponent<ContextIntro>();
        titleFont = fonts.font; bodyFont = fonts.metadataFont;
        var rodLabel = GameObject.Find("Control rod status");
        if (rodLabel != null)
        {
            var labels = new List<Renderer>(legacyLabels); labels.Add(rodLabel.GetComponent<Renderer>()); legacyLabels = labels.ToArray();
        }
        foreach (var label in legacyLabels) { originalVisibility.Add(label.enabled); label.enabled = false; }
        var structure = transform;
        identityA = Make("Module A identification", moduleA.transform, moduleA.transform.TransformPoint(new Vector3(0, .08f, -.54f)), 0,
            new Vector2(.88f, .54f), "POWER MODULE A-01", "380 KG / HEAVY LOAD", 8.8f);
        socketA = Make("Module A socket", structure, new Vector3(-6, 2.12f, 3.53f), 0,
            new Vector2(1.55f, .29f), "A-01 / POWER DOCK", "", 9);
        nodeB = Make("Module B identification", moduleB.transform.parent, new Vector3(8.145f, 11.8f, 1.4f), 90,
            new Vector2(1.8f, .9f), "DISTRIBUTION NODE B-02", "", 12);
        reactorStatus = Make("Reactor identification", structure, new Vector3(0, 2.55f, 2.765f), 0,
            new Vector2(1.3f, .48f), "REACTOR 01", "", 10);
        console = Make("Reactor console identification", reactor.transform.parent, new Vector3(3.5f, 1.48f, 2.735f), 0,
            new Vector2(1.46f, .55f), "REACTOR CONTROL", "", 10);
        selector = Make("Route selector identification", structure, new Vector3(8.285f, .82f, 1), 90,
            new Vector2(1.1f, .4f), "B-02 / FEED SELECTOR", "", 8.5f);
        gravity = Make("Station gravity identification", structure, new Vector3(-7.885f, .64f, -2), 270,
            new Vector2(.98f, .44f), "STATION GRAVITY", "", 9);
        Make("Route 1 designation", structure, new Vector3(8.87f, 2.05f, 4.6f), 90, new Vector2(.85f, .22f), "ROUTE 1", "", 9);
        Make("Route 2 designation", structure, new Vector3(8.87f, 2.05f, -2.6f), 90, new Vector2(.85f, .22f), "ROUTE 2", "", 9);
        Make("Distribution wall designation", structure, new Vector3(8.87f, 3.35f, 1), 90,
            new Vector2(1.65f, .25f), "POWER DISTRIBUTION", "", 9.5f);
        Make("Maintenance bay designation", structure, new Vector3(-8.87f, 3.35f, 1), 270,
            new Vector2(1.65f, .25f), "MAINTENANCE BAY", "", 10);
        Make("Reactor hall designation", structure, new Vector3(0, 3.35f, -10.72f), 180,
            new Vector2(1.65f, .25f), "REACTOR HALL 01", "", 10);
        var lamps = Make("Reactor indicator legend", reactor.transform.parent, new Vector3(3.5f, 1.86f, 2.81f), 0,
            new Vector2(1.35f, .13f), "", "", 6.5f);
        var lampRoot = lamps.Indicator.transform.parent;
        for (int i = 0; i < 3; i++)
        {
            var label = Label(lampRoot, "Indicator " + i, bodyFont, 6.5f, Vector2.zero, new Vector2(26, 12));
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.anchoredPosition = new Vector2((i - 1) * 45, 0);
            label.text = i == 0 ? "A" : i == 1 ? "B" : "RODS"; label.alignment = TextAlignmentOptions.Center;
        }
        rodsStatus = Make("Control rod assembly", structure, new Vector3(0, 1.05f, 1.93f), 0,
            new Vector2(1.45f, .56f), "REACTOR CORE / CONTROL RODS", "", 8.2f);
        rodsWereSeated = circuit.RodsSeated;
        palette = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        var audioRoot = new GameObject("Control rod seating audio"); audioRoot.transform.SetParent(transform, false);
        audioRoot.transform.position = new Vector3(0, 1, 4.5f);
        rodAudio = audioRoot.AddComponent<AudioSource>(); rodAudio.playOnAwake = false;
        rodAudio.spatialBlend = 1; rodAudio.minDistance = 3; rodAudio.maxDistance = 18;
        rodAudio.rolloffMode = AudioRolloffMode.Linear; rodAudio.dopplerLevel = 0;
        foreach (var socket in GameObject.Find("Control rods").GetComponentsInChildren<Renderer>())
            if (socket.name == "Rod socket") MakeDock(socket.transform.position + Vector3.up * .071f);
        Refresh();
    }
    void LateUpdate() { if (identityA != null) Refresh(); }
    void Refresh()
    {
        State(identityA, moduleA.IsRepaired ? "ONLINE" : "OFFLINE", moduleA.IsRepaired);
        State(socketA, moduleA.IsRepaired ? "CONNECTED" : "AWAITING MODULE", moduleA.IsRepaired);
        string b = connectionFeedback.text;
        State(nodeB, b, b == "REPAIR COMPLETE" || b == "FEED LIVE");
        State(reactorStatus, reactor.IsOnline ? "ONLINE" : "OFFLINE", reactor.IsOnline);
        string status = consoleFeedback.text;
        State(console, reactor.IsOnline ? "POWER RESTORED" : status.Contains("READY") ? "READY" : "LOCKED", reactor.IsOnline || status.Contains("READY"));
        console.Detail.text = circuit.RodsSeated ? "RODS / SEATED" : "RODS / FLOATING";
        console.Detail.color = circuit.RodsSeated ? Green : Amber;
        State(selector, "ROUTE " + circuit.Route, circuit.BFeed);
        State(gravity, circuit.RodsSeated ? "ON" : "OFF", circuit.RodsSeated);
        bool seated = circuit.RodsSeated;
        if (seated && !rodsWereSeated)
        {
            seatConfirmedAt = Time.time;
            if (palette != null && palette.drop != null) rodAudio.PlayOneShot(palette.drop, palette.interactionVolume * .65f);
        }
        rodsWereSeated = seated;
        State(rodsStatus, seated ? "RODS SECURED" : "RODS UNSTABLE", seated);
        rodsStatus.Detail.text = seated ? "Reactor rods secured." : "Control rods out of position.";
        float pulse = Mathf.Clamp01(1 - (Time.time - seatConfirmedAt) / .65f);
        Color dockColor = seated ? Color.Lerp(Green, Ink, pulse * .5f) : Amber;
        foreach (var indicator in dockIndicators) indicator.color = dockColor;
        foreach (var dock in docks) dock.localScale = Vector3.one * (.01f * (1 + pulse * .08f));
    }
    void MakeDock(Vector3 position)
    {
        var root = new GameObject("Control rod docking frame", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false); root.transform.SetPositionAndRotation(position, Quaternion.Euler(90, 0, 0));
        root.transform.localScale = Vector3.one * .01f;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(72, 72); docks.Add(root.GetComponent<RectTransform>());
        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
        {
            for (int side = 0; side < 2; side++)
            {
                var strip = new GameObject("Dock alignment corner", typeof(RectTransform), typeof(Image)); strip.transform.SetParent(root.transform, false);
                var rect = strip.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = side == 0 ? new Vector2(17, 2) : new Vector2(2, 17);
                rect.anchoredPosition = side == 0 ? new Vector2(x * 26, y * 34) : new Vector2(x * 34, y * 26);
                var image = strip.GetComponent<Image>(); image.raycastTarget = false; dockIndicators.Add(image);
            }
        }
    }
    static void State(Plate plate, string value, bool live)
    {
        if (plate.State.text != value) plate.State.text = value;
        plate.State.color = plate.Indicator.color = live ? Green : Amber;
    }
    Plate Make(string name, Transform parent, Vector3 position, float yaw, Vector2 size, string title, string detail, float headingSize)
    {
        var go = new GameObject("Power Restoration Equipment Signs", typeof(RectTransform), typeof(Canvas));
        plates.Add(go); go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * .01f;
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size * 100;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
        var background = go.AddComponent<Image>(); background.color = new Color32(17, 24, 29, 246); background.raycastTarget = false;
        float height = rect.sizeDelta.y;
        var stripe = new GameObject(name + " accent", typeof(RectTransform), typeof(Image)); stripe.transform.SetParent(go.transform, false);
        var sr = stripe.GetComponent<RectTransform>(); sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(0, 1);
        sr.anchoredPosition = new Vector2(3, 0); sr.sizeDelta = new Vector2(1, -8);
        var indicator = stripe.GetComponent<Image>(); indicator.color = Muted; indicator.raycastTarget = false;
        var heading = Label(go.transform, name + " title", titleFont, headingSize, new Vector2(0, -5), new Vector2(-16, height > 60 ? 23 : 13)); heading.text = title;
        var body = Label(go.transform, name + " detail", bodyFont, 7, new Vector2(0, -22), new Vector2(-16, 12)); body.text = detail; body.color = Muted;
        float stateY = height > 60 ? -48 : height < 32 ? -17 : -height + 16;
        var state = Label(go.transform, name + " state", bodyFont, height > 60 ? 12 : height < 32 ? 6 : 7.5f, new Vector2(0, stateY), new Vector2(-16, height > 60 ? 23 : 12));
        return new Plate { Detail = body, State = state, Indicator = indicator };
    }
    static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, Vector2 position, Vector2 dimensions)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var text = root.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = Ink;
        text.enableAutoSizing = true; text.fontSizeMin = size * .8f; text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false;
        var rect = text.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = position; rect.sizeDelta = dimensions; return text;
    }
    void OnDestroy()
    {
        for (int i = 0; i < originalVisibility.Count; i++) if (legacyLabels[i] != null) legacyLabels[i].enabled = originalVisibility[i];
        foreach (var plate in plates) if (plate != null) Destroy(plate);
    }
}
