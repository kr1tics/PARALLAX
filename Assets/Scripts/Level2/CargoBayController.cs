using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One cargo transfer, using the existing entangler, carry physics, plate and door.</summary>
[DefaultExecutionOrder(-200)]
public sealed class CargoBayController : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Entangler cargo;
    [SerializeField] Rigidbody pastCrate, futureCrate;
    [SerializeField] PressureButton plate;
    [SerializeField] Door exitDoor;
    [SerializeField] BoxCollider exitZone;
    [SerializeField] Vector3 arrival = new Vector3(0, .02f, -10);
    [SerializeField] CanvasGroup hud;
    [SerializeField] TMP_Text objective, status, prompt, doorLabel;
    [SerializeField] Renderer plateIndicator;
    [SerializeField] Material waitingMaterial, readyMaterial;
    [SerializeField] GameObject placementTarget;
    [SerializeField] ParallaxAudioPalette sounds;
    public bool CargoDelivered { get; private set; }
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
        foreach (var crate in new[] { pastCrate, futureCrate })
            if (sounds != null) crate.gameObject.AddComponent<ParallaxDropAudio>().Configure(sounds, interaction);
    }

    void Update()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        Vector3 p = player.transform.position;
        if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            p.y < -6 || p.y > 9 || Mathf.Abs(p.x) > 15 || Mathf.Abs(p.z) > 15)
            ResetCurrentPuzzle();
    }

    void LateUpdate()
    {
        hud.alpha = GameSettingsMenu.GameplayInputBlocked ? 0 : 1;
        if (GameSettingsMenu.GameplayInputBlocked) return;
        bool past = GlobalState.get().now == TimeState.Past;
        if (!CargoDelivered && !past && plate.IsPressed)
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
            Play(sounds != null ? sounds.timeSwitch : null); UpdateAmbience();
        }
        if (previousHeld != interaction.HeldObject)
        {
            if (interaction.HeldObject != null) Play(sounds != null ? sounds.pickup : null);
            else if (previousHeld != null) previousHeld.GetComponent<ParallaxDropAudio>()?.Arm();
            previousHeld = interaction.HeldObject;
        }
        bool placed = PastCrateOnTarget();
        objective.text = Completed ? "CARGO BAY COMPLETE\nMaintenance access restored." :
            CargoDelivered ? "REACH MAINTENANCE ACCESS\n" + (past ? "Return to the Future to leave." : "Follow the green marker through the exit.") :
            !past ? "DELIVER THE TEMPORAL CARGO\nThe crate is sealed here. F / T: return to the Past." :
            placed ? "CARGO IN POSITION\nRelease the crate, then F / T: inspect the Future." :
            "DELIVER THE TEMPORAL CARGO\nCarry the crate around the freight stacks to the amber pad.";
        status.text = "02 / CARGO BAY     " + (past ? "2026 / PAST" : "2176 / FUTURE");
        var hit = interaction.fireInteractRay(true);
        prompt.text = interaction.HeldObject != null ? "E / CLICK  —  RELEASE CARGO" :
            hit.Item1 && hit.Item2.collider.GetComponentInParent<MovableObject>() != null ? "E / CLICK  —  PICK UP CARGO" : "";
        doorLabel.text = CargoDelivered ? "MAINTENANCE ACCESS / OPEN" : "EXIT LOCKED\nDELIVER CARGO IN THE FUTURE";
        plateIndicator.sharedMaterial = CargoDelivered ? readyMaterial : waitingMaterial;
        placementTarget.SetActive(!CargoDelivered && !placed);
    }

    bool PastCrateOnTarget()
    {
        Bounds target = plate.GetComponent<Collider>().bounds;
        Vector3 p = pastCrate.position;
        return interaction.HeldObject == null && Mathf.Abs(p.x - target.center.x) < target.extents.x &&
            Mathf.Abs(p.z - target.center.z) < target.extents.z && p.y > target.max.y && p.y < target.max.y + 1.2f;
    }

    public void ResetCurrentPuzzle()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        interaction.DropHeldObject();
        plate.ResetButton();
        Restore(pastCrate, pastStart, pastRotation); Restore(futureCrate, futureStart, futureRotation);
        cargo.RebaseAfterReset();
        CargoDelivered = Completed = false;
        exitDoor.onEndTrigger();
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
