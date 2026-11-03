using TMPro;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The tutorial HUD layout, shared by scene-owned objective presenters.</summary>
public sealed class GameplayHud
{
    readonly GameObject uiRoot;
    readonly Canvas hudCanvas;
    readonly CanvasGroup hud;
    readonly TMP_Text number, heading, instruction, status, prompt;
    readonly bool puzzleStyle;
    RectTransform objectivePanel, promptPanel, keycap;
    CanvasGroup promptGroup;
    float expandedUntil;
    float expansion = 1;
    int lastFrame = -1;
    public TMP_Text Instruction => instruction;
    public TMP_Text Status => status;
    public TMP_Text Prompt => prompt;

    public void SetObjective(string id, string title, string detail)
    {
        if (puzzleStyle && (number.text != id || heading.text != title))
            expandedUntil = Time.time + 3.6f;
        number.text = id; heading.text = title; instruction.text = detail;
    }

    public void SetVisible(bool visible)
    {
        hudCanvas.scaleFactor = Mathf.Max(.75f, hudCanvas.scaleFactor);
        hud.alpha = visible ? 1 : 0;
        if (puzzleStyle) UpdatePuzzleLayout();
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
        puzzleStyle = name == "Power Restoration Objective UI" || name == "Cargo Bay Objective UI";
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
        if (puzzleStyle)
        {
            objectivePanel = rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            bool cargo = name == "Cargo Bay Objective UI";
            number.color = cargo ? new Color32(181, 160, 255, 255) : new Color(.38f, .76f, .83f);
            if (cargo)
            {
                panel.GetComponent<Image>().color = new Color32(16, 15, 32, 225);
                heading.color = new Color32(240, 235, 255, 255);
                instruction.color = new Color32(213, 205, 237, 255);
            }
            number.alignment = heading.alignment = instruction.alignment = TextAlignmentOptions.Center;
            var accent = new GameObject("Objective accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(panel.transform, false);
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0, 0); ar.anchorMax = new Vector2(1, 0);
            ar.sizeDelta = new Vector2(0, 1); ar.anchoredPosition = Vector2.zero;
            accent.GetComponent<Image>().color = cargo ? new Color32(181, 160, 255, 165) : new Color(.25f, .65f, .72f, .6f);
            accent.GetComponent<Image>().raycastTarget = false;
            var background = new GameObject("Interaction background", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            background.transform.SetParent(uiRoot.transform, false);
            promptPanel = background.GetComponent<RectTransform>();
            promptPanel.anchorMin = promptPanel.anchorMax = promptPanel.pivot = new Vector2(.5f, .2f);
            background.GetComponent<Image>().color = new Color(.045f, .035f, .022f, .78f);
            background.GetComponent<Image>().raycastTarget = false;
            promptGroup = background.GetComponent<CanvasGroup>();
            prompt.transform.SetParent(background.transform, false);
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(.5f, .5f);
            pr.anchoredPosition = Vector2.zero;
            prompt.color = new Color(.98f, .89f, .70f); prompt.fontSize = 21;
            var key = new GameObject("Interaction keycap", typeof(RectTransform), typeof(Image));
            key.transform.SetParent(background.transform, false); key.transform.SetAsFirstSibling();
            keycap = key.GetComponent<RectTransform>();
            keycap.anchorMin = keycap.anchorMax = keycap.pivot = new Vector2(0, .5f);
            keycap.anchoredPosition = new Vector2(8, 0); keycap.sizeDelta = new Vector2(36, 36);
            key.GetComponent<Image>().color = new Color(.42f, .30f, .12f, .9f);
            key.GetComponent<Image>().raycastTarget = false;
            uiRoot.AddComponent<PuzzleSettingsPlacement>();
        }
    }
    void UpdatePuzzleLayout()
    {
        float width = ((RectTransform)uiRoot.transform).rect.width;
        if (width < 1) width = 1920;
        if (lastFrame != Time.frameCount)
        {
            expansion = Mathf.MoveTowards(expansion, Time.time < expandedUntil ? 1 : 0, Time.deltaTime * 3);
            lastFrame = Time.frameCount;
        }
        float eased = Mathf.SmoothStep(0, 1, expansion);
        float panelWidth = Mathf.Min(width - 56, Mathf.Lerp(680, 780, eased));
        objectivePanel.anchoredPosition = new Vector2(0, -Mathf.Lerp(76, 84, eased));
        objectivePanel.sizeDelta = new Vector2(panelWidth, Mathf.Lerp(110, 158, eased));
        SetLabel(number, panelWidth, 8, 20, Mathf.Lerp(11, 13, eased));
        SetLabel(heading, panelWidth, Mathf.Lerp(29, 34, eased), 34, Mathf.Lerp(21, 29, eased));
        SetLabel(instruction, panelWidth, Mathf.Lerp(65, 85, eased), Mathf.Lerp(40, 64, eased), 16);
        promptGroup.alpha = string.IsNullOrEmpty(prompt.text) ? 0 : 1;
        var desired = prompt.GetPreferredValues(prompt.text, Mathf.Min(760, width - 100), 0);
        float promptWidth = Mathf.Clamp(desired.x + 36, 140, Mathf.Min(800, width - 56));
        float promptHeight = Mathf.Max(48, desired.y + 16);
        promptPanel.sizeDelta = new Vector2(promptWidth, promptHeight);
        prompt.rectTransform.sizeDelta = new Vector2(promptWidth - 24, promptHeight - 12);
        bool hasKey = (prompt.text ?? "").StartsWith("E —") || (prompt.text ?? "").StartsWith("T —");
        keycap.gameObject.SetActive(hasKey);
        if (hasKey && !prompt.isTextOverflowing)
        {
            prompt.ForceMeshUpdate();
            if (prompt.textInfo.characterCount > 0)
                keycap.anchoredPosition = new Vector2(promptWidth * .5f + prompt.textInfo.characterInfo[0].bottomLeft.x - 9, 0);
        }
    }
    static void SetLabel(TMP_Text label, float width, float top, float height, float size)
    {
        label.rectTransform.anchoredPosition = new Vector2(16, -top);
        label.rectTransform.sizeDelta = new Vector2(width - 32, height);
        label.fontSize = size;
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

[DefaultExecutionOrder(450)]
public sealed class PuzzleSettingsPlacement : MonoBehaviour
{
    UnityEngine.UIElements.Button launcher;
    UnityEngine.UIElements.StyleLength top, bottom, left, right;
    void LateUpdate()
    {
        var menu = GameSettingsMenu.Instance;
        if (menu == null) return;
        var document = menu.GetComponent<UnityEngine.UIElements.UIDocument>();
        if (document == null || document.rootVisualElement == null) return;
        var current = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(document.rootVisualElement, "open-settings");
        if (current == null) return;
        if (launcher != current)
        {
            Restore(); launcher = current;
            top = launcher.style.top; bottom = launcher.style.bottom;
            left = launcher.style.left; right = launcher.style.right;
        }
        launcher.style.top = 24; launcher.style.left = 24;
        launcher.style.bottom = UnityEngine.UIElements.StyleKeyword.Auto;
        launcher.style.right = UnityEngine.UIElements.StyleKeyword.Auto;
    }
    void Restore()
    {
        if (launcher == null) return;
        launcher.style.top = top; launcher.style.bottom = bottom;
        launcher.style.left = left; launcher.style.right = right;
        launcher = null;
    }
    void OnDestroy() => Restore();
}
