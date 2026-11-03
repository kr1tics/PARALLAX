using System.Linq;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(600)]
public sealed class MemoryRecoveryCinematic : MonoBehaviour
{
    public static bool IsPlaying => active != null;
    public MemoryRecoveryStage Stage { get; private set; }
    public int PageIndex { get; private set; }
    public bool EndCardReady { get; private set; }
    static MemoryRecoveryCinematic active;
    MemoryRecoverySequence sequence;
    string destination;
    GameObject root, backdrop, ownedEvents;
    CanvasGroup content, pageGroup, decoration;
    RectTransform panel;
    Image blackout, rule, progress;
    TMP_Text header, main, secondary, skip, chapter, counter;
    Button returnButton;
    AudioSource tone, hum;
    ParallaxAudioPalette palette;
    float elapsed, revealAge, previousScale, endCardAge;
    CanvasGroup returnGroup;
    CursorLockMode previousCursor;
    bool previousVisible, ownsLock, skipArmed, finishing, navigating, initialized;
    int closedSettingsFrame = -1;
    bool Final => Stage == MemoryRecoveryStage.Restored;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => active = null;

    public static MemoryRecoveryCinematic Play(MemoryRecoveryStage stage, string destination = null)
    {
        if (active != null) return active;
        var go = new GameObject("Memory Recovery Narrative");
        var view = go.AddComponent<MemoryRecoveryCinematic>();
        view.Configure(stage, destination);
        return view;
    }
    void Awake()
    {
        previousScale = Time.timeScale;
        previousCursor = Cursor.lockState; previousVisible = Cursor.visible;
        ownsLock = true;
        GameSettingsMenu.NarrativeInputBlocked = true;
        Time.timeScale = 0;
    }
    void Start()
    {
        if (!initialized) { Destroy(gameObject); return; }
        Time.timeScale = 0;
        if (GameSettingsMenu.Instance != null) GameSettingsMenu.Instance.SetMainMenuContext(false);
    }
    void Configure(MemoryRecoveryStage stage, string next)
    {
        active = this; Stage = stage; destination = next; sequence = MemoryRecoverySequence.For(stage);
        initialized = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        var intro = Resources.Load<GameObject>("UI/ContextIntro")?.GetComponent<ContextIntro>();
        var titleFont = intro != null && intro.font != null ? intro.font : TMP_Settings.defaultFontAsset;
        var bodyFont = intro != null && intro.metadataFont != null ? intro.metadataFont : titleFont;
        root = new GameObject("Memory Recovery Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        backdrop = Rect(root.transform, "Narrative background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        blackout = backdrop.AddComponent<Image>(); blackout.color = sequence.Background; blackout.raycastTarget = false;
        var container = Rect(root.transform, "Narrative content", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        content = container.gameObject.AddComponent<CanvasGroup>(); content.blocksRaycasts = true;
        panel = Rect(container, "Backup terminal", new Vector2(.12f, .55f), new Vector2(.88f, .55f), Vector2.zero, new Vector2(0, 260));
        if (stage == MemoryRecoveryStage.Trace) panel.anchoredPosition = new Vector2(0, -16);
        pageGroup = panel.gameObject.AddComponent<CanvasGroup>();
        var frame = Rect(panel, "Terminal decoration", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        decoration = frame.gameObject.AddComponent<CanvasGroup>();
        var plate = frame.gameObject.AddComponent<Image>(); plate.color = new Color(0, 0, 0, .18f); plate.raycastTarget = false;
        rule = Rect(frame, "Terminal accent", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2)).gameObject.AddComponent<Image>();
        rule.color = sequence.Accent; rule.raycastTarget = false;
        header = Text(panel, "Terminal status", bodyFont, 17, -30, 32, sequence.Accent);
        main = Text(panel, "Terminal message", titleFont, 36, -76, 104, sequence.Foreground);
        secondary = Text(panel, "Terminal detail", bodyFont, 22, -184, 66, sequence.Foreground * new Color(1, 1, 1, .78f));
        chapter = Text(frame, "Archive chapter", bodyFont, 13, 34, 26, sequence.Accent);
        chapter.text = sequence.Label; chapter.alignment = TextAlignmentOptions.Left;
        counter = Text(frame, "Archive index", bodyFont, 13, 34, 26, sequence.Accent); counter.alignment = TextAlignmentOptions.Right;
        progress = Rect(frame, "Archive recovery progress", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, -12), new Vector2(0, 2)).gameObject.AddComponent<Image>();
        progress.color = sequence.Accent * new Color(1, 1, 1, .4f); progress.raycastTarget = false;
        skip = Text(container, "Skip narrative", bodyFont, 17, 0, 32, sequence.Foreground * new Color(1, 1, 1, .62f));
        skip.rectTransform.anchorMin = new Vector2(.08f, 0); skip.rectTransform.anchorMax = new Vector2(.92f, 0);
        skip.rectTransform.anchoredPosition = new Vector2(0, 44);
        skip.text = "Space — Skip entire sequence    ·    Esc / P — Settings";
        palette = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        tone = root.AddComponent<AudioSource>(); tone.playOnAwake = false; tone.spatialBlend = 0;
        hum = root.AddComponent<AudioSource>(); hum.playOnAwake = false; hum.spatialBlend = 0; hum.loop = true;
        if (palette != null && palette.deviceHum != null) { hum.clip = palette.deviceHum; hum.volume = 0; hum.Play(); }
        if (GameSettingsMenu.Instance != null) GameSettingsMenu.Instance.OpenChanged += SettingsChanged;
        if (stage == MemoryRecoveryStage.Trace)
        {
            var rail = Rect(frame, "Archive file rail", Vector2.zero, new Vector2(0, 1), new Vector2(-14, 0), new Vector2(2, 0));
            rail.gameObject.AddComponent<Image>().color = sequence.Accent * new Color(1, 1, 1, .35f);
        }
        ShowPage(0);
    }
    void SettingsChanged(bool open) { if (!open) closedSettingsFrame = Time.frameCount; }
    void LateUpdate()
    {
        if (!initialized || navigating) return;
        var menu = GameSettingsMenu.Instance;
        if (menu != null && menu.IsOpen || Time.frameCount == closedSettingsFrame)
        { content.alpha = 0; content.interactable = false; content.blocksRaycasts = false; return; }
        content.alpha = 1; content.interactable = true; content.blocksRaycasts = true;
        HideGameplayHud();
        if (EndCardReady)
        {
            endCardAge += Mathf.Min(Time.unscaledDeltaTime, .1f);
            pageGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(endCardAge / 1.6f));
            returnGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((endCardAge - 1.4f) / .8f));
            returnButton.interactable = endCardAge >= 2.2f;
            return;
        }
        bool released = Keyboard.current == null || !Keyboard.current.spaceKey.isPressed;
        skipArmed |= released;
        if (!finishing && skipArmed && elapsed > .25f && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        { finishing = true; revealAge = 0; }
        float delta = Mathf.Min(Time.unscaledDeltaTime, .1f);
        if (finishing)
        {
            if (!released) return;
            if (Final) { ShowEndCard(); return; }
            revealAge += delta;
            float reveal = Mathf.SmoothStep(0, 1, Mathf.Clamp01(revealAge / (Stage == MemoryRecoveryStage.Signal ? 2.4f : .65f)));
            content.alpha = 1 - reveal;
            if (Stage == MemoryRecoveryStage.Signal)
                blackout.color = new Color(sequence.Background.r, sequence.Background.g, sequence.Background.b, 1 - reveal);
            if (palette != null) hum.volume = palette.humVolume * .35f * (1 - reveal);
            if (reveal >= 1) Finish();
            return;
        }
        elapsed += delta;
        float duration = sequence.Pages[PageIndex].Duration;
        float fade = Mathf.Min(Mathf.Clamp01(elapsed / .7f), Mathf.Clamp01((duration - elapsed) / .6f));
        pageGroup.alpha = fade;
        panel.anchoredPosition = new Vector2(0, (Stage == MemoryRecoveryStage.Trace ? -16 : 0) - 8 * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .8f))));
        float quietPulse = Stage == MemoryRecoveryStage.Fragment ? .88f + Mathf.Sin(elapsed * 1.4f) * .08f : .94f;
        var accent = Final && PageIndex == 2 ? Color.Lerp(sequence.Accent, Color.white, Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * .35f) : sequence.Accent;
        rule.color = accent * new Color(1, 1, 1, quietPulse);
        header.color = accent;
        progress.rectTransform.anchorMax = new Vector2((PageIndex + Mathf.Clamp01(elapsed / duration)) / sequence.Pages.Length, 0);
        if (palette != null) hum.volume = palette.humVolume * .35f * Mathf.Clamp01(elapsed);
        if (Final && PageIndex == 4) secondary.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 2.8f) / 1.2f));
        if (elapsed >= duration)
        {
            if (PageIndex + 1 < sequence.Pages.Length) ShowPage(PageIndex + 1);
            else { finishing = true; revealAge = 0; }
        }
        HideGameplayHud();
    }
    void HideGameplayHud()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.name is "Power Restoration Objective UI" or "Cargo Bay Objective UI")
            { var group = canvas.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 0; }
    }
    void ShowPage(int index)
    {
        PageIndex = index; elapsed = 0; pageGroup.alpha = 0;
        var page = sequence.Pages[index];
        header.text = page.Header; main.text = page.Main; secondary.text = page.Secondary;
        secondary.alpha = 1; counter.text = $"{index + 1:00} / {sequence.Pages.Length:00}";
        bool quiet = Final && index >= 3;
        decoration.alpha = quiet ? 0 : 1; header.alpha = quiet ? 0 : 1;
        main.fontSize = quiet ? 42 : 36;
        if (Stage == MemoryRecoveryStage.Trace) counter.text = $"ARCHIVE {index + 1:00}";
        if (Stage == MemoryRecoveryStage.Fragment && index == 2) counter.text = "THERMAL LIMIT";
        if (Final && index == 2) rule.color = Color.Lerp(sequence.Accent, Color.white, .4f);
        if (palette != null && palette.click != null && !quiet)
        { tone.pitch = Stage == MemoryRecoveryStage.Trace ? .88f : Stage == MemoryRecoveryStage.Fragment ? .78f : 1; tone.PlayOneShot(palette.click, palette.uiVolume * .22f); }
    }
    void ShowEndCard()
    {
        EndCardReady = true; finishing = false;
        pageGroup.alpha = 0; decoration.alpha = header.alpha = 0; skip.text = "";
        main.text = "PARALLAX"; main.fontSize = 64; main.characterSpacing = 12;
        secondary.text = "THE END"; secondary.alpha = .8f; secondary.fontSize = 18; secondary.characterSpacing = 5;
        panel.anchoredPosition = Vector2.zero;
        var rect = Rect(content.transform, "Return to Main Menu", new Vector2(.5f, .22f), new Vector2(.5f, .22f), Vector2.zero, new Vector2(320, 52));
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.20f, .12f, .19f, .8f);
        returnGroup = rect.gameObject.AddComponent<CanvasGroup>(); returnGroup.alpha = 0;
        returnButton = rect.gameObject.AddComponent<Button>(); returnButton.targetGraphic = image; returnButton.interactable = false;
        var label = Text(rect, "Return label", main.font, 22, 0, 52, sequence.Foreground);
        label.text = "Return to Main Menu"; label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        returnButton.onClick.AddListener(ReturnToMenu);
        if (EventSystem.current == null)
        {
            ownedEvents = new GameObject("Narrative Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ownedEvents.transform.SetParent(transform, false);
        }
        EventSystem.current?.SetSelectedGameObject(returnButton.gameObject);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (palette != null && palette.complete != null) tone.PlayOneShot(palette.complete, palette.systemVolume * .15f);
        hum.Stop();
    }
    public void ReturnToMenu()
    {
        if (!EndCardReady || endCardAge < 2.2f || navigating || GameSettingsMenu.Instance != null && GameSettingsMenu.Instance.IsOpen) return;
        destination = "MainMenu";
        Finish();
    }
    void Finish()
    {
        if (navigating) return;
        navigating = true;
        Release();
        if (!string.IsNullOrEmpty(destination))
        {
            if (Application.CanStreamedLevelBeLoaded(destination)) SceneManager.LoadScene(destination);
            else { Debug.LogError("Narrative destination is unavailable: " + destination); navigating = false; }
        }
        else Destroy(gameObject);
    }
    void Release()
    {
        if (!ownsLock) return;
        ownsLock = false;
        if (GameSettingsMenu.Instance != null)
        { GameSettingsMenu.Instance.OpenChanged -= SettingsChanged; if (GameSettingsMenu.Instance.IsOpen) GameSettingsMenu.Instance.Close(); }
        GameSettingsMenu.NarrativeInputBlocked = false;
        Time.timeScale = previousScale;
        Cursor.lockState = previousCursor; Cursor.visible = previousVisible;
        if (active == this) active = null;
    }
    void OnDisable() { Release(); }
    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.anchoredPosition = pos; rect.sizeDelta = size; return rect;
    }
    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, float y, float height, Color color)
    {
        var rect = Rect(parent, name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y - height / 2), new Vector2(-64, height));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size;
        text.textWrappingMode = TextWrappingModes.Normal; text.alignment = TextAlignmentOptions.Center;
        text.color = color; text.raycastTarget = false; return text;
    }
}
