using System.Collections;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Reusable story intro; the team main menu owns the Play entry.</summary>
[DefaultExecutionOrder(-200)]
public sealed class ContextIntro : MonoBehaviour
{
    [Header("Typography — null uses the existing TMP default")]
    public TMP_FontAsset font;
    public TMP_FontAsset metadataFont;
    public Color secondaryColor = new Color32(139, 146, 152, 255);
    public float metadataSize = 14f;
    public float signalSize = 18f;
    public Color signalColor = new Color32(185, 198, 205, 255);
    public float skipSize = 15f;
    public Color textColor = new Color32(220, 228, 232, 255);
    public float yearSize = 80;
    public float lineSize = 38;
    [Header("Timing (unscaled seconds)")]
    [Min(0)] public float initialDelay = 0.5f;
    [Min(0)] public float fadeDuration = 0.8f;
    [Min(0)] public float displayDuration = 2f;
    [Min(0)] public float revealDuration = 1.75f;
    [Min(0)] public float blackHoldDuration = 0.5f;
    [Header("Optional audio — empty clips are safe")]
    public AudioClip openingHum;
    public AudioClip mechanicalAmbience;
    public AudioClip signalSound;
    public AudioClip futureAmbience;
    [Range(0, 1)] public float humVolume = 0.035f;
    [Range(0, 1)] public float mechanicalVolume = 0.07f;
    [Range(0, 1)] public float signalVolume = 0.12f;
    [Range(0, 1)] public float futureVolume = 0.15f;
    [Tooltip("Optional existing Future laboratory ambience sources. Their authored volumes are restored after reveal.")]
    public AudioSource[] laboratoryAmbience = new AudioSource[0];
    public bool allowSkip = true;
    public bool showStartGameButton = true;
    public bool IsPlaying { get; private set; }
    public float LaboratoryRevealBlend => revealing ? revealProgress : 0;
    private CanvasGroup curtain;
    private TextMeshProUGUI caption, archive, signal;
    private CanvasGroup skipGroup;
    private AudioSource humSource, mechanicalSource, signalSource, futureSource;
    private float[] laboratoryVolumes;
    private float introElapsed, mechanicalStarted = -1, signalStarted = -1;
    private bool revealing, ending;
    private float revealProgress;
    private GameObject startButton, skipButton;
    private GameObject canvasObject, ownedEventSystem;
    private bool skip, ownsLock;
    private float savedTimeScale;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInputLock() => GameSettingsMenu.StartupInputBlocked = false;

    private void Awake()
    {
        savedTimeScale = Time.timeScale;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        ownsLock = true;
        GameSettingsMenu.StartupInputBlocked = true;
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Set the initial era before Entangler.Start initializes its visible objects.
        GlobalState.get().now = TimeState.Future;
        BuildUI();
        if (!showStartGameButton && startButton != null) startButton.SetActive(false);
    }

    private void BuildUI()
    {
        canvasObject = new GameObject("Context Intro Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        curtain = canvasObject.GetComponent<CanvasGroup>();
        var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
        black.transform.SetParent(canvasObject.transform, false);
        Stretch(black.GetComponent<RectTransform>());
        black.GetComponent<Image>().color = Color.black;
        caption = Text("Caption", canvasObject.transform, "PARALLAX", yearSize);
        Stretch(caption.rectTransform);
        caption.rectTransform.offsetMin = new Vector2(48, 110);
        caption.rectTransform.offsetMax = new Vector2(-48, -110);
        startButton = Button("Start Game", new Vector2(0, -140), StartGame);
        archive = Metadata("Archive", "ARCHIVE_2176", false);
        signal = Metadata("Signal", "SIGNAL DETECTED", true);
        archive.gameObject.SetActive(false);
        signal.gameObject.SetActive(false);
        signal.alpha = 0;
        var skipLabel = Text("ESC — SKIP INTRO", canvasObject.transform, "ESC — SKIP INTRO", skipSize);
        skipLabel.color = secondaryColor;
        skipLabel.alignment = TextAlignmentOptions.TopRight;
        skipLabel.raycastTarget = true;
        Corner(skipLabel.rectTransform, Vector2.one, new Vector2(-32, -24), new Vector2(210, 32));
        skipButton = skipLabel.gameObject;
        var button = skipButton.AddComponent<Button>();
        button.targetGraphic = skipLabel;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(1.1f, 1.1f, 1.1f, 1);
        colors.fadeDuration = 0.15f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(Skip);
        skipGroup = skipButton.AddComponent<CanvasGroup>();
        skipGroup.alpha = 0;
        skipButton.SetActive(false);
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            ownedEventSystem = new GameObject("Intro EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }

    private TextMeshProUGUI Metadata(string name, string value, bool isSignal)
    {
        var label = Text(name, canvasObject.transform, value, isSignal ? signalSize : metadataSize);
        if (metadataFont != null) label.font = metadataFont;
        else label.text = (isSignal ? "<mspace=10>" : "<mspace=8>") + value + "</mspace>";
        label.color = isSignal ? signalColor : secondaryColor;
        label.alignment = isSignal ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        Corner(label.rectTransform, isSignal ? new Vector2(0.5f, 0.5f) : new Vector2(0, 1),
            isSignal ? new Vector2(0, -110) : new Vector2(32, -24),
            isSignal ? new Vector2(480, 40) : new Vector2(220, 32));
        return label;
    }

    private static void Corner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI Text(string name, Transform parent, string value, float size)
    {
        // Assign the font before OnEnable can ask TMP to generate geometry.
        var go = new GameObject(name, typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        if (text.font == null)
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.text = value; text.fontSize = size; text.color = textColor;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        go.SetActive(true);
        return text;
    }

    private GameObject Button(string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasObject.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 60); rect.anchoredPosition = position;
        go.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.10f, 1);
        go.GetComponent<Button>().onClick.AddListener(action);
        var labelText = Text("Label", go.transform, label, 24);
        Stretch(labelText.rectTransform);
        return go;
    }

    public void StartGame()
    {
        if (IsPlaying || !ownsLock) return;
        IsPlaying = true; skip = false;
        introElapsed = 0; mechanicalStarted = signalStarted = -1;
        revealing = ending = false; revealProgress = 0;
        archive.gameObject.SetActive(true);
        signal.gameObject.SetActive(false);
        BeginAudio();
        if (startButton != null) startButton.SetActive(false);
        skipButton.SetActive(allowSkip);
        caption.text = "";
        StartCoroutine(Play());
    }

    public void Skip() { if (IsPlaying && allowSkip) skip = true; }
    private void Update()
    {
        if (!IsPlaying) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Skip();
        introElapsed += Time.unscaledDeltaTime;
        skipGroup.alpha = ending ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01((introElapsed - 1f) / 0.45f));
        if (signalStarted >= 0)
        {
            float age = introElapsed - signalStarted;
            signal.alpha = Mathf.Clamp01(age / 0.12f) * (1 - Mathf.Clamp01((age - 1.2f) / 0.7f));
        }
        UpdateAudio();
    }

    private IEnumerator Play()
    {
        yield return Wait(initialDelay);
        string[] lines = { "2176", "150 YEARS OF SILENCE.", "YET SOMETHING IS STILL RUNNING." };
        for (int i = 0; i < lines.Length && !skip; i++)
        {
            caption.text = lines[i]; caption.fontSize = i == 0 ? yearSize : lineSize;
            if (i == 1) mechanicalStarted = introElapsed;
            yield return FadeText(0, 1, i == 2);
            yield return Wait(displayDuration);
            yield return FadeText(1, 0);
        }
        caption.text = "";
        archive.gameObject.SetActive(false);
        signal.gameObject.SetActive(false);
        // Keep its hit area alive while hiding all text for the completely black hold.
        ending = true; skipGroup.alpha = 0;
        yield return Wait(blackHoldDuration);
        revealing = true;
        if (futureSource != null && futureSource.clip != null) futureSource.Play();
        float duration = Mathf.Max(0, revealDuration);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            revealProgress = Mathf.SmoothStep(0, 1, elapsed / duration);
            curtain.alpha = 1 - revealProgress;
            yield return null;
        }
        revealProgress = 1; UpdateAudio();
        curtain.alpha = 0;
        // Consume the skip/submit frame before returning gameplay control.
        yield return null;
        IsPlaying = false;
        Release(true);
        canvasObject.SetActive(false);
        StopIntroAudio();
    }

    private IEnumerator Wait(float duration)
    {
        for (float elapsed = 0; elapsed < Mathf.Max(0, duration) && !skip; elapsed += Time.unscaledDeltaTime)
            yield return null;
    }
    private IEnumerator FadeText(float from, float to, bool flicker = false)
    {
        caption.alpha = from;
        float duration = Mathf.Max(0, fadeDuration);
        bool signalled = false;
        for (float elapsed = 0; elapsed < duration && !skip; elapsed += Time.unscaledDeltaTime)
        {
            float progress = elapsed / duration;
            caption.alpha = Mathf.SmoothStep(from, to, progress);
            // One short dip during the third fade, never repeated flashing.
            if (flicker && progress >= 0.42f)
            {
                if (!signalled)
                {
                    signalled = true; signalStarted = introElapsed;
                    signal.gameObject.SetActive(true);
                    if (signalSource != null && signalSound != null)
                        signalSource.PlayOneShot(signalSound, signalVolume);
                }
                if (progress < 0.52f) caption.alpha *= 0.5f;
            }
            yield return null;
        }
        caption.alpha = to;
    }
    private AudioSource Audio(string name, AudioClip clip, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop;
        source.spatialBlend = 0; source.clip = clip; source.volume = 0;
        return source;
    }
    private void BeginAudio()
    {
        humSource = Audio("Intro Hum", openingHum, true);
        mechanicalSource = Audio("Intro Mechanical", mechanicalAmbience, true);
        signalSource = Audio("Intro Signal", null, false);
        signalSource.volume = 1;
        futureSource = Audio("Future Ambience", futureAmbience, true);
        if (openingHum != null) humSource.Play();
        if (mechanicalAmbience != null) mechanicalSource.Play();
        laboratoryVolumes = new float[laboratoryAmbience.Length];
        for (int i = 0; i < laboratoryAmbience.Length; i++)
            if (laboratoryAmbience[i] != null)
            {
                laboratoryVolumes[i] = laboratoryAmbience[i].volume;
                laboratoryAmbience[i].volume = 0;
            }
    }
    private void UpdateAudio()
    {
        if (humSource == null) return;
        float blend = revealing ? revealProgress : 0;
        humSource.volume = humVolume * Mathf.SmoothStep(0, 1, Mathf.Clamp01(introElapsed / 3f)) * (1 - blend);
        mechanicalSource.volume = mechanicalVolume * (mechanicalStarted < 0 ? 0 :
            Mathf.SmoothStep(0, 1, Mathf.Clamp01((introElapsed - mechanicalStarted) / 2f))) * (1 - blend);
        signalSource.volume = 1 - blend;
        futureSource.volume = futureVolume * blend;
        for (int i = 0; laboratoryVolumes != null && i < laboratoryVolumes.Length; i++)
            if (laboratoryAmbience[i] != null) laboratoryAmbience[i].volume = laboratoryVolumes[i] * blend;
    }
    private void StopIntroAudio()
    {
        if (humSource != null) humSource.Stop();
        if (mechanicalSource != null) mechanicalSource.Stop();
        if (signalSource != null) signalSource.Stop();
    }
    private void RestoreLaboratoryAudio()
    {
        for (int i = 0; laboratoryVolumes != null && i < laboratoryVolumes.Length; i++)
            if (laboratoryAmbience[i] != null) laboratoryAmbience[i].volume = laboratoryVolumes[i];
    }

    private void Release(bool gameplay)
    {
        if (!ownsLock) return;
        ownsLock = false;
        GameSettingsMenu.StartupInputBlocked = false;
        Time.timeScale = savedTimeScale;
        Cursor.lockState = gameplay ? CursorLockMode.Locked : savedCursorLock;
        Cursor.visible = gameplay ? false : savedCursorVisible;
    }
    private void OnDisable()
    {
        StopAllCoroutines(); IsPlaying = false; Release(false);
        StopIntroAudio(); RestoreLaboratoryAudio();
        if (futureSource != null) futureSource.Stop();
        if (canvasObject != null) canvasObject.SetActive(false);
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
    }
}
