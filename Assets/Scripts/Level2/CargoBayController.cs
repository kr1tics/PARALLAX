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
    [SerializeField] CanvasGroup hud;
    [SerializeField] TMP_Text objective, status, prompt, doorLabel;
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
    Color[] lightColours;
    float[] lightIntensities;
    public bool CargoDelivered { get; private set; }
    public bool FreightAccessGranted { get; private set; }
    public bool Completed { get; private set; }
    // C2 has left the clamp in 2026; its Future copy follows on the next switch to 2176.
    public bool RackReleased => !cradle.Contains(rackPast.transform.position);
    Vector3 pastStart, futureStart, rackPastStart, rackFutureStart;
    Quaternion pastRotation, futureRotation, rackPastRotation, rackFutureRotation;
    Interact interaction;
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
    }

    void Update()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        Vector3 p = player.transform.position;
        if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            p.y < -6 || p.y > 9 || Mathf.Abs(p.x) > 21 || Mathf.Abs(p.z) > 21)
            ResetCurrentPuzzle();
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
        hud.alpha = GameSettingsMenu.GameplayInputBlocked ? 0 : 1;
        if (GameSettingsMenu.GameplayInputBlocked) return;
        bool past = GlobalState.get().now == TimeState.Past;
        bool anchored = PastCrateOnTarget(stagingPlate);
        if (!FreightAccessGranted && !past && stagingPlate.IsPressed && anchored)
        {
            FreightAccessGranted = true;
            freightDoor.onStartTrigger();
            Play(sounds != null ? sounds.click : null);
        }
        bool c1 = plate.IsPressed, c2 = rackPlate.IsPressed;
        // Both weight scales read only in 2176, at the same time; the door then latches.
        if (!CargoDelivered && FreightAccessGranted && !past && c1 && c2)
        {
            CargoDelivered = true;
            exitDoor.onStartTrigger();
            Play(sounds != null ? sounds.click : null);
        }
        if (!Completed && CargoDelivered && !past && exitZone.bounds.Contains(player.transform.position))
        {
            Completed = true;
            Play(sounds != null ? sounds.complete : null);
            GetComponent<TutorialCompletionScreen>()?.ShowCompletion();
        }
        if (previousTime != GlobalState.get().now)
        {
            previousTime = GlobalState.get().now;
            Play(sounds != null ? sounds.timeSwitch : null); UpdateAmbience(); ApplyTimeline();
        }
        if (previousHeld != interaction.HeldObject)
        {
            if (interaction.HeldObject != null) Play(sounds != null ? sounds.pickup : null);
            else if (previousHeld != null) previousHeld.GetComponent<ParallaxDropAudio>()?.Arm();
            previousHeld = interaction.HeldObject;
        }
        bool clampOpen = past && (counterweight.IsPressed || RackReleased);
        objective.text = Completed ? "CARGO BAY COMPLETE\nMaintenance access restored." :
            CargoDelivered ? "REACH MAINTENANCE ACCESS\n" + (past ? "Return to the Future to leave." : "Follow the green marker through the exit.") :
            !FreightAccessGranted ? "01 / LOADING BAY\nOpen freight access. " + (past ? "In 2026, C1 is not caged." : "Manifest C1 is caged in 2176.") :
            !RackReleased ? "02 / ARCHIVE RACK\nFree manifest C2 from the mezzanine rack. " + (!past ? "Its 2176 clamp has seized." :
                counterweight.IsPressed ? "The clamp is open." : "The clamp is locked; its counterweight pad is empty.") :
            "03 / RECEIVING\nLoad C1 and C2 at the same time. " + (past ? "The scales only read in 2176." :
                "R1 " + (c1 ? "LOADED" : "EMPTY") + "   R2 " + (c2 ? "LOADED" : "EMPTY"));
        status.text = "02 / CARGO BAY     " + (past ? "2026 / PAST" : "2176 / FUTURE");
        var hit = interaction.fireInteractRay(true);
        var aimed = hit.Item1 ? hit.Item2.collider.GetComponentInParent<MovableObject>() : null;
        prompt.text = interaction.HeldObject != null ? "E / CLICK  —  RELEASE CARGO" : aimed == null ? "" :
            aimed.GetComponent<Rigidbody>().isKinematic ? "CARGO CLAMPED" : "E / CLICK  —  PICK UP CARGO";
        doorLabel.text = CargoDelivered ? "MAINTENANCE ACCESS / OPEN" : "MAINTENANCE LIFT\nAWAITING C1 + C2";
        plateIndicator.sharedMaterial = CargoDelivered || (!past && c1) ? readyMaterial : waitingMaterial;
        rackPlateIndicator.sharedMaterial = CargoDelivered || (!past && c2) ? readyMaterial : waitingMaterial;
        stagingIndicator.sharedMaterial = FreightAccessGranted ? readyMaterial : waitingMaterial;
        stagingLabel.text = "A / FREIGHT RELEASE\n" + (FreightAccessGranted ? "ACCESS GRANTED" : "AWAITING C1");
        foreach (var item in clampFeedback) item.sharedMaterial = clampOpen ? readyMaterial : waitingMaterial;
        rackLabel.text = "C2 / RACK CLAMP\n" + (!past ? (cradle.Contains(rackFuture.transform.position) ? "SEIZED" : "EMPTY") :
            RackReleased ? "EMPTY" : counterweight.IsPressed ? "OPEN" : "LOCKED");
        placementTarget.SetActive(FreightAccessGranted && !CargoDelivered);
    }

    bool PastCrateOnTarget(PressureButton targetPlate)
    {
        Bounds target = targetPlate.GetComponent<Collider>().bounds;
        Vector3 p = pastCrate.transform.position;
        return interaction.HeldObject != pastCrate.GetComponent<MovableObject>() && Mathf.Abs(p.x - target.center.x) < target.extents.x &&
            Mathf.Abs(p.z - target.center.z) < target.extents.z && p.y > target.max.y && p.y < target.max.y + 1.2f;
    }

    public void ResetCurrentPuzzle()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        interaction.DropHeldObject();
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
