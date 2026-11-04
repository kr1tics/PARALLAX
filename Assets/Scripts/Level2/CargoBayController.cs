using System.Linq;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Three stages on existing carry, plate, door and causality mechanics: open freight with C1, free C2 from the archive
/// rack with a Past counterweight, then load both Future manifests together. Only Level 2 owns this logic.</summary>
[DefaultExecutionOrder(-200)]
public sealed class CargoBayController : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Entangler cargo;
    [SerializeField] Rigidbody pastCrate, futureCrate;
    [SerializeField] PressureButton plate;
    [SerializeField] PressureButton stagingPlate;
    [SerializeField] Door freightDoor;
    [SerializeField] TMP_Text stagingLabel;
    [SerializeField] Renderer stagingIndicator;
    [SerializeField] Door exitDoor;
    [SerializeField] BoxCollider exitZone;
    [SerializeField] Vector3 arrival = new Vector3(0, .02f, -10);
    [Tooltip("Same fonts as the tutorial and Power Restoration HUD.")]
    [SerializeField] TMP_FontAsset titleFont, supportingFont;
    [SerializeField] TMP_Text doorLabel;
    [SerializeField] Renderer plateIndicator;
    [SerializeField] Material waitingMaterial, readyMaterial;
    [SerializeField] GameObject placementTarget;
    [SerializeField] ParallaxAudioPalette sounds;
    [SerializeField] Light[] zoneLights = { };
    [SerializeField] GameObject[] pastOnly = { }, futureOnly = { };
    [Header("Archive rack: C2 is clamped until Past C1 weights the counterweight pad; the 2176 clamp has seized.")]
    [SerializeField] Entangler rackCargo;
    [SerializeField] Rigidbody rackPast, rackFuture;
    [SerializeField] PressureButton counterweight;
    [SerializeField] Bounds cradle = new Bounds(new Vector3(-14.6f, 2.45f, 2.6f), new Vector3(1.3f, 1, 1.3f));
    [SerializeField] Renderer[] clampFeedback = { };
    [SerializeField] TMP_Text rackLabel;
    [Header("Receiving: R2 sits in the booth that has collapsed shut by 2176.")]
    [SerializeField] PressureButton rackPlate;
    [SerializeField] Renderer rackPlateIndicator;
    [SerializeField] GameObject futureDebris;
    static readonly Color Solved = new Color(.35f, 1f, .65f), Unsolved = new Color(.75f, .9f, 1);
    Color[] lightColours;
    float[] lightIntensities;
    public bool CargoDelivered { get; private set; }
    public bool FreightAccessGranted { get; private set; }
    public Door FreightGate => freightDoor;
    public bool StagingOccupied => stagingPlate.IsPressed || (GlobalState.get().now == TimeState.Past && PastCrateOnTarget(stagingPlate));
    public bool FreightCargoExamined => failedAttempts[0] > 0;
    public bool SecuredCargoExamined => failedAttempts[2] > 0;
    public int ReceivingStatus => receivingStatus;
    public bool C1Positioned => PastCrateOnTarget(plate);
    public bool C2Positioned => PastBodyOnTarget(rackPast, rackPlate);
    public int GuidanceResetVersion { get; private set; }
    public bool Completed { get; private set; }
    // C2 has left the clamp in 2026; its Future copy follows on the next switch to 2176.
    public bool RackReleased => !cradle.Contains(rackPast.transform.position);
    Vector3 pastStart, futureStart, rackPastStart, rackFutureStart;
    Quaternion pastRotation, futureRotation, rackPastRotation, rackFutureRotation;
    Interact interaction;
    TutorialObjectiveHud hud;
    PowerInteractionFeedback feedback;
    readonly int[] failedAttempts = new int[5];
    bool rackHandled, rackWasClamped;
    string displayedObjective;
    string idleProgress;
    float idleSeconds;
    int idleHintCount;
    int receivingStatus, receivingSeen;
    bool clampReleaseAnnounced, stagingWasRegistered;
    float transferConfirmedAt = -10;
    Bounds cageBounds;
    bool hasCageBounds;
    public bool ObjectiveExpanded => hud != null && hud.Expanded;
    public int ObjectiveRevision => hud != null ? hud.Revision : 0;
    TMP_Text controls;
    ArtificialGravity playerGravity;
    AudioSource events, ambience;
    TimeState previousTime;
    MovableObject previousHeld;

    void Awake()
    {
        GlobalState.ResetForNewGame();
        // The inactive Past body has not joined physics yet; its authored Transform is authoritative.
        pastStart = pastCrate.transform.position; futureStart = futureCrate.transform.position;
        pastRotation = pastCrate.transform.rotation; futureRotation = futureCrate.transform.rotation;
        rackPastStart = rackPast.transform.position; rackFutureStart = rackFuture.transform.position;
        rackPastRotation = rackPast.transform.rotation; rackFutureRotation = rackFuture.transform.rotation;
        interaction = player.GetComponent<Interact>();
    }

    void Start()
    {
        player.ResetForCheckpoint(arrival);
        Camera.main.GetComponent<Look>().Rotate(new Vector2(90, 0));
        previousTime = GlobalState.get().now;
        events = gameObject.AddComponent<AudioSource>(); events.playOnAwake = false;
        ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false;
        ambience.loop = true; ambience.ignoreListenerPause = true;
        UpdateAmbience();
        lightColours = zoneLights.Select(l => l.color).ToArray();
        lightIntensities = zoneLights.Select(l => l.intensity).ToArray();
        ApplyTimeline();
        foreach (var crate in new[] { pastCrate, futureCrate, rackPast, rackFuture })
            if (sounds != null) crate.gameObject.AddComponent<ParallaxDropAudio>().Configure(sounds, interaction);
        // Cargo Bay has no distortable objects (G hidden); R resets the bay, so its row stays in the controls list.
        var menu = GameSettingsMenu.Instance;
        if (menu != null) { menu.SetMainMenuContext(false); menu.SetGameplayControls(false, true); }
        hud = new TutorialObjectiveHud(transform, titleFont, supportingFont, "Cargo Bay Objective UI", 5f, true, true, new Color32(181, 160, 255, 255));
        feedback = gameObject.AddComponent<PowerInteractionFeedback>();
        feedback.Initialize(hud.Instruction.GetComponentInParent<Canvas>().transform, supportingFont);
        rackWasClamped = rackPast.isKinematic;
        foreach (var surface in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(IsCage))
        {
            if (!hasCageBounds) { cageBounds = surface.bounds; hasCageBounds = true; }
            else cageBounds.Encapsulate(surface.bounds);
        }
        controls = GameplayHud.CreateControlHint(hud.Instruction.GetComponentInParent<Canvas>().transform, supportingFont);
        playerGravity = player.GetComponent<ArtificialGravity>();
    }

    void Update()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        Vector3 p = player.transform.position;
        if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) || Lost(p))
        { ResetCurrentPuzzle(); return; }
        // A lost crate returns alone to its start, so the rest of the bay keeps its progress; a Past crate's
        // Future copy follows on the next switch to 2176, exactly as for any other Past move.
        if (Lost(pastCrate.transform.position)) Recover(pastCrate, pastStart, pastRotation);
        if (Lost(futureCrate.transform.position)) Recover(futureCrate, futureStart, futureRotation);
        if (Lost(rackPast.transform.position)) Recover(rackPast, rackPastStart, rackPastRotation);
        if (Lost(rackFuture.transform.position)) Recover(rackFuture, rackFutureStart, rackFutureRotation);
    }

    static bool Lost(Vector3 p) => p.y < -6 || p.y > 9 || Mathf.Abs(p.x) > 21 || Mathf.Abs(p.z) > 21;

    void Recover(Rigidbody crate, Vector3 position, Quaternion rotation)
    {
        if (interaction.HeldObject == crate.GetComponent<MovableObject>()) interaction.DropHeldObject();
        Restore(crate, position, rotation); Physics.SyncTransforms();
    }

    // Kinematic is the existing "cannot be carried" state; only a crate resting in the cradle is ever clamped.
    void FixedUpdate()
    {
        var held = interaction.HeldObject;
        Clamp(rackPast, !counterweight.IsPressed && held != rackPast.GetComponent<MovableObject>() && cradle.Contains(rackPast.transform.position));
        Clamp(rackFuture, held != rackFuture.GetComponent<MovableObject>() && cradle.Contains(rackFuture.transform.position));
    }

    static void Clamp(Rigidbody crate, bool clamped)
    {
        if (crate.isKinematic == clamped) return;
        if (clamped) crate.linearVelocity = crate.angularVelocity = Vector3.zero;
        crate.isKinematic = clamped;
        if (!clamped) crate.WakeUp();
    }

    void LateUpdate()
    {
        hud.SetVisible(Completed || !GameSettingsMenu.GameplayInputBlocked);
        if (GameSettingsMenu.GameplayInputBlocked) return;
        bool past = GlobalState.get().now == TimeState.Past;
        if (previousTime != GlobalState.get().now)
        {
            previousTime = GlobalState.get().now;
            feedback.Clear();
            Play(sounds != null ? sounds.timeSwitch : null); UpdateAmbience(); ApplyTimeline();
        }
        bool registered = PastCrateOnTarget(stagingPlate);
        if (past && registered && !stagingWasRegistered && !FreightAccessGranted)
        {
            feedback.NotifyEnvironmentEvent("Cargo registered.", true);
            Play(sounds != null ? sounds.drop : null);
        }
        stagingWasRegistered = registered;
        ObserveFailedInteractions(past);
        hud.Tick(Time.deltaTime);
        bool anchored = PastCrateOnTarget(stagingPlate);
        if (!FreightAccessGranted && !past && stagingPlate.IsPressed && anchored)
        {
            FreightAccessGranted = true;
            feedback.Clear();
            freightDoor.onStartTrigger();
            feedback.NotifyEnvironmentEvent("Freight gate unlocked.", true);
            Play(sounds != null ? sounds.click : null);
        }
        bool c1 = plate.IsPressed, c2 = rackPlate.IsPressed;
        if (!past)
        {
            receivingStatus = (c1 ? 1 : 0) | (c2 ? 2 : 0);
            if (FreightAccessGranted)
            {
                if (receivingSeen == 0 && (receivingStatus == 1 || receivingStatus == 2))
                    feedback.NotifyEnvironmentEvent("One down, one to go.", true);
                receivingSeen |= receivingStatus;
            }
        }
        // Both weight scales read only in 2176, at the same time; the door then latches.
        if (!CargoDelivered && FreightAccessGranted && !past && c1 && c2)
        {
            CargoDelivered = true;
            transferConfirmedAt = Time.time;
            feedback.Clear();
            feedback.NotifyEnvironmentEvent("The way forward is open.", true);
            exitDoor.onStartTrigger();
            Play(sounds != null ? sounds.click : null);
        }
        if (!Completed && CargoDelivered && !past && exitZone.bounds.Contains(player.transform.position))
        {
            Completed = true;
            Play(sounds != null ? sounds.complete : null);
            GetComponent<TutorialCompletionScreen>()?.ShowCompletion();
        }
        if (previousHeld != interaction.HeldObject)
        {
            if (interaction.HeldObject != null) { feedback.Clear(); Play(sounds != null ? sounds.pickup : null); }
            else if (previousHeld != null) previousHeld.GetComponent<ParallaxDropAudio>()?.Arm();
            previousHeld = interaction.HeldObject;
        }
        bool clampOpen = past && (counterweight.IsPressed || RackReleased);
        if (!clampReleaseAnnounced && past && counterweight.IsPressed && !RackReleased)
        {
            clampReleaseAnnounced = true;
            feedback.NotifyEnvironmentEvent("Looks like that worked.", true);
        }
        UpdateObjective(past, c1, c2);
        TickIdleHints(Time.deltaTime);
        hud.Status.text = (past ? "2026 / PAST" : "2176 / FUTURE") + (playerGravity.IsGravityActive ? "  ·  GRAVITY ON" : "  ·  GRAVITY OFF");
        controls.text = Completed ? "" : "CTRL — Descend in Zero-G    ·    R — Reset cargo";
        var hit = interaction.fireInteractRay(true);
        var aimed = hit.Item1 ? hit.Item2.collider.GetComponentInParent<MovableObject>() : null;
        hud.Prompt.text = Completed ? "" : interaction.HeldObject != null ? "E — Place" : aimed == null ? "" :
            aimed.GetComponent<Rigidbody>().isKinematic ? (aimed.GetComponent<Rigidbody>() == rackPast || aimed.GetComponent<Rigidbody>() == rackFuture ? "C2 · Secured" : "") : "E — Pick up";
        doorLabel.text = CargoDelivered ? "MAINTENANCE ACCESS / OPEN" : "MAINTENANCE LIFT\nAWAITING C1 + C2";
        plateIndicator.sharedMaterial = CargoDelivered || (!past && c1) ? readyMaterial : waitingMaterial;
        rackPlateIndicator.sharedMaterial = CargoDelivered || (!past && c2) ? readyMaterial : waitingMaterial;
        stagingIndicator.sharedMaterial = FreightAccessGranted ? readyMaterial : waitingMaterial;
        stagingLabel.text = "A / FREIGHT RELEASE\n" + (FreightAccessGranted ? "ACCESS GRANTED" : "AWAITING C1");
        foreach (var item in clampFeedback) item.sharedMaterial = clampOpen ? readyMaterial : waitingMaterial;
        rackLabel.text = "C2 / RACK CLAMP\n" + (!past ? (cradle.Contains(rackFuture.transform.position) ? "SEIZED" : "EMPTY") :
            RackReleased ? "EMPTY" : counterweight.IsPressed ? "OPEN" : "LOCKED");
        // Power Restoration's socket labels turn this green once their condition is met.
        stagingLabel.color = FreightAccessGranted ? Solved : Unsolved;
        rackLabel.color = clampOpen ? Solved : Unsolved;
        doorLabel.color = CargoDelivered ? Solved : Unsolved;
        placementTarget.SetActive(FreightAccessGranted && !CargoDelivered);
    }

    void UpdateObjective(bool past, bool c1, bool c2)
    {
        string stage = Completed ? "Complete" : CargoDelivered && Time.time - transferConfirmedAt >= 1.6f ? "Exit" : CargoDelivered ? "Transfer" : (!FreightAccessGranted || (GetComponent<CargoBaySignage>() is CargoBaySignage signs && !signs.FreightGatePassed)) ? "Freight" : !RackReleased ? "Release" : "Transfer";
        string title = stage == "Complete" ? "Cargo Bay Complete" : stage == "Exit" ? "Reach Maintenance Access" :
            stage == "Freight" ? "Restore Freight Access" : stage == "Release" ? "Release the Secured Cargo" : "Complete the Cargo Transfer";
        string status = stage == "Freight" ? (FreightAccessGranted ? "Access restored" : "Freight access: locked") : stage == "Release" ?
            (past && counterweight.IsPressed ? "C2: clamp released" : "C2: secured") :
            stage == "Transfer" ? CargoDelivered ? "Cargo transfer: complete" : "C1: " + (past ? C1Positioned ? "positioned" : "pending" : (receivingStatus & 1) != 0 ? "received" : "pending") +
                " · C2: " + (past ? C2Positioned ? "positioned" : "pending" : (receivingStatus & 2) != 0 ? "received" : "pending") : "Cargo transfer: complete";
        string display = stage + "|" + status;
        if (display == displayedObjective) return;
        bool enteringTransfer = stage == "Transfer" && (displayedObjective == null || !displayedObjective.StartsWith(stage + "|"));
        displayedObjective = display;
        hud.SetObjective(stage, title, status, title.ToUpperInvariant() + "\n" + status);
        if (enteringTransfer && string.IsNullOrEmpty(feedback.Message))
            feedback.NotifyEnvironmentEvent("Both cargo units need to be delivered.");
    }

    void TickIdleHints(float elapsed)
    {
        if (GameSettingsMenu.GameplayInputBlocked || Completed) return;
        string progress = FreightAccessGranted + "/" + RackReleased + "/" + counterweight.IsPressed + "/" +
            receivingStatus + "/" + CargoDelivered + "/" + (interaction.HeldObject != null ? interaction.HeldObject.name : "");
        if (progress != idleProgress)
        {
            idleProgress = progress; idleSeconds = 0; idleHintCount = 0;
            return;
        }
        if (!FreightAccessGranted && StagingOccupied) return;
        idleSeconds += elapsed;
        if (idleHintCount >= 2 || idleSeconds < (idleHintCount + 1) * 45f || !string.IsNullOrEmpty(feedback.Message)) return;
        bool later = idleHintCount > 0;
        string hint = CargoDelivered ? "The maintenance entrance is open now." :
            !FreightAccessGranted ? interaction.HeldObject != null ?
                (later ? "That staging platform looks like it registers cargo." : "There should be somewhere to stage this cargo.") :
                (later ? "Maybe that cargo wasn't always sealed away." : "That sealed cargo unit might be worth a closer look.") :
            GetComponent<CargoBaySignage>() is CargoBaySignage signs && !signs.FreightGatePassed ? "The freight gate is open. I can look beyond it." :
            !RackReleased ? counterweight.IsPressed ? "The clamp is open. I should check the cargo." :
                (later ? "That mechanism seems connected to the cargo rack." : "Something is keeping the upper cargo secured.") :
            later ? "The receiving stations still need both cargo units." : "I should check which receiving station is still waiting.";
        if (feedback.NotifyEnvironmentEvent(hint)) idleHintCount++;
    }

    bool IsCage(Collider surface) => surface.GetComponentsInParent<Transform>().Any(item => item.name.StartsWith("Sealed freight cage"));

    void Hint(int condition, string first, string later, Collider surface = null)
    {
        string message = failedAttempts[condition] == 0 ? first : later;
        bool shown = surface != null ? feedback.NotifySurfaceAttempt(interaction, surface, message) : feedback.NotifyEnvironmentEvent(message);
        if (shown) failedAttempts[condition]++;
    }

    void ObserveFailedInteractions(bool past)
    {
        bool holdingRack = interaction.HeldObject == rackPast.GetComponent<MovableObject>();
        rackHandled |= holdingRack;
        if (past && rackHandled && !rackWasClamped && rackPast.isKinematic && cradle.Contains(rackPast.transform.position))
            Hint(3, "Not quite free yet.", "I need to move it clear of the rack.");
        rackWasClamped = rackPast.isKinematic;
        bool attempted = player.enabled && Keyboard.current != null && Mouse.current != null &&
            (Keyboard.current.eKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame);
        if (!attempted || interaction.HeldObject != null || previousHeld != null) return;
        var cast = interaction.fireInteractRay(false);
        if (!cast.Item1) return;
        var surface = cast.Item2.collider;
        var body = surface.attachedRigidbody;
        if (!past && hasCageBounds && cageBounds.Contains(futureCrate.transform.position) && IsCage(surface))
            Hint(0, "This cargo won't budge...", "Maybe things were different in the past.", surface);
        else if (!FreightAccessGranted && surface.transform.IsChildOf(freightDoor.transform))
            Hint(1, "Something must be keeping this gate locked.", "There must be something controlling it.", surface);
        else if ((body == rackPast || body == rackFuture) && interaction.fireInteractRay(true).Item1 && body.isKinematic && cradle.Contains(body.transform.position))
            Hint(2, "Something's holding C2 in place.", "I wonder what releases the clamp.", surface);
        else if (!past && surface.transform.IsChildOf(futureDebris.transform))
            Hint(4, "Looks like this section has collapsed.", "I wonder what this area looked like before...", surface);
    }

    bool PastCrateOnTarget(PressureButton targetPlate) => PastBodyOnTarget(pastCrate, targetPlate);

    bool PastBodyOnTarget(Rigidbody body, PressureButton targetPlate)
    {
        Bounds target = targetPlate.GetComponent<Collider>().bounds;
        Vector3 p = body.transform.position;
        return interaction.HeldObject != body.GetComponent<MovableObject>() && Mathf.Abs(p.x - target.center.x) < target.extents.x &&
            Mathf.Abs(p.z - target.center.z) < target.extents.z && p.y > target.max.y && p.y < target.max.y + 1.2f;
    }

    public void ResetCurrentPuzzle()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        GuidanceResetVersion++;
        idleProgress = null; idleSeconds = 0; idleHintCount = 0;
        interaction.DropHeldObject();
        System.Array.Clear(failedAttempts, 0, failedAttempts.Length); rackHandled = false;
        rackWasClamped = true; stagingWasRegistered = false; previousHeld = null; displayedObjective = null;
        receivingStatus = receivingSeen = 0; clampReleaseAnnounced = false; transferConfirmedAt = -10;
        feedback.Clear(); hud.Reannounce();
        plate.ResetButton();
        stagingPlate.ResetButton();
        counterweight.ResetButton(); rackPlate.ResetButton();
        Restore(pastCrate, pastStart, pastRotation); Restore(futureCrate, futureStart, futureRotation);
        Restore(rackPast, rackPastStart, rackPastRotation); Restore(rackFuture, rackFutureStart, rackFutureRotation);
        cargo.RebaseAfterReset(); rackCargo.RebaseAfterReset();
        CargoDelivered = Completed = FreightAccessGranted = false;
        exitDoor.onEndTrigger();
        freightDoor.onEndTrigger();
        GlobalState.get().gravity = GlobalState.StationGravityStrength;
        GlobalState.get().ResetTimelineForCheckpoint();
        player.ResetForCheckpoint(arrival);
        Camera.main.GetComponent<Look>().Rotate(new Vector2(90, 0));
        Physics.SyncTransforms();
    }

    static void Restore(Rigidbody crate, Vector3 position, Quaternion rotation)
    {
        crate.position = position; crate.rotation = rotation;
        crate.transform.SetPositionAndRotation(position, rotation);
        if (!crate.isKinematic) crate.linearVelocity = crate.angularVelocity = Vector3.zero;
        crate.GetComponent<ArtificialGravity>().effective = true; crate.WakeUp();
    }
    // Past: a freshly commissioned bay. Future: dimmer, cooler light and decayed set dressing.
    void ApplyTimeline()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        foreach (var item in pastOnly) item.SetActive(past);
        foreach (var item in futureOnly) item.SetActive(!past);
        futureDebris.SetActive(!past);
        for (int i = 0; i < zoneLights.Length; i++)
        {
            zoneLights[i].intensity = lightIntensities[i] * (past ? 1.15f : .7f);
            zoneLights[i].color = Color.Lerp(lightColours[i], past ? new Color(1, .93f, .8f) : new Color(.55f, .7f, .82f), .3f);
        }
        RenderSettings.ambientLight = past ? new Color(.3f, .29f, .27f) : new Color(.18f, .21f, .25f);
    }
    void Play(AudioClip clip) { if (clip != null) events.PlayOneShot(clip, sounds.systemVolume); }
    void UpdateAmbience()
    {
        if (sounds == null) return;
        bool past = GlobalState.get().now == TimeState.Past;
        ambience.clip = past ? sounds.past : sounds.future;
        ambience.volume = past ? sounds.pastVolume : sounds.futureVolume;
        if (ambience.clip != null) ambience.Play();
    }
}
