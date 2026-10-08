using System.Collections.Generic;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.UIElements;
using UButton = UnityEngine.UI.Button;
using Button = UnityEngine.UIElements.Button;

/// <summary>Scene-owned sound observer. Does not change puzzle, timeline or physics state.</summary>
[DefaultExecutionOrder(500)]
public sealed class ParallaxSceneAudio : MonoBehaviour
{
    [SerializeField] ParallaxAudioPalette palette;
    [SerializeField] Level1Controller level;
    [SerializeField] Interact interaction;
    [SerializeField] PressureButton accessPlate, engineeringPlate;
    AudioSource primary, secondary, events, ui, accessHum, engineeringHum;
    ContextIntro intro;
    TutorialCompletionScreen completion;
    MovableObject previousHeld;
    TimeState previousTime;
    float previousGravity, nextBind;
    bool previousAccessSolved, previousEngineeringSolved, previousReady;
    readonly HashSet<VisualElement> roots = new();
    readonly HashSet<UButton> buttons = new();
    public float PastVolume => level == null ? 0 : primary.volume;
    public float FutureVolume => level == null ? 0 : secondary.volume;
    void Start()
    {
        if (palette == null) palette = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        if (palette == null) { enabled = false; return; }
        primary = Source(transform, "Primary ambience", level == null ? palette.menu : palette.past, true, false);
        secondary = Source(transform, "Future ambience", level == null ? null : palette.future, true, false);
        events = Source(transform, "System and interaction audio", null, false, false);
        ui = Source(transform, "UI audio", null, false, false); ui.ignoreListenerPause = true;
        if (level != null)
        {
            intro = FindFirstObjectByType<ContextIntro>(); completion = GetComponent<TutorialCompletionScreen>();
            accessHum = Source(accessPlate.transform, "Access relay device hum", palette.deviceHum, true, true);
            engineeringHum = Source(engineeringPlate.transform, "Engineering relay device hum", palette.deviceHum, true, true);
            previousHeld = interaction.HeldObject;
            previousTime = GlobalState.get().now; previousGravity = GlobalState.get().gravity;
            foreach (var cube in FindObjectsByType<MovableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cube.gameObject.scene == gameObject.scene) cube.gameObject.AddComponent<ParallaxDropAudio>().Configure(palette, interaction);
        }
        BindUI();
    }
    void LateUpdate()
    {
        if (primary == null) return;
        if (Time.unscaledTime >= nextBind) { nextBind = Time.unscaledTime + .5f; BindUI(); }
        bool story = intro != null && intro.IsPlaying;
        var state = GlobalState.get();
        float step = Time.unscaledDeltaTime / palette.crossfadeSeconds;
        primary.volume = Mathf.MoveTowards(primary.volume, story ? 0 : level == null ? palette.menuVolume : state.now == TimeState.Past ? palette.pastVolume : 0, step * (level == null ? palette.menuVolume : palette.pastVolume));
        secondary.volume = Mathf.MoveTowards(secondary.volume, level != null ? palette.futureVolume * (story ? intro.LaboratoryRevealBlend : state.now == TimeState.Future ? 1 : 0) : 0, step * palette.futureVolume);
        if (level == null) return;
        accessHum.volume = Mathf.MoveTowards(accessHum.volume, !story && level.IntroSolved ? palette.humVolume : 0, step * palette.humVolume);
        engineeringHum.volume = Mathf.MoveTowards(engineeringHum.volume, !story && level.MainSolved ? palette.humVolume : 0, step * palette.humVolume);
        bool ready = completion != null && completion.IsReady;
        if (ready && !previousReady) PlayEvent(palette.complete, palette.systemVolume);
        previousReady = ready;
        if (GameSettingsMenu.GameplayInputBlocked) return;
        if (state.now != previousTime) PlayEvent(palette.timeSwitch, palette.systemVolume);
        if (previousGravity > .1f && state.gravity < .1f) PlayEvent(palette.gravityOff, palette.systemVolume);
        var held = interaction.HeldObject;
        if (held != previousHeld)
        {
            if (held != null) PlayEvent(palette.pickup, palette.interactionVolume);
            if (previousHeld != null && held == null) previousHeld.GetComponent<ParallaxDropAudio>()?.Arm();
        }
        if (level.IntroSolved && !previousAccessSolved || level.MainSolved && !previousEngineeringSolved)
            PlayEvent(palette.click, palette.systemVolume);
        previousTime = state.now; previousGravity = state.gravity; previousHeld = held;
        previousAccessSolved = level.IntroSolved; previousEngineeringSolved = level.MainSolved;
    }
    void PlayEvent(AudioClip clip, float volume) { if (clip != null) events.PlayOneShot(clip, volume); }
    AudioSource Source(Transform parent, string name, AudioClip clip, bool loop, bool spatial)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.clip = clip;
        source.loop = loop; source.volume = loop ? 0 : 1;
        source.spatialBlend = spatial ? 1 : 0; source.minDistance = 1.5f; source.maxDistance = 9;
        source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
        if (loop && clip != null) source.Play(); return source;
    }
    void BindUI()
    {
        foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            var root = document.rootVisualElement;
            if (root != null && roots.Add(root)) { root.RegisterCallback<ClickEvent>(Click, TrickleDown.TrickleDown); root.RegisterCallback<NavigationSubmitEvent>(Submit, TrickleDown.TrickleDown); }
        }
        foreach (var button in FindObjectsByType<UButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.gameObject.scene == gameObject.scene && buttons.Add(button)) button.onClick.AddListener(Blip);
    }
    void Click(ClickEvent evt) { if (evt.target is VisualElement target && (target is Button || target.GetFirstAncestorOfType<Button>() != null)) Blip(); }
    void Submit(NavigationSubmitEvent evt) { if (evt.target is Button) Blip(); }
    void Blip() { if (ui != null && palette.click != null) ui.PlayOneShot(palette.click, palette.uiVolume); }
    void OnDisable()
    {
        foreach (var root in roots) { root.UnregisterCallback<ClickEvent>(Click, TrickleDown.TrickleDown); root.UnregisterCallback<NavigationSubmitEvent>(Submit, TrickleDown.TrickleDown); }
        foreach (var button in buttons) if (button != null) button.onClick.RemoveListener(Blip);
        roots.Clear(); buttons.Clear();
    }
}
