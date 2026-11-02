using System.Linq;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Stage cargo in the Past, then deliver its Future counterpart using existing carry, plate and door mechanics.</summary>
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
    Color[] lightColours;
    float[] lightIntensities;
    public bool CargoDelivered { get; private set; }
    public bool FreightAccessGranted { get; private set; }
    public bool Completed { get; private set; }
    Vector3 pastStart, futureStart;
    Quaternion pastRotation, futureRotation;
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
        foreach (var crate in new[] { pastCrate, futureCrate })
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
        if (!CargoDelivered && FreightAccessGranted && anchored && !past && plate.IsPressed)
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
        objective.text = Completed ? "CARGO BAY COMPLETE\nMaintenance access restored." :
            CargoDelivered ? "REACH MAINTENANCE ACCESS\n" + (past ? "Return to the Future to leave." : "Follow the green marker through the exit.") :
            !FreightAccessGranted ? "01 / STAGE THE CARGO\n" + (past ?
                anchored ? "Crate staged at A. Return to the Future." : "Move the Past crate onto staging pad A." :
                "The cargo is sealed here. Visit the Past to stage it at A.") :
            "02 / KEEP THE PAST, CHANGE THE FUTURE\n" + (!anchored ? "Restore the Past crate to pad A." :
                past ? "Leave this crate at A. Carry its Future copy to B." :
                "Carry the Future crate through freight access to B.");
        status.text = "02 / CARGO BAY     " + (past ? "2026 / PAST" : "2176 / FUTURE");
        var hit = interaction.fireInteractRay(true);
        prompt.text = interaction.HeldObject != null ? "E / CLICK  —  RELEASE CARGO" :
            hit.Item1 && hit.Item2.collider.GetComponentInParent<MovableObject>() != null ? "E / CLICK  —  PICK UP CARGO" : "";
        doorLabel.text = CargoDelivered ? "MAINTENANCE ACCESS / OPEN" : "EXIT LOCKED\nDELIVER CARGO IN THE FUTURE";
        plateIndicator.sharedMaterial = CargoDelivered ? readyMaterial : waitingMaterial;
        stagingIndicator.sharedMaterial = FreightAccessGranted && anchored ? readyMaterial : waitingMaterial;
        stagingLabel.text = "A / ARCHIVE STAGING\n" + (anchored ? "PAST POSITION PRESERVED" : "LEAVE THE PAST CRATE HERE");
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
        Restore(pastCrate, pastStart, pastRotation); Restore(futureCrate, futureStart, futureRotation);
        cargo.RebaseAfterReset();
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
        crate.linearVelocity = crate.angularVelocity = Vector3.zero;
        crate.GetComponent<ArtificialGravity>().effective = true; crate.WakeUp();
    }
    // Past: a freshly commissioned bay. Future: dimmer, cooler light and decayed set dressing.
    void ApplyTimeline()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        foreach (var item in pastOnly) item.SetActive(past);
        foreach (var item in futureOnly) item.SetActive(!past);
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
