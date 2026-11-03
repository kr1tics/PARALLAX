using System.Collections;
using System.Linq;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Cold Storage, the final level. Two time-linked coolant cells (existing Entangler pairs): each 2176 copy's
/// condition is the record of where its own 2026 copy spent 150 years. Storage, valve, flange, balance pan and hatch
/// are scene state; carrying, seating and time travel stay with the shared systems.</summary>
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
    [SerializeField] TMP_Text[] pastTags = { }, futureTags = { };
    [SerializeField] Renderer[] futureCoolant = new Renderer[2];
    [Header("Storage and mechanisms")]
    [SerializeField] PressureButton s1, s2, pan;
    [SerializeField] GameObject vaultGate, hatch;
    [SerializeField] Bounds s1Site = new Bounds(new Vector3(0, .9f, 5), new Vector3(1.3f, 1.2f, 1.3f));
    [SerializeField] Bounds s2Site = new Bounds(new Vector3(5, 7.8f, 5.8f), new Vector3(1.3f, 1.2f, 1.3f));
    [SerializeField] Bounds panSite = new Bounds(new Vector3(2.6f, .8f, 3), new Vector3(1.3f, 1.2f, 1.3f));
    [SerializeField] Bounds rSite = new Bounds(new Vector3(9, 9.9f, 6), new Vector3(1.5f, 1.3f, 1.5f));
    [SerializeField] Bounds hatchOpening = new Bounds(new Vector3(7.9f, 9.9f, 6), new Vector3(.9f, 1.5f, 1.6f));
    [SerializeField] Bounds vaultSite = new Bounds(new Vector3(-6, 2.5f, 6.2f), new Vector3(4.6f, 5, 5.6f));
    [SerializeField] Bounds vaultDoorway = new Bounds(new Vector3(-6, 2.3f, 3), new Vector3(4.6f, 4.6f, 1.4f));
    [SerializeField] Transform flange;
    [Header("Feedback")]
    [SerializeField] TMP_Text valveLabel, flangeLabel, s1Label, s2Label, rLabel, panLabel, vaultLabel, consoleLabel, reactorLabel;
    [SerializeField] Renderer valveLamp, flangeLamp, s1Lamp, s2Lamp, rLamp, consoleLamp;
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
    readonly Outcome[] history = new Outcome[2];
    Vector3[] pastStart, futureStart;
    Quaternion[] pastRotation, futureRotation;
    Interact interaction;
    GameplayHud hud;
    TMP_Text controls;
    AudioSource events, ambience, hum;
    TimeState previousTime;
    MovableObject previousHeld;
    Color[] lightColours;
    float[] lightIntensities;
    bool holdingInput;

    public bool ServiceMode { get; private set; }
    public bool FlangeRepaired { get; private set; }
    public bool Starting { get; private set; }
    public bool Completed { get; private set; }
    public bool HatchOpen => !hatch.activeSelf;
    public bool VaultOpen => !vaultGate.activeSelf;
    public bool LoopSealed => FlangeRepaired && !ServiceMode;
    public bool CellUsable(int cell) => history[cell].usable;
    public string CellReason(int cell) => history[cell].reason;
    public Site CellSite(int cell) => history[cell].site;
    static bool Past => GlobalState.get().now == TimeState.Past;
    MovableObject Held => interaction.HeldObject;

    void Awake()
    {
        GlobalState.ResetForNewGame();
        interaction = player.GetComponent<Interact>();
        // The inactive copies have not joined physics yet; their authored Transforms are authoritative.
        pastStart = pastCells.Select(c => c.transform.position).ToArray(); pastRotation = pastCells.Select(c => c.transform.rotation).ToArray();
        futureStart = futureCells.Select(c => c.transform.position).ToArray(); futureRotation = futureCells.Select(c => c.transform.rotation).ToArray();
        for (int i = 0; i < 2; i++) history[i] = Evaluate(i);
    }

    void Start()
    {
        player.ResetForCheckpoint(arrival);
        Camera.main.GetComponent<Look>().Rotate(new Vector2(90, 0));
        previousTime = GlobalState.get().now;
        events = gameObject.AddComponent<AudioSource>(); events.playOnAwake = false;
        ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false; ambience.loop = true; ambience.ignoreListenerPause = true;
        hum = gameObject.AddComponent<AudioSource>(); hum.playOnAwake = false; hum.loop = true; hum.ignoreListenerPause = true;
        lightColours = zoneLights.Select(l => l.color).ToArray(); lightIntensities = zoneLights.Select(l => l.intensity).ToArray();
        foreach (var cell in pastCells.Concat(futureCells))
            if (sounds != null) cell.gameObject.AddComponent<ParallaxDropAudio>().Configure(sounds, interaction);
        var menu = GameSettingsMenu.Instance;
        // Cold Storage has no distortable objects (G hidden); R resets the plant, so its row stays.
        if (menu != null) { menu.SetMainMenuContext(false); menu.SetGameplayControls(false, true); }
        hud = new GameplayHud(transform, "Cold Storage Objective UI", titleFont, supportingFont);
        controls = GameplayHud.CreateControlHint(hud.Instruction.GetComponentInParent<Canvas>().transform, supportingFont);
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
        UpdateMechanisms();
        if (previousHeld != Held)
        {
            if (Held != null) Play(sounds != null ? sounds.pickup : null);
            else if (previousHeld != null) previousHeld.GetComponent<ParallaxDropAudio>()?.Arm();
            previousHeld = Held;
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
        if (!Past) { vaultGate.SetActive(false); return; }
        bool vaultHeld = vaultSite.Contains(player.transform.position) || (VaultOpen &&
            (vaultDoorway.Intersects(playerBounds) || active.Any(c => vaultDoorway.Intersects(c.GetComponent<Collider>().bounds))));
        vaultGate.SetActive(!(ServiceMode || vaultHeld));
    }

    public void Operate(ColdStorageControl.Kind kind)
    {
        if (GameSettingsMenu.GameplayInputBlocked || Starting || Completed) return;
        switch (kind)
        {
            case ColdStorageControl.Kind.Valve:
                if (!Past) { Play(sounds != null ? sounds.drop : null); return; }
                ServiceMode = !ServiceMode; Play(sounds != null ? sounds.click : null); break;
            case ColdStorageControl.Kind.Flange:
                if (!CanRepair) return;
                FlangeRepaired = true; Play(sounds != null ? sounds.click : null); break;
            case ColdStorageControl.Kind.Console:
                if (Ready) StartCoroutine(StartUp()); break;
        }
    }

    bool CanRepair => Past && ServiceMode && !FlangeRepaired && Held == null &&
        Mathf.Abs(player.transform.position.y - flange.position.y) < 1.5f;

    // The usable cell resting in a socket, if any; present-day handling never changes a cell's condition.
    int UsableIn(PressureButton socket, Bounds site)
    {
        if (Past || !socket.IsPressed) return -1;
        for (int i = 0; i < 2; i++)
            if (history[i].usable && site.Contains(futureCells[i].transform.position) && Held?.gameObject != futureCells[i].gameObject) return i;
        return -1;
    }
    public bool Ready => !Past && !Completed && !Starting && LoopSealed && UsableIn(s1, s1Site) >= 0 && UsableIn(s2, s2Site) >= 0;

    public string Hint(ColdStorageControl.Kind kind)
    {
        if (Completed || Starting) return "";
        switch (kind)
        {
            case ColdStorageControl.Kind.Valve:
                return !Past ? "Valve seized — iced and rusted" : "E — Turn valve to " + (ServiceMode ? "LOOP" : "SERVICE");
            case ColdStorageControl.Kind.Flange:
                if (!Past) return FlangeRepaired ? "F3 intact" : "F3 burst — beyond repair";
                if (FlangeRepaired) return "F3 repaired";
                if (!ServiceMode) return "Line pressurised — vent the loop first";
                if (Held != null) return "Hands full";
                return CanRepair ? "E — Repair flange F3" : "Float level with the flange";
            default:
                if (Past) return "Plant running normally in 2026";
                return Ready ? "E — Start the exchanger" : ConsoleState();
        }
    }

    string ConsoleState() => !LoopSealed ? (ServiceMode ? "Loop open — valve on SERVICE" : "Loop empty — leaked at F3") :
        "S1 " + SocketState(s1, s1Site) + " · S2 " + SocketState(s2, s2Site);

    string SocketState(PressureButton socket, Bounds site)
    {
        if (!socket.IsPressed) return "empty";
        if (Past) return "loaded";
        for (int i = 0; i < 2; i++)
            if (site.Contains(futureCells[i].transform.position))
                return (i == 0 ? "A" : "B") + (history[i].usable ? " usable" : " boiled off");
        return "empty";
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
        interaction.DropHeldObject();
        foreach (var button in new[] { s1, s2, pan }) button.ResetButton();
        for (int i = 0; i < 2; i++)
        {
            Place(pastCells[i], pastStart[i], pastRotation[i]); Place(futureCells[i], futureStart[i], futureRotation[i]);
            cells[i].RebaseAfterReset();
        }
        ServiceMode = FlangeRepaired = Completed = false;
        hatch.SetActive(true); vaultGate.SetActive(true);
        foreach (var lamp in loopLamps) Lamp(lamp, false);
        coolingLight.enabled = false;
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

    // 2026 copies are always fresh; a 2176 copy shows its own history.
    void ApplyConditions()
    {
        for (int i = 0; i < 2; i++)
        {
            string name = "CELL " + (i == 0 ? "A" : "B");
            futureCoolant[i].enabled = history[i].usable;
            foreach (var tag in futureTags.Where(t => t != null && t.transform.IsChildOf(futureCells[i].transform)))
            {
                tag.text = name + "\n" + (history[i].usable ? "USABLE" : "BOILED OFF") + "\n" + history[i].reason;
                tag.color = history[i].usable ? Satisfied : Warning;
            }
            foreach (var tag in pastTags.Where(t => t != null && t.transform.IsChildOf(pastCells[i].transform)))
            { tag.text = name + "\nUSABLE\n2026"; tag.color = Satisfied; }
        }
    }

    void UpdateFeedback()
    {
        bool past = Past;
        int s1Cell = UsableIn(s1, s1Site), s2Cell = UsableIn(s2, s2Site);
        Set(valveLabel, "PLANT VALVE\n" + (ServiceMode ? "SERVICE · LOOP VENTED · VAULT LIVE" : "LOOP · CLOSED") + (past ? "" : "\nSEIZED"), !ServiceMode);
        Lamp(valveLamp, !ServiceMode);
        Set(flangeLabel, "F3\n" + (past ? (FlangeRepaired ? "REPAIRED" : "HAIRLINE CRACK\nPRESSURE TEST FAILED") : FlangeRepaired ? "INTACT" : "BURST"), FlangeRepaired);
        Lamp(flangeLamp, FlangeRepaired);
        flangeCrack.SetActive(past && !FlangeRepaired); flangeBurst.SetActive(!past && !FlangeRepaired); flangeIntact.SetActive(!past && FlangeRepaired);
        tankCoolant.SetActive(past || LoopSealed); s1Frost.SetActive(!past && LoopSealed);
        Set(s1Label, "S1 CARTRIDGE BAY\nSEALS WITH THE PRIMARY LOOP\n" + (past ? (ServiceMode ? "VENTED" : "SEALED") : SocketState(s1, s1Site).ToUpper()), past ? !ServiceMode : s1Cell >= 0);
        Lamp(s1Lamp, past ? !ServiceMode : s1Cell >= 0);
        Set(s2Label, "S2 HOT SOCKET\nOPEN CRADLE · NOT SEALED\n" + SocketState(s2, s2Site).ToUpper(), s2Cell >= 0);
        Lamp(s2Lamp, s2Cell >= 0);
        bool panLoaded = pan.IsPressed, obstructed = HatchOpen && !panLoaded;
        Set(rLabel, "R RADIATOR SINK\nPASSIVE COLD · NO POWER\nHATCH SEALED WHEN PAN EMPTY\n" + (obstructed ? "HATCH HELD — OBSTRUCTED" : HatchOpen ? "HATCH OPEN" : "HATCH SEALED"), !HatchOpen);
        Lamp(rLamp, !HatchOpen);
        Set(panLabel, "BALANCE PAN · R HATCH\nLOAD ONE CARTRIDGE\n" + (panLoaded ? "LOADED · HATCH LIFTED" : "EMPTY"), false);
        Set(vaultLabel, "ACTIVE-CHILL VAULT\nREQUIRES PLANT POWER\n" + (!past ? "FAILED · DOOR BUCKLED" : VaultOpen ? (ServiceMode ? "OPEN" : "SAFETY HOLD") : "LOCKED"), false);
        Set(consoleLabel, "EXCHANGER\n" + (Completed ? "RUNNING" : past ? "PLANT RUNNING — NORMAL" : Ready ? "READY" : ConsoleState().ToUpper()), Completed || Ready);
        Lamp(consoleLamp, Completed || Ready);
        if (!Starting) reactorLabel.text = "REACTOR LOOP\n" + (Completed ? "STABLE · 298 K" : past ? "NOMINAL · 305 K" : "CRITICAL · 612 K");
        reactorLabel.color = Completed ? Satisfied : past ? Neutral : Warning;
        if (warningLight != null)
        {
            warningLight.enabled = !past && !Completed;
            if (warningLight.enabled) warningLight.intensity = 1 + .8f * Mathf.Sin(Time.time * 4);
        }
        UpdateObjective(past);
        hud.Status.text = (past ? "2026 / PAST" : "2176 / FUTURE") + (player.GetComponent<ArtificialGravity>().IsGravityActive ? "  ·  GRAVITY ON" : "  ·  JETPACK");
        controls.text = Completed ? "" : "ESC / P — Settings / Resume    ·    R — Reset plant    ·    Q — Jetpack";
        var hit = interaction.fireInteractRay(true);
        var target = hit.Item1 ? hit.Item2.collider.GetComponentInParent<Interactable>() : null;
        hud.Prompt.text = Completed || Starting ? "" : Held != null ? "E — Place" : target is ColdStorageControl control ? control.Hint :
            target is MovableObject ? "E — Pick up" : "";
    }

    // States, never the solution: what the plant needs, then what this timeline shows.
    void UpdateObjective(bool past)
    {
        if (Completed && !Starting) hud.SetObjective("COMPLETE", "Station Stable", "Power and cooling restored.");
        else if (Starting) hud.SetObjective("STARTING", "Exchanger Start-Up", "Coolant circulating to the reactor.");
        else if (Ready) hud.SetObjective("OBJECTIVE 02", "Start the Exchanger", "Both sockets hold usable cells.\nThe primary loop is sealed.");
        else hud.SetObjective("OBJECTIVE 01", "Restore Reactor Cooling",
            past ? "The plant runs normally in 2026.\nStored cells must last 150 years." :
                "Usable cells needed in S1 and S2.\n" + ConsoleState());
    }

    void Set(TMP_Text label, string text, bool satisfied)
    {
        if (label == null) return;
        if (label.text != text) label.text = text;
        label.color = satisfied ? Satisfied : Neutral;
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
