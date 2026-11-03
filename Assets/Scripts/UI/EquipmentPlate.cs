using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The station's mounted equipment plate (dark plate, thin accent, title, optional detail and state line),
/// shared by the level signage components so every level builds identical plates.</summary>
public static class EquipmentPlate
{
    public static readonly Color Ink = new Color32(220, 228, 232, 255);
    public static readonly Color Muted = new Color32(149, 174, 182, 255);
    public static readonly Color Amber = new Color32(232, 174, 105, 255);
    public static readonly Color Green = new Color32(112, 222, 164, 255);

    /// <summary>A world-space plate at <paramref name="position"/>, sized in metres; plates narrower than 0.8 m drop the
    /// detail line, headers show the title only.</summary>
    public static GameObject Create(string name, Transform parent, Vector3 position, float yaw, Vector2 size, string heading, string detail,
        TMP_FontAsset titleFont, TMP_FontAsset bodyFont, bool header, out TMP_Text state, out Image accent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(Image));
        root.transform.SetParent(parent, false); root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var scale = parent.lossyScale; root.transform.localScale = new Vector3(.01f / scale.x, .01f / scale.y, .01f / scale.z);
        root.GetComponent<RectTransform>().sizeDelta = size * 100;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var background = root.GetComponent<Image>(); background.color = new Color32(17, 24, 29, 246); background.raycastTarget = false;
        var strip = new GameObject("Equipment accent", typeof(RectTransform), typeof(Image)); strip.transform.SetParent(root.transform, false);
        var rect = strip.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(3, 0); rect.sizeDelta = new Vector2(1, -8);
        accent = strip.GetComponent<Image>(); accent.color = Muted; accent.raycastTarget = false;
        bool small = size.x < .8f;
        var title = Label(root.transform, "Equipment title", titleFont, small ? 8.5f : header ? 10 : 8.5f, header ? -7 : -5, header ? 18 : 13); title.text = heading;
        var body = Label(root.transform, "Equipment detail", bodyFont, small ? 4.6f : 6.5f, small ? -15 : -20, 10); body.text = detail; body.color = Muted;
        state = Label(root.transform, "Equipment state", bodyFont, small ? 4.8f : 7, -size.y * 100 + (small ? 8 : 14), small ? 8 : 12);
        if (small) body.gameObject.SetActive(false);
        if (header) { body.gameObject.SetActive(false); state.gameObject.SetActive(false); }
        return root;
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
}
