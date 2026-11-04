using System.Collections;
using System.Linq;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Cold Storage, the final level. Two time-linked coolant cells (existing Entangler pairs): each 2176 copy's
/// condition is the record of where its own 2026 copy spent 150 years. Storage, valve, flange, balance pan and hatch
/// are scene state; carrying, seating and time travel stay with the shared systems. Equipment plates are drawn by
/// <see cref="ColdStorageSignage"/>.</summary>
[DefaultExecutionOrder(-200)]
public sealed class ColdStorageController : MonoBehaviour
{
    public enum Site { Open, S1, S2, R, Pan, Vault }

    // One cell's 150-year history: where its 2026 copy rested and what that storage did to it.
    public struct Outcome : System.IEquatable<Outcome>
    {
        public Site site; public bool usable; public string reason;
        public bool Equals(Outcome other) => site == other.site && usable == other.usable && reason == other.reason;
    }

    [SerializeField] PlayerController player;
    [SerializeField] Vector3 arrival = new Vector3(0, 1.05f, -8);
    [Header("Cells: index 0 = A, 1 = B")]
    [SerializeField] Entangler[] cells = new Entangler[2];
    [SerializeField] Rigidbody[] pastCells = new Rigidbody[2], futureCells = new Rigidbody[2];
    [SerializeField] Renderer[] futureCoolant = new Renderer[2];
    [Header("Storage and mechanisms")]
    [SerializeField] PressureButton s1, s2, pan;
    [SerializeField] GameObject vaultGate, hatch;
    [Tooltip("Visual plug and pan deck; they follow the real hatch collider and pan load, never the other way round.")]
    [SerializeField] Transform hatchVisual, panVisual;
    [SerializeField] Bounds s1Site = new Bounds(new Vector3(0, .9f, 5), new Vector3(1.3f, 1.2f, 1.3f));
    [SerializeField] Bounds s2Site = new Bounds(new Vector3(5, 7.8f, 5.8f), new Vector3(1.3f, 1.2f, 1.3f));
    [SerializeField] Bounds panSite = new Bounds(new Vector3(2.6f, .8f, 3), new Vector3(1.3f, 1.2f, 1.3f));
    [SerializeField] Bounds rSite = new Bounds(new Vector3(9, 9.9f, 6), new Vector3(1.5f, 1.3f, 1.5f));
    [SerializeField] Bounds hatchOpening = new Bounds(new Vector3(7.9f, 9.9f, 6), new Vector3(.9f, 1.5f, 1.6f));
    [SerializeField] Bounds vaultSite = new Bounds(new Vector3(-6, 2.5f, 6.2f), new Vector3(4.6f, 5, 5.6f));
    [SerializeField] Bounds vaultDoorway = new Bounds(new Vector3(-6, 2.3f, 3), new Vector3(4.6f, 4.6f, 1.4f));
    [SerializeField] Transform flange;
    [Header("Feedback")]
    [SerializeField] TMP_Text reactorLabel;
    [SerializeField] Renderer valveLamp, consoleLamp;
    [SerializeField] Renderer[] loopLamps = { };
    [SerializeField] Material lockedMaterial, readyMaterial;
    [SerializeField] GameObject flangeCrack, flangeBurst, flangeIntact, tankCoolant, s1Frost;
    [SerializeField] GameObject[] pastOnly = { }, futureOnly = { };
    [SerializeField] Light[] zoneLights = { };
    [SerializeField] Light warningLight, coolingLight;
    [SerializeField] TMP_FontAsset titleFont, supportingFont;
    [SerializeField] TutorialCompletionScreen completion;
    [SerializeField] ParallaxAudioPalette sounds;
    [SerializeField, Min(0)] float startUpSeconds = 3f;

    static readonly Color Satisfied = new Color(.35f, 1f, .65f), Neutral = new Color(.85f, .92f, .96f), Warning = new Color(1, .7f, .3f);
    const float HatchTravel = 1.35f, PanDip = .04f;
    readonly Outcome[] history = new Outcome[2];
    readonly Outcome?[] explained = new Outcome?[2];
    readonly int[] refusals = new int[3];
    Vector3[] pastStart, futureStart;
    Quaternion[] pastRotation, futureRotation;
    Vector3 hatchClosed, panRest;
    Interact interaction;
    TutorialObjectiveHud hud;
    PowerInteractionFeedback feedback;
    bool handlingThought, bothSurvivedThought, oneInstalledThought;
    string pendingThought;
    bool recoveryThought, leakingSlotThought, sealedStorageThought;

    TMP_Text controls;
    AudioSource events, ambience, hum;
    TimeState previousTime;
    MovableObject previousHeld;
    Color[] lightColours;
    float[] lightIntensities;
    bool holdingInput, hatchWasOpen;

    public int GuidanceResetVersion { get; private set; }
    public bool ServiceMode { get; private set; }
    public bool FlangeRepaired { get; private set; }
    public bool Starting { get; private set; }
    public bool Completed { get; private set; }
    public bool HatchOpen => !hatch.activeSelf;
    public bool PanLoaded => pan.IsPressed;
    public bool HatchObstructed => HatchOpen && !PanLoaded;
    public bool VaultOpen => !vaultGate.activeSelf;
    public bool LoopSealed => FlangeRepaired && !ServiceMode;
    public bool CellUsable(int cell) => history[cell].usable;
    public string CellReason(int cell) => history[cell].reason;
    public Site CellSite(int cell) => history[cell].site;
    public Rigidbody PastCell(int cell) => pastCells[cell];
    public Rigidbody FutureCell(int cell) => futureCells[cell];
    static bool Past => GlobalState.get().now == TimeState.Past;
    MovableObject Held => interaction.HeldObject;

    void Awake()
    {
        GlobalState.ResetForNewGame();
        interaction = player.GetComponent<Interact>();
        // The inactive copies have not joined physics yet; their authored Transforms are authoritative.
        pastStart = pastCells.Select(c => c.transform.position).ToArray(); pastRotation = pastCells.Select(c => c.transform.rotation).ToArray();
        futureStart = futureCells.Select(c => c.transform.position).ToArray(); futureRotation = futureCells.Select(c => c.transform.rotation).ToArray();
        hatchClosed = hatchVisual.position; panRest = panVisual.position;
        for (int i = 0; i < 2; i++) history[i] = Evaluate(i);
    }

    void Start()
    {
        player.ResetForCheckpoint(arrival);
        Camera.main.GetComponent<Look>().Rotate(new Vector2(90, 0));
        previousTime = GlobalState.get().now;
        if (warningLight != null)
        {
            warningLight.transform.position = new Vector3(0,2.7f,7.7f);
            warningLight.color = new Color(1,.32f,.08f); warningLight.range = 8;
            warningLight.shadows = LightShadows.None;
        }
        reactorLabel.enableAutoSizing = true;
        reactorLabel.fontSizeMin = reactorLabel.fontSize * .65f;
        events = gameObject.AddComponent<AudioSource>(); events.playOnAwake = false;
        ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false; ambience.loop = true; ambience.ignoreListenerPause = true;
        hum = gameObject.AddComponent<AudioSource>(); hum.playOnAwake = false; hum.loop = true; hum.ignoreListenerPause = true;
        lightColours = zoneLights.Select(l => l.color).ToArray(); lightIntensities = zoneLights.Select(l => l.intensity).ToArray();
        foreach (var cell in pastCells.Concat(futureCells))
            if (sounds != null) cell.gameObject.AddComponent<ParallaxDropAudio>().Configure(sounds, interaction);
        var menu = GameSettingsMenu.Instance;
        // Cold Storage has no distortable objects (G hidden); R resets the plant, so its row stays.
        if (menu != null) { menu.SetMainMenuContext(false); menu.SetGameplayControls(false, true); }
        // Main's puzzle HUD: brief objective card, slim centred bar, key-cap prompt, Settings moved top-left.
        hud = new TutorialObjectiveHud(transform, titleFont, supportingFont, "Cold Storage Objective UI", 5f, true, true, new Color32(110, 205, 220, 255));
        var canvas = hud.Instruction.GetComponentInParent<Canvas>().transform;
        feedback = gameObject.AddComponent<PowerInteractionFeedback>();
        feedback.Initialize(canvas, supportingFont);
        controls = GameplayHud.CreateControlHint(canvas, supportingFont);
        UpdateAmbience(); ApplyTimeline(); ApplyConditions();
    }

    void Update()
    {
        if (GameSettingsMenu.GameplayInputBlocked || Starting) return;
        if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) || Lost(player.transform.position)) { ResetPlant(); return; }
        // A lost copy returns alone; a recovered 2026 copy is a history change for that cell only.
        for (int i = 0; i < 2; i++)
        {
            if (Lost(pastCells[i].transform.position)) Recover(pastCells[i], pastStart[i], pastRotation[i]);
            if (Lost(futureCells[i].transform.position)) Recover(futureCells[i], futureStart[i], futureRotation[i]);
        }
    }

    static bool Lost(Vector3 p) => p.y < -3 || p.y > 13 || Mathf.Abs(p.x) > 11 || Mathf.Abs(p.z) > 12;

    void LateUpdate()
    {
        hud.SetVisible(Completed || !GameSettingsMenu.GameplayInputBlocked);
        if (previousTime != GlobalState.get().now)
        {
            previousTime = GlobalState.get().now;
            if (!Past) Arrive();
            Play(sounds != null ? sounds.timeSwitch : null); UpdateAmbience(); ApplyTimeline();
        }
        if (GameSettingsMenu.GameplayInputBlocked && !Starting) return;
        hud.Tick(Time.deltaTime);
        UpdateMechanisms();
        if (previousHeld != Held)
        {
            if (Held != null) { Play(sounds != null ? sounds.pickup : null); ExplainOnPickup(); }
            else if (previousHeld != null) previousHeld.GetComponent<ParallaxDropAudio>()?.Arm();
            previousHeld = Held;
        }
        if (!Past && !Completed && !Starting)
        {
            if (!bothSurvivedThought && !Ready && CellUsable(0) && CellUsable(1))
            {
                bothSurvivedThought = true;
                pendingThought = "Both survived. I need them in the cooling slots here.";
            }
            if (!oneInstalledThought && SocketUsable(1) != SocketUsable(2))
            {
                oneInstalledThought = true;
                pendingThought = "One is in place. I still need the other cell here.";
            }
        }
        bool cellStoredInPastSlot = Past && s1.IsPressed && pastCells.Any(c => s1Site.Contains(c.transform.position) && (Held == null || Held.gameObject != c.gameObject));
        if (cellStoredInPastSlot && !FlangeRepaired && !leakingSlotThought)
        {
            leakingSlotThought = true;
            pendingThought = "The cell's in place, but this pipe is still leaking.";
        }
        if (cellStoredInPastSlot && LoopSealed && !sealedStorageThought)
        {
            sealedStorageThought = true;
            pendingThought = "Cell sealed for long-term storage.";
        }
        if (pendingThought == "The cell's in place, but this pipe is still leaking." && (!cellStoredInPastSlot || FlangeRepaired)) pendingThought = null;
        if (pendingThought == "Cell sealed for long-term storage." && (!cellStoredInPastSlot || !LoopSealed)) pendingThought = null;
        if (pendingThought != null && string.IsNullOrEmpty(feedback.Message))
        {
            feedback.NotifyEnvironmentEvent(pendingThought);
            pendingThought = null;
        }
        UpdateFeedback();
    }

    // Each arrival in 2176 re-reads every cell's own history. Unchanged history leaves that cell's 2176 copy exactly as
    // the player left it; changed history re-places it where its 2026 copy rests, in the condition that storage produced.
    void Arrive()
    {
        for (int i = 0; i < 2; i++)
        {
            var outcome = Evaluate(i);
            if (outcome.Equals(history[i])) continue;
            history[i] = outcome;
            Place(futureCells[i], pastCells[i].transform.position, pastCells[i].transform.rotation);
        }
        ApplyConditions();
    }

    public Site SiteOf(Vector3 p) =>
        s1Site.Contains(p) ? Site.S1 : s2Site.Contains(p) ? Site.S2 : rSite.Contains(p) ? Site.R :
        panSite.Contains(p) ? Site.Pan : vaultSite.Contains(p) ? Site.Vault : Site.Open;

    // Only the dependencies of this cell's own storage count.
    Outcome Evaluate(int cell)
    {
        var site = SiteOf(pastCells[cell].transform.position);
        switch (site)
        {
            case Site.S1: return Make(site, LoopSealed, LoopSealed ? "SEALED IN THE LOOP" : ServiceMode ? "BAY VENTED" : "LOOP LEAKED AT F3");
            case Site.R: bool closed = RSealedIn2026(); return Make(site, closed, closed ? "PASSIVE COLD" : "HATCH LEFT OPEN");
            case Site.S2: return Make(site, false, "UNSEALED SOCKET");
            case Site.Pan: return Make(site, false, "LEFT ON BALANCE PAN");
            case Site.Vault: return Make(site, false, "ACTIVE CHILL FAILED");
            default: return Make(site, false, "UNSTORED");
        }
    }
    static Outcome Make(Site site, bool usable, string reason) => new Outcome { site = site, usable = usable, reason = reason };

    // R stays sealed for 150 years only if nothing in 2026 holds its hatch: no cartridge on the pan and nothing in the
    // opening. The player counts too: this runs on the frame of arrival, where the player stands as they leave 2026.
    bool RSealedIn2026()
    {
        for (int i = 0; i < 2; i++)
        {
            Vector3 p = pastCells[i].transform.position;
            if (panSite.Contains(p) || hatchOpening.Intersects(new Bounds(p, Vector3.one * .9f))) return false;
        }
        return !hatchOpening.Intersects(player.GetComponent<Collider>().bounds);
    }

    void UpdateMechanisms()
    {
        var playerBounds = player.GetComponent<Collider>().bounds;
        var active = Past ? pastCells : futureCells;
        // Present-day mechanics only. The hatch never closes through the player or a cell, and holds while obstructed.
        bool hatchHeld = HatchOpen && (hatchOpening.Intersects(playerBounds) || active.Any(c => hatchOpening.Intersects(c.GetComponent<Collider>().bounds)));
        hatch.SetActive(!(pan.IsPressed || hatchHeld));
        if (HatchOpen != hatchWasOpen)
        {
            hatchWasOpen = HatchOpen;
            Play(sounds != null ? sounds.click : null);
            if (HatchOpen && PanLoaded) pendingThought = "Something opened above me. I should take a look.";
        }
        // The plug rises as soon as the collider clears and settles once it is back; the pan dips under a cartridge.
        var target = hatchClosed + (HatchOpen ? Vector3.up * HatchTravel : Vector3.zero);
        hatchVisual.position = Vector3.MoveTowards(hatchVisual.position, target, Time.deltaTime * HatchTravel / (HatchOpen ? .4f : .25f));
        panVisual.position = Vector3.MoveTowards(panVisual.position, panRest + (PanLoaded ? Vector3.down * PanDip : Vector3.zero), Time.deltaTime * .2f);
        if (!Past) { vaultGate.SetActive(false); return; }
        bool vaultHeld = vaultSite.Contains(player.transform.position) || (VaultOpen &&
            (vaultDoorway.Intersects(playerBounds) || active.Any(c => vaultDoorway.Intersects(c.GetComponent<Collider>().bounds))));
        vaultGate.SetActive(!(ServiceMode || vaultHeld));
    }

    // Returns why the action was refused (shown once per press by the shared refusal notice), or null when it ran.
    public string Operate(ColdStorageControl.Kind kind)
    {
        if (GameSettingsMenu.GameplayInputBlocked || Starting || Completed) return null;
        switch (kind)
        {
            case ColdStorageControl.Kind.Valve:
                if (!Past) { Play(sounds != null ? sounds.drop : null); return Refuse(0, "The valve won't turn.", "It's frozen in place."); }
                ServiceMode = !ServiceMode;
                Play(sounds != null ? sounds.click : null);
                if (LoopSealed) feedback.NotifyEnvironmentEvent("Cooling loop sealed.", true);
                return null;
            case ColdStorageControl.Kind.Flange:
                if (!Past) return FlangeRepaired ? null : "This pipe joint burst long ago.";
                if (FlangeRepaired) return null;
                if (!ServiceMode) return Refuse(1, "It's still under pressure.", "There's still pressure in the pipe.");
                if (!CanRepair) return null;
                FlangeRepaired = true;
                Play(sounds != null ? sounds.click : null);
                feedback.NotifyEnvironmentEvent("The leak's fixed. I still need to close the loop.", true);
                return null;
            default:
                if (Past) return "The plant is running normally in 2026.";
                if (!Ready) return Refuse(2, StartUpIssue(), StartUpIssue());
                StartCoroutine(StartUp()); return null;
        }
    }
    string Refuse(int kind, string first, string later) => refusals[kind]++ == 0 ? first : later;

    bool CanRepair => Past && ServiceMode && !FlangeRepaired && Held == null &&
        Mathf.Abs(player.transform.position.y - flange.position.y) < 1.5f;

    // Picking up a 2176 cell explains its history once per change of that history; carrying it never alters it.
    void ExplainOnPickup()
    {
        if (feedback == null) return;
        if (Past)
        {
            if (!handlingThought)
            {
                handlingThought = true;
                feedback.NotifyEnvironmentEvent("I need somewhere to keep this cold for 150 years.");
            }
            return;
        }
        for (int i = 0; i < 2; i++)
        {
            if (Held.gameObject != futureCells[i].gameObject || explained[i].HasValue && explained[i].Value.Equals(history[i])) continue;
            explained[i] = history[i];
            if (!history[i].usable && !recoveryThought)
            {
                recoveryThought = true;
                pendingThought = "It didn't survive the 150 years. I need to store it differently in the past.";
            }
            feedback.NotifyEnvironmentEvent("Cell " + (i == 0 ? "A" : "B") + " " + Explanation(history[i].reason), history[i].usable);
        }
    }
    public void ObserveEquipment(string equipment)
    {
        if (GameSettingsMenu.GameplayInputBlocked || Completed || Starting) return;
        pendingThought = equipment == "Reactor" ? "It's overheating. I need to get the cooling system working." :
            "It's cold in here, even without power. This could keep a cell chilled.";
    }

    static string Explanation(string reason) => reason switch
    {
        "SEALED IN THE LOOP" => "survived 150 years sealed in slot 1.",
        "PASSIVE COLD" => "survived in the sealed cold chamber.",
        "BAY VENTED" => "lost its coolant. Slot 1 was left open in 2026.",
        "LOOP LEAKED AT F3" => "lost its coolant through the leak at F3.",
        "HATCH LEFT OPEN" => "lost its coolant. The cold chamber was left open.",
        "UNSEALED SOCKET" => "lost its coolant. Slot 2 did not keep it sealed.",
        "LEFT ON BALANCE PAN" => "lost its coolant after 150 years on the balance pan.",
        "ACTIVE CHILL FAILED" => "lost its coolant when the vault's power failed.",
        _ => "lost its coolant after 150 years out in the open.",
    };

    // The usable cell resting in a socket, if any; present-day handling never changes a cell's condition.
    int UsableIn(PressureButton socket, Bounds site)
    {
        if (Past || !socket.IsPressed) return -1;
        for (int i = 0; i < 2; i++)
            if (history[i].usable && site.Contains(futureCells[i].transform.position) && Held?.gameObject != futureCells[i].gameObject) return i;
        return -1;
    }
    public bool Ready => !Past && !Completed && !Starting && LoopSealed && UsableIn(s1, s1Site) >= 0 && UsableIn(s2, s2Site) >= 0;
    public bool SocketUsable(int socket) => (socket == 1 ? UsableIn(s1, s1Site) : UsableIn(s2, s2Site)) >= 0;

    public string Hint(ColdStorageControl.Kind kind)
    {
        if (Completed || Starting) return "";
        switch (kind)
        {
            case ColdStorageControl.Kind.Valve:
                return !Past ? "The valve won't turn." : "E — Turn valve to " + (ServiceMode ? "LOOP" : "SERVICE");
            case ColdStorageControl.Kind.Flange:
                if (!Past) return FlangeRepaired ? "F3 intact" : "F3 burst";
                if (FlangeRepaired) return "F3 repaired";
                if (!ServiceMode) return "F3 · Under pressure";
                if (Held != null) return "Hands full";
                return CanRepair ? "E — Repair F3" : "F3 · Out of reach";
            default:
                if (Past) return "Plant running normally";
                return Ready ? "E — Start the exchanger" : "Not ready";
        }
    }

    string StartUpIssue() => !LoopSealed ? (ServiceMode ? "The cooling loop is still open." : "The cooling loop is leaking.") :
        !SocketUsable(1) ? "Slot 1 needs usable coolant." : "Slot 2 needs usable coolant.";

    public string ConsoleState() => !LoopSealed ? (ServiceMode ? "loop open" : "loop empty") :
        "S1 " + SocketState(1) + " · S2 " + SocketState(2);

    public string SocketState(int socket)
    {
        var button = socket == 1 ? s1 : s2; var site = socket == 1 ? s1Site : s2Site;
        if (!button.IsPressed) return "empty";
        if (Past) return "loaded";
        for (int i = 0; i < 2; i++)
            if (site.Contains(futureCells[i].transform.position))
                return (i == 0 ? "A" : "B") + (history[i].usable ? " usable" : " boiled off");
        return "empty";
    }

    public string DisplaySocketState(int socket)
    {
        string state = SocketState(socket);
        return state.Contains("boiled off") ? "No coolant" : state.Contains("usable") ? "Coolant supplied" : state == "loaded" ? "Cell inserted" : "No cell";
    }

    IEnumerator StartUp()
    {
        Starting = true; Completed = true;
        // Hold the shared input lock now; it passes to the completion screen on the last frame.
        GameSettingsMenu.StartupInputBlocked = true; holdingInput = true;
        interaction.DropHeldObject();
        Play(sounds != null ? sounds.complete : null);
        if (sounds != null && sounds.deviceHum != null) { hum.clip = sounds.deviceHum; hum.volume = 0; hum.Play(); }
        coolingLight.enabled = true;
        for (float t = 0; t < startUpSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / startUpSeconds);
            for (int i = 0; i < loopLamps.Length; i++) Lamp(loopLamps[i], k * loopLamps.Length > i);
            hum.volume = Mathf.Lerp(0, sounds != null ? sounds.humVolume : 0, k);
            reactorLabel.text = "REACTOR LOOP\n" + (k < .85f ? "COOLING · " + Mathf.RoundToInt(Mathf.Lerp(612, 298, k)) + " K" : "STABLE · 298 K");
            coolingLight.intensity = Mathf.Lerp(0, 2, k);
            yield return null;
        }
        Starting = false;
        ApplyTimeline(); UpdateFeedback();
        // Same frame: release, then the completion screen takes the shared lock before any input runs.
        ReleaseInput();
        completion.ShowCompletion();
    }

    void ReleaseInput()
    {
        if (!holdingInput) return;
        holdingInput = false; GameSettingsMenu.StartupInputBlocked = false;
    }
    void OnDisable() => ReleaseInput();

    public void ResetPlant()
    {
        if (GameSettingsMenu.GameplayInputBlocked || Starting) return;
        GuidanceResetVersion++;
        handlingThought = bothSurvivedThought = oneInstalledThought = false;
        pendingThought = null;
        recoveryThought = leakingSlotThought = sealedStorageThought = false;
        interaction.DropHeldObject();
        foreach (var button in new[] { s1, s2, pan }) button.ResetButton();
        for (int i = 0; i < 2; i++)
        {
            Place(pastCells[i], pastStart[i], pastRotation[i]); Place(futureCells[i], futureStart[i], futureRotation[i]);
            cells[i].RebaseAfterReset();
            explained[i] = null;
        }
        ServiceMode = FlangeRepaired = Completed = false;
        System.Array.Clear(refusals, 0, refusals.Length);
        hatch.SetActive(true); vaultGate.SetActive(true); hatchWasOpen = false;
        hatchVisual.position = hatchClosed; panVisual.position = panRest;
        foreach (var lamp in loopLamps) Lamp(lamp, false);
        coolingLight.enabled = false;
        feedback.Clear(); hud.Reannounce();
        GlobalState.get().gravity = GlobalState.StationGravityStrength;
        GlobalState.get().ResetTimelineForCheckpoint();
        player.ResetForCheckpoint(arrival);
        Camera.main.GetComponent<Look>().Rotate(new Vector2(90, 0));
        for (int i = 0; i < 2; i++) history[i] = Evaluate(i);
        ApplyConditions(); ApplyTimeline();
    }

    void Recover(Rigidbody cell, Vector3 position, Quaternion rotation)
    {
        if (Held != null && Held.gameObject == cell.gameObject) interaction.DropHeldObject();
        Place(cell, position, rotation);
    }

    static void Place(Rigidbody cell, Vector3 position, Quaternion rotation)
    {
        cell.position = position; cell.rotation = rotation;
        cell.transform.SetPositionAndRotation(position, rotation);
        if (!cell.isKinematic) cell.linearVelocity = cell.angularVelocity = Vector3.zero;
        cell.GetComponent<ArtificialGravity>().effective = true; cell.WakeUp();
        Physics.SyncTransforms();
    }

    // 2026 copies are always fresh; a 2176 copy's coolant window shows its own history.
    void ApplyConditions()
    {
        for (int i = 0; i < 2; i++) futureCoolant[i].enabled = history[i].usable;
    }

    void UpdateFeedback()
    {
        bool past = Past;
        Lamp(valveLamp, LoopSealed);
        flangeCrack.SetActive(past && !FlangeRepaired); flangeBurst.SetActive(!past && !FlangeRepaired); flangeIntact.SetActive(!past && FlangeRepaired);
        tankCoolant.SetActive(past || LoopSealed); s1Frost.SetActive(!past && LoopSealed);
        Lamp(consoleLamp, Completed || Ready);
        if (!Starting) reactorLabel.text = Completed ? "REACTOR STABLE" : past ? "COOLING SYSTEM ONLINE" : "REACTOR OVERHEATING";
        reactorLabel.color = Completed ? Satisfied : past ? Neutral : Warning;
        if (warningLight != null)
        {
            warningLight.enabled = !past && !Completed;
            if (warningLight.enabled) warningLight.intensity = 1.65f + .12f * Mathf.Sin(Time.time * .8f);
        }
        UpdateObjective(past);
        hud.Status.text = (past ? "2026 / PAST" : "2176 / FUTURE") + (player.GetComponent<ArtificialGravity>().IsGravityActive ? "  ·  GRAVITY ON" : "  ·  JETPACK");
        controls.text = Completed ? "" : "T — Switch time    ·    Q — Jetpack    ·    R — Reset plant    ·    ESC / P — Settings";
        var hit = interaction.fireInteractRay(true);
        var target = hit.Item1 ? hit.Item2.collider.GetComponentInParent<Interactable>() : null;
        hud.Prompt.text = Completed || Starting ? "" : Held != null ? "E — Place" : target is ColdStorageControl control ? control.Hint :
            target is MovableObject cell ? "E — Pick up cell " + (cell.name.Contains("Cell A") ? "A" : "B") : "";
    }

    // Main's objective pattern: a goal title with one status line, a short thought on the expanded card.
    void UpdateObjective(bool past)
    {
        if (Completed && !Starting) hud.SetObjective("Complete", "Station Stable", "Cooling restored.", "STATION STABLE\nCooling restored");
        else if (Starting) hud.SetObjective("Starting", "Exchanger Start-Up", "Coolant is reaching the reactor.", "EXCHANGER START-UP\nCoolant circulating");
        else if (Ready) hud.SetObjective("Start", "Start the Exchanger", "Everything is in place.",
            "START THE EXCHANGER\nBoth cooling slots supplied");
        else
        {
            string milestone = recoveryThought && !(CellUsable(0) && CellUsable(1)) ? "Preserve" : !FlangeRepaired ? "Cooling" : !(CellUsable(0) && CellUsable(1)) ? "Preserve" : "Supply";
            string title = milestone == "Cooling" ? "Restore Reactor Cooling" : milestone == "Preserve" ? "Preserve the Coolant Cells" : "Supply Both Cooling Slots";
            string detail = milestone == "Cooling" ? "The cooling system isn't working." : milestone == "Preserve" ? "Find a way to keep both cells usable until 2176." : "Both cells survived. The reactor still needs coolant.";
            string compact = "RESTORE REACTOR COOLING\n" + (past ? "Stored cells must last 150 years" : "Slot 1: " + DisplaySocketState(1) + " · Slot 2: " + DisplaySocketState(2));
            hud.SetObjective(milestone,title,detail,compact);
        }
    }

    void Lamp(Renderer lamp, bool ready)
    {
        if (lamp == null) return;
        var material = ready ? readyMaterial : lockedMaterial;
        if (lamp.sharedMaterial != material) lamp.sharedMaterial = material;
    }

    // 2026: a commissioned plant. 2176: dimmer, cooler light, every third fixture dead until the plant restarts.
    void ApplyTimeline()
    {
        bool lit = Past || Completed;
        foreach (var item in pastOnly) if (item != null) item.SetActive(Past);
        foreach (var item in futureOnly) if (item != null) item.SetActive(!Past);
        for (int i = 0; i < zoneLights.Length; i++)
        {
            zoneLights[i].intensity = lightIntensities[i] * (lit ? 1.15f : i % 3 == 0 ? .2f : .7f);
            zoneLights[i].color = Color.Lerp(lightColours[i], lit ? new Color(1, .95f, .85f) : new Color(.55f, .7f, .82f), .3f);
        }
        RenderSettings.ambientLight = lit ? new Color(.3f, .3f, .3f) : new Color(.16f, .19f, .23f);
    }
    void Play(AudioClip clip) { if (clip != null && events != null) events.PlayOneShot(clip, sounds.systemVolume); }
    void UpdateAmbience()
    {
        if (sounds == null) return;
        ambience.clip = Past ? sounds.past : sounds.future;
        ambience.volume = Past ? sounds.pastVolume : sounds.futureVolume;
        if (ambience.clip != null) ambience.Play();
    }
}
