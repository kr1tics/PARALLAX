using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The tutorial HUD layout, shared by scene-owned objective presenters.</summary>
public sealed class GameplayHud
{
    readonly GameObject uiRoot;
    readonly Canvas hudCanvas;
    readonly CanvasGroup hud;
    readonly TMP_Text number, heading, instruction, status, prompt;
    public TMP_Text Instruction => instruction;
    public TMP_Text Status => status;
    public TMP_Text Prompt => prompt;

    public void SetObjective(string id, string title, string detail)
    { number.text = id; heading.text = title; instruction.text = detail; }

    public void SetVisible(bool visible)
    {
        hudCanvas.scaleFactor = Mathf.Max(.75f, hudCanvas.scaleFactor);
        hud.alpha = visible ? 1 : 0;
    }

    public static TMP_Text CreateControlHint(Transform parent, TMP_FontAsset font)
    {
        var text = Label(parent, "Contextual control hints", font, 16, new Vector2(28, 24), new Vector2(1000, 32));
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = Vector2.zero;
        text.color = new Color(.70f, .73f, .75f);
        return text;
    }

    public GameplayHud(Transform parent, string name, TMP_FontAsset titleFont, TMP_FontAsset supportingFont, float titleSize = 23, float instructionSize = 17)
    {
        uiRoot = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        uiRoot.transform.SetParent(parent, false);
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
