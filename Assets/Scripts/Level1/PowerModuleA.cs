using Parallax.Settings;
using UnityEngine;

/// <summary>Past-only installation using the existing physical grab. The 380 kg module can only be handled while station
/// gravity is off; released inside the socket guide, its rails draw it in and square it up.</summary>
public sealed class PowerModuleA : MovableObject
{
    [SerializeField] Interact player;
    [SerializeField] TextMesh socketLabel;
    [SerializeField] GameObject alignmentMarker;
    [SerializeField] Vector3 installationPosition = new Vector3(-6, 1.05f, 3);
    [Tooltip("Release anywhere this close to the socket; a forgiving guide, not a precision target.")]
    [SerializeField, Min(.1f)] float guideRadius = .9f;
    [SerializeField, Range(0, 90)] float guideTilt = 45;
    [SerializeField, Min(.01f)] float seatDuration = .3f;
    Rigidbody moduleBody;
    Vector3 initialPosition, seatFrom;
    Quaternion initialRotation, seatFromRotation;
    float seatProgress = 1;
    bool wasPast;
    int timelineFailures, gravityFailures;
    readonly Collider[] overlaps = new Collider[16];
    // Both timelines share this object; the repair lasts until this scene is unloaded.
    public bool IsRepaired { get; private set; }
    bool Past => GlobalState.get().now == TimeState.Past;
    public static bool StationZeroG => GlobalState.get().gravity < .1f;

    // Times time travel sent a displaced, unfinished module back to its cradle; the HUD reports each one.
    public int Returns { get; private set; }

    public bool CanInstall => !GameSettingsMenu.GameplayInputBlocked && Past && !IsRepaired && player.HeldObject == this && InstallIssue == null;

    // Why a held module will not seat yet, in the order a player would fix it; null when it will.
    string InstallIssue
    {
        get
        {
            if (Vector3.Distance(transform.position, installationPosition) > guideRadius) return "Bring A to its socket · E — Release";
            if (Quaternion.Angle(transform.rotation, initialRotation) > guideTilt) return "Square A up to the socket · E — Release";
            // Keep the seated casing clear of the player or another solid object. Ignore its own compound colliders.
            int count = Physics.OverlapBoxNonAlloc(installationPosition, new Vector3(.59f, .615f, .44f),
                overlaps, initialRotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return "Socket blocked · E — Release";
            for (int i = 0; i < count; i++) if (overlaps[i].attachedRigidbody != moduleBody) return "Socket blocked · E — Release";
            return null;
        }
    }

    public string InteractionHint => IsRepaired ? "Module A installed" : !Past ? "Seized in its cradle in 2176" :
        player.HeldObject == this ? InstallIssue ?? "E — Install Module A" :
        !StationZeroG ? "Too heavy — station gravity ON" : "E — Pick up Module A";

    void Start()
    {
        moduleBody = GetComponent<Rigidbody>();
        initialPosition = transform.position; initialRotation = transform.rotation;
        wasPast = Past;
        moduleBody.isKinematic = !Past;
        RefreshFeedback();
    }

    void Update()
    {
        if (seatProgress < 1)
        {
            seatProgress = Mathf.Min(1, seatProgress + Time.deltaTime / seatDuration);
            float t = Mathf.SmoothStep(0, 1, seatProgress);
            var position = Vector3.Lerp(seatFrom, installationPosition, t); var rotation = Quaternion.Slerp(seatFromRotation, initialRotation, t);
            moduleBody.position = position; moduleBody.rotation = rotation; transform.SetPositionAndRotation(position, rotation);
        }
        // 380 kg cannot be held once station gravity returns.
        if (player.HeldObject == this && !StationZeroG) Drop();
        if (Past == wasPast) return;
        wasPast = Past;
        if (!IsRepaired)
        {
            // An unfinished carry still resets safely; an installed module stays seated and kinematic.
            if (Vector3.Distance(transform.position, initialPosition) > .3f) Returns++;
            Drop();
            if (!moduleBody.isKinematic) { moduleBody.linearVelocity = Vector3.zero; moduleBody.angularVelocity = Vector3.zero; }
            moduleBody.isKinematic = !Past;
            moduleBody.position = initialPosition; moduleBody.rotation = initialRotation;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }
        RefreshFeedback();
    }

    void RefreshFeedback()
    {
        alignmentMarker.SetActive(Past && !IsRepaired);
        socketLabel.text = IsRepaired ? "A / REPAIRED" : "A / INSTALLATION SLOT";
        socketLabel.color = IsRepaired ? new Color(.35f, 1f, .65f) : Color.white;
    }

    public override void onInteract(Interact owner)
    {
        if (GameSettingsMenu.GameplayInputBlocked || owner != player || IsRepaired) return;
        if (!Past)
        {
            if (PowerInteractionFeedback.Notify(owner, this, timelineFailures == 0 ?
                "Something's different about this module here..." : "Maybe I should try another timeline.")) timelineFailures++;
            return;
        }
        if (player.HeldObject == null && !StationZeroG)
        {
            if (PowerInteractionFeedback.Notify(owner, this, gravityFailures == 0 ?
                "I can't lift it while it's this heavy." : "There must be a way to make it easier to move.")) gravityFailures++;
        }
        if (CanInstall)
        {
            Drop();
            moduleBody.linearVelocity = Vector3.zero; moduleBody.angularVelocity = Vector3.zero;
            moduleBody.isKinematic = true;
            // The guide rails draw the casing in over a moment instead of teleporting it.
            seatFrom = transform.position; seatFromRotation = transform.rotation; seatProgress = 0;
            IsRepaired = true;
            RefreshFeedback();
        }
        else if (player.HeldObject == this || StationZeroG) base.onInteract(owner);
    }
}
