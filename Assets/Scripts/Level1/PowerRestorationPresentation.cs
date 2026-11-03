using UnityEngine;
using Parallax.Settings;
using TMPro;

// Scene-local presentation only. Movement, input, gravity and time travel stay in the existing systems.
[DefaultExecutionOrder(-100)]
public sealed class PowerRestorationPresentation : MonoBehaviour
{
    [SerializeField] private Look playerLook;
    [SerializeField] private ArtificialGravity playerGravity;
    [SerializeField] private Light[] roomLights;
    [SerializeField] private Interact interaction;
    [SerializeField] private TMP_FontAsset titleFont, supportingFont;
    [SerializeField] private PowerModuleA moduleA;
    [SerializeField] private PowerModuleB moduleB;
    [SerializeField] private PowerReactor reactor;
    [SerializeField] private PowerCircuit circuit;
    private string previousObjective;
    private TimeState? displayedTime;
    private TutorialObjectiveHud hud;
    private TMP_Text controls, eventMessage;
    private PowerInteractionFeedback interactionFeedback;
    private ParallaxAudioPalette sounds;
    private AudioSource events, ambience;
    // Last observed state, so each change is announced once with its sound and a short line under the prompt.
    private bool wasRepairedA, wasRepairedB, wasZeroG, wasOnline;
    private int lastRoute, lastReturns;
    private MovableObject lastHeld;
    private TimeState lastTime;
    private float messageUntil;
    public string EventMessage => eventMessage.text;
    public bool ObjectiveExpanded => hud != null && hud.Expanded;
    public int ObjectiveRevision => hud != null ? hud.Revision : 0;

    // Read-only scene state for the HUD and reactor: the modules own the repair flags, the circuit the power path.
    public bool ReactorReady => circuit.ReactorReady;

    public string ReactorFailureReason => !circuit.AFeed || !moduleB.IsRepaired || circuit.Route != 2 ?
        "Power routing incomplete." : !circuit.RodsSeated ? "The reactor rods are still floating." :
        GlobalState.get().now != TimeState.Future ? "Reactor startup unavailable in this timeline." : null;

    private void Awake()
    {
        GlobalState.ResetForNewGame();
        playerLook.Rotate(new Vector2(90f, 0f));
    }

    private void Start()
    {
        var menu = GameSettingsMenu.Instance;
        menu.SetMainMenuContext(false);
        menu.SetGameplayControls(false, false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        hud = new TutorialObjectiveHud(transform, titleFont, supportingFont, "Power Restoration Objective UI", 5f, true, true);
        var canvas = hud.Instruction.GetComponentInParent<Canvas>().transform;
        controls = GameplayHud.CreateControlHint(canvas, supportingFont);
        interactionFeedback = gameObject.AddComponent<PowerInteractionFeedback>();
        interactionFeedback.Initialize(canvas, supportingFont);
        // A second line in the same style, centred under the interaction prompt, for what just changed.
        eventMessage = GameplayHud.CreateControlHint(canvas, supportingFont);
        eventMessage.name = "Power event message";
        var rect = eventMessage.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0, -130); rect.sizeDelta = new Vector2(900, 32);
        eventMessage.alignment = TextAlignmentOptions.Center; eventMessage.fontSize = 18; eventMessage.color = new Color(.35f, 1, .65f);
        sounds = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        events = gameObject.AddComponent<AudioSource>(); events.playOnAwake = false;
        ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false; ambience.loop = true;
        if (sounds != null) moduleA.gameObject.AddComponent<ParallaxDropAudio>().Configure(sounds, interaction);
        wasRepairedA = moduleA.IsRepaired; wasRepairedB = moduleB.IsRepaired; wasZeroG = PowerModuleA.StationZeroG;
        lastRoute = circuit.Route; lastReturns = moduleA.Returns; lastTime = GlobalState.get().now; wasOnline = reactor.IsOnline;
        UpdateAmbience();
        UpdateObjective(GlobalState.get().now);
    }

    public void SetObjective(string number, string title, string instruction) => hud.SetObjective(number, title, instruction);

    private void LateUpdate()
    {
        var time = GlobalState.get().now;
        UpdateObjective(time);
        hud.SetVisible(reactor.IsOnline || !GameSettingsMenu.GameplayInputBlocked);
        if (!GameSettingsMenu.GameplayInputBlocked) hud.Tick(Time.deltaTime);
        hud.Status.text = (time == TimeState.Past ? "2026 / PAST" : "2176 / FUTURE") +
            (playerGravity.IsGravityActive ? "  ·  GRAVITY ON" : "  ·  GRAVITY OFF");
        // Flight is this level's core traversal, so its key stays listed (it replaced the old wall sign).
        controls.text = "ESC / P — Settings / Resume    ·    Q — Toggle jetpack";
        Announce(time);
        eventMessage.alpha = string.IsNullOrEmpty(interactionFeedback.Message) ? 1 : 0;
        var cast = interaction.fireInteractRay(true);
        var target = cast.Item1 ? cast.Item2.collider.GetComponentInParent<Interactable>() : null;
        var module = interaction.HeldObject as PowerModuleA ?? target as PowerModuleA;
        hud.Prompt.text = module != null ? module.InteractionHint : interaction.HeldObject != null ? "E — Place" :
            target is PowerModuleB repair ? repair.InteractionHint :
            target is PowerReactor console ? console.InteractionHint :
            target is PowerRouteSelector selector ? selector.InteractionHint :
            target is MovableObject movable && !movable.GetComponent<Rigidbody>().isKinematic ? "E — Pick up" :
            target is Switch ? (PowerModuleA.StationZeroG ? "E — Restore station gravity" : "E — Cut station gravity") : "";
        if (interaction.HeldObject is PowerModuleA heldA)
            hud.Prompt.text = heldA.CanInstall ? "E — Install Module A" : "E — Place";
        else if (!hud.Prompt.text.StartsWith("E —")) hud.Prompt.text = "";
        if (reactor.IsOnline) { controls.text = ""; hud.Prompt.text = ""; }
        if (displayedTime == time && !reactor.IsOnline) return;
        // Restored power lights the hall fully in 2176 too.
        foreach (var light in roomLights)
            light.intensity = time == TimeState.Future && !reactor.IsOnline ? 1.4f : 2.6f;
        displayedTime = time;
    }

    private void Announce(TimeState time)
    {
        if (moduleA.IsRepaired && !wasRepairedA) { interactionFeedback.Clear(); Event("Module A seated · feed live", sounds?.click); }
        if (moduleB.IsRepaired && !wasRepairedB) Play(sounds?.click);
        if (circuit.Route != lastRoute) { interactionFeedback.Clear(); Event("B feed switched to route " + circuit.Route, sounds?.click); }
        if (PowerModuleA.StationZeroG != wasZeroG)
        {
            interactionFeedback.Clear();
            Event(PowerModuleA.StationZeroG ? "Station gravity OFF" : "Station gravity ON", PowerModuleA.StationZeroG ? sounds?.gravityOff : sounds?.gravityOn);
        }
        if (moduleA.Returns != lastReturns) Event("Module A returned to its cradle", null);
        if (reactor.IsOnline && !wasOnline) Event("Reactor start-up", null);
        if (time != lastTime) { interactionFeedback.Clear(); Play(sounds?.timeSwitch); UpdateAmbience(); }
        if (interaction.HeldObject != lastHeld)
        {
            if (interaction.HeldObject != null) Play(sounds?.pickup);
            else if (lastHeld != null) lastHeld.GetComponent<ParallaxDropAudio>()?.Arm();
        }
        wasRepairedA = moduleA.IsRepaired; wasRepairedB = moduleB.IsRepaired; wasZeroG = PowerModuleA.StationZeroG;
        lastRoute = circuit.Route; lastReturns = moduleA.Returns; lastTime = time; wasOnline = reactor.IsOnline;
        lastHeld = interaction.HeldObject;
        if (Time.unscaledTime > messageUntil) eventMessage.text = "";
    }

    private void Event(string message, AudioClip clip)
    {
        eventMessage.text = message; messageUntil = Time.unscaledTime + 2.5f;
        Play(clip);
    }

    private void Play(AudioClip clip) { if (clip != null) events.PlayOneShot(clip, sounds.systemVolume); }

    private void UpdateAmbience()
    {
        if (sounds == null) return;
        bool past = GlobalState.get().now == TimeState.Past;
        ambience.clip = past ? sounds.past : sounds.future;
        ambience.volume = past ? sounds.pastVolume : sounds.futureVolume;
        if (ambience.clip != null) ambience.Play();
    }

    private void UpdateObjective(TimeState time)
    {
        bool repaired = moduleA.IsRepaired && moduleB.IsRepaired;
        string stage = reactor.IsOnline ? "Complete" : !repaired ? "Modules" : circuit.Route != 2 ? "PowerFlow" :
            !circuit.RodsSeated ? "Stabilise" : "Ready";
        string title = stage == "Complete" ? "Power Restored" : stage == "Modules" ? "Restore Power Modules" :
            stage == "PowerFlow" ? "Restore the Power Flow" : stage == "Stabilise" ? "Stabilise the Reactor" : "Start the Reactor";
        string thought = stage == "Complete" ? "Engineering is online. I should keep moving." :
            stage == "Modules" ? (moduleA.IsRepaired ? "One connection restored. What else is missing?" : "I need to get these systems working again.") :
            stage == "PowerFlow" ? "Both modules are repaired. Why isn't the reactor getting power?" :
            stage == "Stabilise" ? "Those rods don't look secure yet." : "Everything seems stable. I can try starting the reactor.";
        string progress = (moduleA.IsRepaired ? "A: repaired" : "A: pending") + " · " +
            (moduleB.IsRepaired ? "B: repaired" : "B: pending");
        string display = stage + "|" + progress;
        reactor.RefreshFeedback();
        if (display == previousObjective) return;
        previousObjective = display;
        hud.SetObjective(stage, title, thought, progress + "\n" + thought);
    }
}
