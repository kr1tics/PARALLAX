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
    private GameplayHud hud;
    private TMP_Text controls, eventMessage;
    private ParallaxAudioPalette sounds;
    private AudioSource events, ambience;
    // Last observed state, so each change is announced once with its sound and a short line under the prompt.
    private bool wasRepairedA, wasRepairedB, wasZeroG, wasOnline;
    private int lastRoute, lastReturns;
    private MovableObject lastHeld;
    private TimeState lastTime;
    private float messageUntil;
    public string EventMessage => eventMessage.text;

    // Read-only scene state for the HUD and reactor: the modules own the repair flags, the circuit the power path.
    public bool ReactorReady => circuit.ReactorReady;

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
        hud = new GameplayHud(transform, "Power Restoration Objective UI", titleFont, supportingFont);
        var canvas = hud.Instruction.GetComponentInParent<Canvas>().transform;
        controls = GameplayHud.CreateControlHint(canvas, supportingFont);
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
        hud.Status.text = (time == TimeState.Past ? "2026 / PAST" : "2176 / FUTURE") +
            (playerGravity.IsGravityActive ? "  ·  GRAVITY ON" : "  ·  GRAVITY OFF");
        // Flight is this level's core traversal, so its key stays listed (it replaced the old wall sign).
        controls.text = "ESC / P — Settings / Resume    ·    Q — Toggle jetpack";
        Announce(time);
        var cast = interaction.fireInteractRay(true);
        var target = cast.Item1 ? cast.Item2.collider.GetComponentInParent<Interactable>() : null;
        var module = interaction.HeldObject as PowerModuleA ?? target as PowerModuleA;
        hud.Prompt.text = module != null ? module.InteractionHint : interaction.HeldObject != null ? "E — Place" :
            target is PowerModuleB repair ? repair.InteractionHint :
            target is PowerReactor console ? console.InteractionHint :
            target is PowerRouteSelector selector ? selector.InteractionHint :
            target is MovableObject movable && !movable.GetComponent<Rigidbody>().isKinematic ? "E — Pick up" :
            target is Switch ? (PowerModuleA.StationZeroG ? "E — Restore station gravity" : "E — Cut station gravity") : "";
        if (reactor.IsOnline) { controls.text = ""; hud.Prompt.text = ""; }
        if (displayedTime == time && !reactor.IsOnline) return;
        // Restored power lights the hall fully in 2176 too.
        foreach (var light in roomLights)
            light.intensity = time == TimeState.Future && !reactor.IsOnline ? 1.4f : 2.6f;
        displayedTime = time;
    }

    private void Announce(TimeState time)
    {
        if (moduleA.IsRepaired && !wasRepairedA) Event("Module A seated · feed live", sounds?.click);
        if (moduleB.IsRepaired && !wasRepairedB) Event("Module B repaired", sounds?.click);
        if (circuit.Route != lastRoute) Event("B feed switched to route " + circuit.Route, sounds?.click);
        if (PowerModuleA.StationZeroG != wasZeroG) Event(PowerModuleA.StationZeroG ? "Station gravity OFF" : "Station gravity ON",
            PowerModuleA.StationZeroG ? sounds?.gravityOff : sounds?.gravityOn);
        if (moduleA.Returns != lastReturns) Event("Module A returned to its cradle", null);
        if (reactor.IsOnline && !wasOnline) Event("Reactor start-up", null);
        if (time != lastTime) { Play(sounds?.timeSwitch); UpdateAmbience(); }
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
        string progress = (moduleA.IsRepaired ? "A repaired" : "A pending") + " · " +
            (moduleB.IsRepaired ? "B repaired" : "B pending");
        bool repaired = moduleA.IsRepaired && moduleB.IsRepaired;
        // States, not steps: each line names what is wrong now, never the fix.
        string next = reactor.IsOnline ? "Reactor online. Power restored." :
            ReactorReady ? (time == TimeState.Future ? "Reactor ready. E — Activate console." : "Reactor ready in 2176.") :
            repaired ? (time == TimeState.Past ? "Check the reactor in 2176." :
                !circuit.BFeed ? "B has no feed in 2176." : "Control rods are not seated.") :
            time != TimeState.Past ? "Repairs are made in 2026." :
            !moduleA.IsRepaired ? (PowerModuleA.StationZeroG ? "Module A floats free." : "Module A is too heavy to lift.") :
            "B's junction has power now.";
        string instruction = progress + "\n" + next;
        if (instruction == previousObjective) return;
        previousObjective = instruction;
        string number = reactor.IsOnline ? "04" : !repaired ? "01" : !ReactorReady ? "02" : "03";
        string title = reactor.IsOnline ? "Power Restored" : !repaired ? "Restore Power Modules" :
            !ReactorReady ? "Complete the Power Path" : "Activate the Reactor";
        SetObjective("OBJECTIVE " + number, title, instruction);
        reactor.RefreshFeedback();
    }
}
