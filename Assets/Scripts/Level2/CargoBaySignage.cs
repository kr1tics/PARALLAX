using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(650)]
public sealed class CargoBaySignage : MonoBehaviour
{
    [SerializeField] CargoBayController level;
    [SerializeField] Rigidbody[] manifests;
    [SerializeField] PressureButton counterweight, receivingOne, receivingTwo;
    [SerializeField] TMP_FontAsset titleFont, bodyFont;
    [SerializeField] Renderer[] legacyLabels;
    [SerializeField] TMP_Text[] legacyTexts;
    readonly List<bool> originalTextEnabled = new();
    [SerializeField] Bounds containment;
    readonly List<bool> originalVisibility = new();
    readonly List<Plate> manifestPlates = new();
    readonly List<GameObject> roots = new();
    Plate staging, weight, r1, r2, clamp, cage;
    Interact interaction;
    static readonly Color Ink = new Color32(220, 228, 232, 255);
    static readonly Color Muted = new Color32(149, 174, 182, 255);
    static readonly Color Amber = new Color32(232, 174, 105, 255);
    static readonly Color Green = new Color32(112, 222, 164, 255);
    sealed class Plate { public TMP_Text State; public Image Accent; public int Manifest = -1; }
    void Start()
    {
        interaction = FindFirstObjectByType<Interact>();
        foreach (var text in legacyTexts) { originalTextEnabled.Add(text.enabled); text.enabled = false; }
        foreach (var renderer in legacyLabels) { originalVisibility.Add(renderer.enabled); renderer.enabled = false; }
        staging = Make("A freight plate", transform, new Vector3(-10.5f, -.48f, -4.245f), 0, new Vector2(1.2f, .46f), "A / FREIGHT RELEASE", "ARCHIVE STAGING");
        weight = Make("P counterweight plate", transform, new Vector3(-10.8f, -.48f, 1.155f), 0, new Vector2(1.2f, .46f), "P / COUNTERWEIGHT", "RACK CONTROL");
        r1 = Make("R1 receiving plate", transform, new Vector3(13.5f, 1.05f, 13.105f), 0, new Vector2(1.35f, .5f), "R1 / RECEIVING", "MANIFEST C1");
        r2 = Make("R2 receiving plate", transform, new Vector3(-11.925f, 1.1f, 11.55f), 270, new Vector2(1.35f, .5f), "R2 / RECEIVING", "MANIFEST C2");
        clamp = Make("C2 rack identification", transform, new Vector3(-13.095f, 2.72f, 2.6f), 270, new Vector2(1.25f, .46f), "C2 / SECURING CLAMP", "ARCHIVE RACK");
        cage = Make("C1 containment identification", transform, new Vector3(containment.min.x + 1.05f, 1.85f, containment.min.z - .035f), 0,
            new Vector2(1.5f, .5f), "C1 / CONTAINMENT", "FREIGHT STORAGE");
        Make("Cargo hall header", transform, new Vector3(0, 4.3f, -8.82f), 0, new Vector2(2.1f, .3f), "CARGO BAY / 02", "", true);
        Make("Freight gate header", transform, new Vector3(10.5f, 2.1f, 7.025f), 0, new Vector2(2.35f, .25f), "FREIGHT ACCESS / 02", "", true);
        Make("Maintenance gate header", transform, new Vector3(10.5f, 2.1f, 13.025f), 0, new Vector2(2.35f, .25f), "MAINTENANCE ACCESS", "", true);
        Make("Archive rail identification", transform, new Vector3(-13.095f, 2.75f, -4.8f), 270, new Vector2(1.75f, .28f), "ARCHIVE RACKS", "", true);
        for (int index = 0; index < manifests.Length; index++)
        {
            var body = manifests[index].transform;
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90;
                var rotation = Quaternion.Euler(0, yaw, 0);
                var local = rotation * new Vector3(0, .08f, -.515f);
                var plate = Make("Cargo unit " + index + " plate " + side, body, body.TransformPoint(local), yaw,
                    new Vector2(.61f, .31f), index < 2 ? "C1 / FREIGHT" : "C2 / ARCHIVE", "MANIFEST UNIT");
                plate.Manifest = index; manifestPlates.Add(plate);
            }
        }
        Refresh();
    }
    void LateUpdate() { if (staging != null) Refresh(); }
    void Refresh()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        Set(staging, level.FreightAccessGranted ? "ACCESS GRANTED" : "STANDBY", level.FreightAccessGranted);
        Set(weight, counterweight.IsPressed ? "ENGAGED" : "STANDBY", counterweight.IsPressed);
        Set(r1, level.CargoDelivered || (!past && receivingOne.IsPressed) ? "RECEIVED" : "AWAITING CARGO", level.CargoDelivered || (!past && receivingOne.IsPressed));
        Set(r2, level.CargoDelivered || (!past && receivingTwo.IsPressed) ? "RECEIVED" : "AWAITING CARGO", level.CargoDelivered || (!past && receivingTwo.IsPressed));
        var activeRack = manifests[past ? 2 : 3];
        Set(clamp, level.RackReleased ? "EMPTY" : activeRack.isKinematic ? "LOCKED" : "RELEASED", level.RackReleased || !activeRack.isKinematic);
        bool occupied = !past && containment.Contains(manifests[1].transform.position);
        Set(cage, occupied ? "SEALED" : "CLEAR", !occupied);
        foreach (var plate in manifestPlates)
        {
            bool received = level.CargoDelivered || (!past && (plate.Manifest < 2 ? receivingOne.IsPressed : receivingTwo.IsPressed));
            var body = manifests[plate.Manifest];
            bool secured = body.isKinematic || (plate.Manifest == 1 && occupied);
            bool held = interaction != null && interaction.HeldObject == body.GetComponent<MovableObject>();
            Set(plate, received ? "RECEIVED" : held ? "IN TRANSIT" : secured ? "SECURED" : "AVAILABLE", received);
            if (!received && !secured) plate.State.color = Ink;
            if (!received) plate.Accent.color = plate.Manifest < 2 ? Amber : new Color32(125, 197, 220, 255);
        }
    }
    static void Set(Plate plate, string text, bool live)
    {
        if (plate.State.text != text) plate.State.text = text;
        plate.State.color = plate.Accent.color = live ? Green : Amber;
    }
    Plate Make(string name, Transform parent, Vector3 position, float yaw, Vector2 size, string heading, string detail, bool header = false)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(Image)); roots.Add(root);
        root.transform.SetParent(parent, false); root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var scale = parent.lossyScale; root.transform.localScale = new Vector3(.01f / scale.x, .01f / scale.y, .01f / scale.z);
        root.GetComponent<RectTransform>().sizeDelta = size * 100;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var background = root.GetComponent<Image>(); background.color = new Color32(17, 24, 29, 246); background.raycastTarget = false;
        var strip = new GameObject("Equipment accent", typeof(RectTransform), typeof(Image)); strip.transform.SetParent(root.transform, false);
        var rect = strip.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(3, 0); rect.sizeDelta = new Vector2(1, -8);
        var accent = strip.GetComponent<Image>(); accent.color = Muted; accent.raycastTarget = false;
        bool small = size.x < .8f;
        var title = Label(root.transform, "Equipment title", titleFont, small ? 8.5f : header ? 10 : 8.5f, header ? -7 : -5, header ? 18 : 13); title.text = heading;
        var body = Label(root.transform, "Equipment detail", bodyFont, small ? 4.6f : 6.5f, small ? -15 : -20, 10); body.text = detail; body.color = Muted;
        var state = Label(root.transform, "Equipment state", bodyFont, small ? 4.8f : 7, -size.y * 100 + (small ? 8 : 14), small ? 8 : 12);
        if (small) body.gameObject.SetActive(false);
        if (header) { body.gameObject.SetActive(false); state.gameObject.SetActive(false); }
        return new Plate { State = state, Accent = accent };
    }
    static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, float top, float height)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var label = root.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
        label.enableAutoSizing = true; label.fontSizeMin = size * .8f; label.fontSizeMax = size;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.alignment = TextAlignmentOptions.TopLeft;
        label.color = Ink; label.raycastTarget = false;
        var rect = label.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, top); rect.sizeDelta = new Vector2(-14, height); return label;
    }
    void OnDestroy()
    {
        for (int i = 0; i < originalTextEnabled.Count; i++) if (legacyTexts[i] != null) legacyTexts[i].enabled = originalTextEnabled[i];
        for (int i = 0; i < originalVisibility.Count; i++) if (legacyLabels[i] != null) legacyLabels[i].enabled = originalVisibility[i];
        foreach (var root in roots) if (root != null) Destroy(root);
    }
}
