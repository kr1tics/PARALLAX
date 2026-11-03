using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class Puzzle3Placeholder : MonoBehaviour
{
    bool completed;
    CanvasGroup screen;
    void Start()
    {
        if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (GameSettingsMenu.Instance != null) GameSettingsMenu.Instance.SetMainMenuContext(false);
        var intro = Resources.Load<GameObject>("UI/ContextIntro").GetComponent<ContextIntro>();
        var root = new GameObject("Cooling System Test Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(transform, false); screen = root.GetComponent<CanvasGroup>();
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var bg = Panel(root.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.gameObject.AddComponent<Image>().color = new Color32(17, 20, 25, 255);
        var accent = Panel(root.transform, "Cooling interface accent", new Vector2(.18f, .66f), new Vector2(.82f, .66f), Vector2.zero, new Vector2(0, 2));
        accent.gameObject.AddComponent<Image>().color = new Color32(232, 174, 105, 255);
        Label(root.transform, "Development status", "NARRATIVE FLOW TEST", intro.metadataFont, 16, .71f, new Color32(232, 174, 105, 255));
        Label(root.transform, "Cooling system title", "PUZZLE 3 / COOLING SYSTEM", intro.font, 36, .57f, new Color32(255, 240, 219, 255));
        Label(root.transform, "Temporary scene notice", "Temporary scene — awaiting final puzzle integration", intro.metadataFont, 21, .47f, new Color32(165, 173, 184, 255));
        var rect = Panel(root.transform, "Complete Test Level", new Vector2(.5f, .32f), new Vector2(.5f, .32f), Vector2.zero, new Vector2(330, 56));
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.23f, .17f, .10f, 1);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(CompleteTestLevel);
        var text = Label(rect, "Complete test label", "Complete Test Level", intro.font, 23, .5f, new Color32(255, 240, 219, 255));
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        if (EventSystem.current == null)
        { var events = new GameObject("Placeholder Event System", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(transform, false); }
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
    void Update()
    {
        if (TutorialCompletionScreen.DevelopmentSkipPressed) CompleteTestLevel();
    }
    public void CompleteTestLevel()
    {
        if (completed || GameSettingsMenu.GameplayInputBlocked) return;
        completed = true; screen.interactable = false; screen.alpha = 0;
        MemoryRecoveryFlow.PlayFinal();
    }
    static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = pos; rect.sizeDelta = size; return rect;
    }
    static TMP_Text Label(Transform parent, string name, string value, TMP_FontAsset font, float size, float y, Color color)
    {
        var rect = Panel(parent, name, new Vector2(.08f, y), new Vector2(.92f, y), Vector2.zero, new Vector2(0, 80));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size;
        text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
}
