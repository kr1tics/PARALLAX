using System.Linq;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(500)]
public sealed class PowerRestorationIntro : MonoBehaviour
{
    static bool queued;
    static string previousScene;
    bool observing;
    TutorialCompletionScreen completion;
    Button continueButton;
    CanvasGroup gameplayHud;
    PowerRestorationOpeningView view;
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        queued = false; previousScene = null; IsPlaying = false;
        GameSettingsMenu.NarrativeInputBlocked = false;
        SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded;
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool play = mode == LoadSceneMode.Single && queued && previousScene == "StartScene" && scene.name == "Level1_PowerRestoration";
        queued = false; previousScene = scene.name;
        if (mode != LoadSceneMode.Single || (scene.name != "StartScene" && !play)) return;
        var go = new GameObject(play ? "Power Restoration narrative" : "Tutorial narrative listener");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<PowerRestorationIntro>().observing = !play;
    }

    void Start()
    {
        if (observing)
        {
            completion = FindObjectsByType<TutorialCompletionScreen>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == gameObject.scene);
            return;
        }
        var canvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(x => x.gameObject.scene == gameObject.scene && x.name == "Power Restoration Objective UI");
        gameplayHud = canvas.GetComponent<CanvasGroup>();
        var labels = canvas.GetComponentsInChildren<TMP_Text>(true);
        var title = labels.Single(x => x.name == "Objective title").font;
        var body = labels.Single(x => x.name == "Current instruction").font;
        view = new PowerRestorationOpeningView(transform, title, body);
        gameplayHud.alpha = 0; IsPlaying = true;
    }

    void LateUpdate()
    {
        if (observing)
        {
            if (continueButton != null || completion == null || !completion.IsReady) return;
            continueButton = completion.GetComponentsInChildren<Button>().FirstOrDefault(x => x.name == "CONTINUE");
            if (continueButton != null) continueButton.onClick.AddListener(QueueOpening);
            return;
        }
        view.Tick();
        if (view.Active) gameplayHud.alpha = 0;
        else { IsPlaying = false; Destroy(gameObject); }
    }

    static void QueueOpening() => queued = true;

    void OnDisable()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(QueueOpening);
        if (view != null) { view.Dispose(); IsPlaying = false; }
    }
}

internal sealed class PowerRestorationOpeningView
{
    const float Reveal = 15.8f;
    float Handoff => Reveal + (skipRequested ? .8f : 3.2f);
    float Duration => Handoff + 3.2f;
    readonly GameObject root;
    readonly CanvasGroup group, panelGroup;
    readonly GameObject backdrop;
    readonly Image blackout;
    readonly RectTransform panel;
    readonly TMP_Text header, message, detail, skip;
    readonly Image rule;
    readonly AudioSource audio, hum;
    readonly ParallaxAudioPalette palette;
    readonly float previousScale;
    readonly bool previousCursorVisible;
    readonly CursorLockMode previousCursorLock;
    bool ownsLock = true, skipArmed, skipRequested;
    int previousLine = -1;
    float age;
    int resumedSettingsFrame = -1;
    public bool Active { get; private set; } = true;

    public PowerRestorationOpeningView(Transform parent, TMP_FontAsset titleFont, TMP_FontAsset supportingFont)
    {
        previousScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        GameSettingsMenu.NarrativeInputBlocked = true;
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        root = new GameObject("Power Restoration Opening", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        backdrop = new GameObject("Power Restoration Backdrop", typeof(RectTransform), typeof(Canvas));
        backdrop.transform.SetParent(parent, false);
        var backdropCanvas = backdrop.GetComponent<Canvas>();
        backdropCanvas.renderMode = RenderMode.ScreenSpaceOverlay; backdropCanvas.sortingOrder = 39;
        var cover = Rect(backdrop.transform, "Station reveal blackout", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        blackout = cover.gameObject.AddComponent<Image>(); blackout.color = Color.black; blackout.raycastTarget = false;
        panel = Rect(root.transform, "Backup terminal", new Vector2(.12f, .55f), new Vector2(.88f, .55f), new Vector2(0, 0), new Vector2(0, 220));
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<Image>().color = new Color(.018f, .035f, .045f, .88f);
        var accent = Rect(panel, "Terminal accent", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));
        rule = accent.gameObject.AddComponent<Image>(); rule.color = new Color(.29f, .78f, .89f, .85f);
        header = Text(panel, "Terminal status", supportingFont, 17, -25, 30);
        header.color = new Color(.46f, .83f, .92f);
        message = Text(panel, "Terminal message", titleFont, 34, -70, 105);
        detail = Text(panel, "Terminal detail", supportingFont, 21, -142, 64);
        skip = Text(root.transform, "Skip transmission", supportingFont, 18, -36, 32);
        skip.rectTransform.anchorMin = new Vector2(.2f, 0); skip.rectTransform.anchorMax = new Vector2(.8f, 0);
        skip.rectTransform.pivot = new Vector2(.5f, 0); skip.rectTransform.anchoredPosition = new Vector2(0, 34);
        skip.text = "Space — Skip    ·    Esc / P — Settings";
        skip.color = new Color(.75f, .83f, .87f);
        palette = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
        hum = root.AddComponent<AudioSource>(); hum.playOnAwake = false; hum.spatialBlend = 0; hum.loop = true; hum.volume = 0;
        if (palette != null && palette.deviceHum != null) { hum.clip = palette.deviceHum; hum.Play(); }
        if (GameSettingsMenu.Instance != null) GameSettingsMenu.Instance.OpenChanged += SettingsChanged;
        Draw();
    }

    void SettingsChanged(bool open) { if (!open) resumedSettingsFrame = Time.frameCount; }

    public void Tick()
    {
        if (!Active) return;
        var menu = GameSettingsMenu.Instance;
        if (Time.frameCount == resumedSettingsFrame || menu != null && (menu.IsOpen || (!ownsLock && GameSettingsMenu.GameplayInputBlocked)))
        { group.alpha = 0; return; }
        group.alpha = 1;
        skipArmed |= SkipReleased();
        if (ownsLock && skipArmed && age > .25f && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            float progress = Mathf.Clamp01((age - Reveal) / (Handoff - Reveal));
            skipRequested = true;
            age = Reveal + progress * (Handoff - Reveal);
        }
        else if (age < Handoff || !ownsLock || SkipReleased()) age += Mathf.Min(Time.unscaledDeltaTime, .1f);
        Draw();
        if (age >= Handoff && ownsLock && SkipReleased()) Release();
        if (age >= Duration && !ownsLock) Dispose();
    }

    static bool SkipReleased() => Keyboard.current == null || !Keyboard.current.spaceKey.isPressed;

    void Draw()
    {
        int line = age < 4f ? 0 : age < 8.7f ? 1 : age < 10f ? 2 : age < Reveal ? 3 : age < Handoff ? 4 : 5;
        float revealProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - Reveal) / (Handoff - Reveal)));
        blackout.color = new Color(0, 0, 0, 1 - revealProgress);
        hum.volume = palette == null ? 0 : palette.humVolume * .6f * Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 1.5f)) * (1 - revealProgress);
        panelGroup.alpha = line == 4 ? 0 : 1;
        if (line != previousLine)
        {
            previousLine = line;
            if (palette != null && palette.click != null && (line == 0 || line == 2 || line == 5))
            {
                audio.pitch = line == 2 ? .72f : line == 5 ? 1.12f : 1f;
                audio.PlayOneShot(palette.click, palette.uiVolume * .2f);
            }
            header.text = line == 2 ? "BACKUP TERMINAL / READ FAILED" : line == 5 ? "FIRST OBJECTIVE — RESTORE POWER" :
                line == 4 ? "" : "BACKUP TERMINAL / EMERGENCY ACCESS";
            message.text = line == 0 ? "Emergency interface… online." :
                line == 1 || line == 2 ? "Station records inaccessible. Main power required." :
                line == 3 ? "Restore power to recover the station’s final transmission." : line == 5 ? "Bring Engineering online." : "";
            detail.text = line == 5 ? "Find out what happened here." : "";
            if (line == 5)
            {
                header.font = message.font; header.fontSize = 28;
                header.rectTransform.sizeDelta = new Vector2(-64, 50);
                header.rectTransform.anchoredPosition = new Vector2(0, -45);
                message.fontSize = 26;
            }
            panel.anchorMin = new Vector2(.12f, line == 5 ? 1 : .55f);
            panel.anchorMax = new Vector2(.88f, line == 5 ? 1 : .55f);
            panel.anchoredPosition = new Vector2(0, line == 5 ? -180 : 0);
            skip.text = line == 5 ? "" : "Space — Skip    ·    Esc / P — Settings";
        }
        float start = line == 0 ? 0 : line == 1 ? 4f : line == 2 ? 8.7f : line == 3 ? 10f : line == 4 ? Reveal : Handoff;
        float end = line == 0 ? 4f : line == 1 ? 8.7f : line == 2 ? 10f : line == 3 ? Reveal : line == 4 ? Handoff : Duration;
        float fade = line == 2 ? 1 : Mathf.Min(Mathf.Clamp01((age - start) / .65f), Mathf.Clamp01((end - age) / .55f));
        message.alpha = fade;
        detail.alpha = fade;
        rule.color = line == 2 ? new Color(.94f, .68f, .29f, .75f) : new Color(.29f, .78f, .89f, .82f + Mathf.Sin(age * 5) * .025f);
    }

    void Release()
    {
        ownsLock = false;
        GameSettingsMenu.NarrativeInputBlocked = false;
        Time.timeScale = previousScale;
        Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
    }

    public void Dispose()
    {
        if (!Active) return;
        if (ownsLock)
        {
            if (GameSettingsMenu.Instance != null && GameSettingsMenu.Instance.IsOpen) GameSettingsMenu.Instance.Close();
            Release();
        }
        if (GameSettingsMenu.Instance != null) GameSettingsMenu.Instance.OpenChanged -= SettingsChanged;
        Active = false;
        Object.Destroy(root);
        Object.Destroy(backdrop);
    }

    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, float y, float height)
    {
        var rect = Rect(parent, name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y - height / 2), new Vector2(-64, height));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size;
        text.color = new Color(.88f, .94f, .96f); text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
