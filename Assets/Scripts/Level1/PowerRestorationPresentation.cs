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
    private TMP_Text controls;

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
        controls = GameplayHud.CreateControlHint(hud.Instruction.GetComponentInParent<Canvas>().transform, supportingFont);
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
        controls.text = "ESC / P — Settings / Resume" + (!playerGravity.IsGravityActive ? "    ·    Q — Toggle jetpack" : "");
        var cast = interaction.fireInteractRay(true);
        var target = cast.Item1 ? cast.Item2.collider.GetComponentInParent<Interactable>() : null;
        var module = interaction.HeldObject as PowerModuleA ?? target as PowerModuleA;
        hud.Prompt.text = module != null ? module.InteractionHint : interaction.HeldObject != null ? "E — Place" :
            target is PowerModuleB repair ? repair.InteractionHint :
            target is PowerReactor console ? console.InteractionHint :
            target is PowerRouteSelector selector ? selector.InteractionHint :
            target is MovableObject movable && !movable.GetComponent<Rigidbody>().isKinematic ? "E — Pick up" :
            target is Switch ? (time == TimeState.Past ? "E — Toggle gravity" : "F — Switch Timeline") : "";
        if (reactor.IsOnline) { controls.text = ""; hud.Prompt.text = ""; }
        if (displayedTime == time) return;
        foreach (var light in roomLights)
            light.intensity = time == TimeState.Future ? 1.4f : 2.6f;
        displayedTime = time;
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
