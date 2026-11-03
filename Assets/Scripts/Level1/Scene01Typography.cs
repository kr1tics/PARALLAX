using System.Collections.Generic;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

/// <summary>Typography confined to this scene and its Settings document. No global font changes.</summary>
[DefaultExecutionOrder(350)]
public sealed class Scene01Typography : MonoBehaviour
{
    [SerializeField] TMP_FontAsset headingFont, supportingFont;
    [SerializeField] TMP_Text[] environmentalHeadings;
    [SerializeField] Color primaryColor = new Color(.863f, .894f, .910f);
    [SerializeField] Color secondaryColor = new Color(.545f, .573f, .596f);
    readonly HashSet<TMP_Text> configured = new();
    readonly HashSet<TMP_Text> headings = new();
    readonly List<GameObject> roots = new();
    readonly Dictionary<VisualElement, OriginalStyle> originalSettingsStyles = new();
    VisualElement settingsRoot;
    float nextCheck;

    struct OriginalStyle
    {
        public StyleFontDefinition font;
        public StyleEnum<FontStyle> weight;
        public StyleColor color;
        public StyleLength spacing, top, bottom, left, right, height, size;
        public OriginalStyle(IStyle style)
        { font = style.unityFontDefinition; weight = style.unityFontStyleAndWeight; color = style.color; spacing = style.letterSpacing; top = style.top; bottom = style.bottom; left = style.left; right = style.right; height = style.height; size = style.fontSize; }
        public void Restore(IStyle style)
        { style.unityFontDefinition = font; style.unityFontStyleAndWeight = weight; style.color = color; style.letterSpacing = spacing; style.top = top; style.bottom = bottom; style.left = left; style.right = right; style.height = height; style.fontSize = size; }
    }

    void Start()
    {
        foreach (var text in environmentalHeadings) if (text != null) headings.Add(text);
        ApplySceneText(); ApplySettings();
    }
    void LateUpdate()
    {
        // Covers newly spawned labels and rebuilt Settings documents, without scanning every frame.
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .25f;
        ApplySceneText(); ApplySettings();
    }
    void ApplySceneText()
    {
        gameObject.scene.GetRootGameObjects(roots);
        foreach (var root in roots)
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Canvas>()?.name is "Scene 01 Objective UI" or "Power Restoration Opening" ||
                text.GetComponentInParent<ContextIntro>() != null ||
                text.GetComponentInParent<Canvas>()?.name is "Tutorial Completion Screen" or "Power Restoration Completion Screen" ||
                !configured.Add(text)) continue;
            bool heading = headings.Contains(text) || text.name == "Objective title" || text.name == "Objective number" ||
                text.GetComponentInParent<UnityEngine.UI.Button>() != null;
            TMP_FontAsset font = heading ? headingFont : supportingFont;
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            text.characterSpacing = heading ? .5f : 0;
            text.wordSpacing = 0; text.lineSpacing = 0;
            text.color = text.name == "Objective number" ? secondaryColor : primaryColor;
            text.richText = false;
            // Retain dimensions, alignment, size and text content owned by each UI/sign component.
        }
    }
    void ApplySettings()
    {
        var menu = GameSettingsMenu.Instance;
        if (menu == null) { RestoreSettings(); return; }
        var document = menu.GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;
        // The panel root also contains runtime quality dropdown labels; other UI panels are independent.
        var root = document.rootVisualElement.panel?.visualTree ?? document.rootVisualElement;
        if (root != settingsRoot) { RestoreSettings(); settingsRoot = root; }
        Configure(root, false);
        foreach (var text in root.Query<TextElement>().ToList())
        {
            bool heading = text is Button || text.ClassListContains("title") || text.ClassListContains("controls-title") ||
                text.ClassListContains("unity-base-field__label");
            Configure(text, heading);
        }
        var launcher = document.rootVisualElement.Q<Button>("open-settings");
        if (launcher != null)
        {
            if (gameObject.scene.name == "StartScene")
            {
                launcher.style.top = 24; launcher.style.bottom = StyleKeyword.Auto;
                launcher.style.left = 24; launcher.style.right = StyleKeyword.Auto;
            }
            else
            {
                launcher.style.top = StyleKeyword.Auto; launcher.style.bottom = 24;
            }
            launcher.style.height = 32;
            launcher.style.fontSize = 14;
        }
    }
    void Configure(VisualElement element, bool heading)
    {
        if (originalSettingsStyles.ContainsKey(element)) return;
        originalSettingsStyles.Add(element, new OriginalStyle(element.style));
        // UI Toolkit uses TextCore rather than TMP assets. Reuse the exact existing Regular source faces.
        var font = heading ? headingFont : supportingFont;
        element.style.unityFontDefinition = FontDefinition.FromFont(font.sourceFontFile);
        element.style.unityFontStyleAndWeight = FontStyle.Normal;
        element.style.letterSpacing = heading ? .5f : 0;
        if (element is TextElement && !(element is Button))
            element.style.color = element.ClassListContains("save-status") || element.ClassListContains("controls-hint") ||
                element.ClassListContains("slider-value") ? secondaryColor : primaryColor;
    }
    void RestoreSettings()
    {
        foreach (var entry in originalSettingsStyles) entry.Value.Restore(entry.Key.style);
        originalSettingsStyles.Clear(); settingsRoot = null;
    }
    void OnDisable() => RestoreSettings();
    void OnDestroy() => RestoreSettings();
}
