using Parallax.Settings;
using UnityEngine;

/// <summary>Past-only installation using the existing physical grab and personal Zero-G controls.</summary>
public sealed class PowerModuleA : MovableObject
{
    [SerializeField] Interact player;
    [SerializeField] PowerRestorationPresentation presentation;
    [SerializeField] TextMesh socketLabel;
    [SerializeField] GameObject alignmentMarker;
    [SerializeField] Vector3 alignmentPosition = new Vector3(-6, 3.05f, 3);
    [SerializeField] Vector3 installationPosition = new Vector3(-6, 1.05f, 3);
    [SerializeField, Min(.05f)] float alignmentTolerance = .35f;
    Rigidbody moduleBody;
    ArtificialGravity playerGravity;
    Vector3 initialPosition;
    Quaternion initialRotation;
    bool wasPast;
    string previousInstruction;
    readonly Collider[] overlaps = new Collider[16];
    public bool IsRepaired { get; private set; }
    bool Past => GlobalState.get().now == TimeState.Past;
    bool ZeroG => !playerGravity.effective;

    public bool CanInstall
    {
        get
        {
            if (GameSettingsMenu.GameplayInputBlocked || !Past || IsRepaired || !ZeroG || player.HeldObject != this ||
                player.transform.position.y < alignmentPosition.y - .5f ||
                Vector3.Distance(transform.position, alignmentPosition) > alignmentTolerance ||
                Quaternion.Angle(transform.rotation, initialRotation) > 15 ||
                Vector3.Distance(player.rayOrigin.position, transform.position) > player.reachDistance) return false;
            // Keep the seated casing clear of the player or another solid object. Ignore its own compound colliders.
            int count = Physics.OverlapBoxNonAlloc(installationPosition, new Vector3(.59f, .615f, .44f),
                overlaps, initialRotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (overlaps[i].attachedRigidbody != moduleBody) return false;
            return true;
        }
    }

    public string InteractionHint => IsRepaired ? "Module A repaired" : !Past ? "F — Switch to Past" :
        !ZeroG ? "Q — Activate Zero-G" : player.HeldObject == this ?
        (CanInstall ? "E — Install Module A" : "Align above socket · E — Release") : "E — Pick up Module A";

    void Start()
    {
        moduleBody = GetComponent<Rigidbody>();
        playerGravity = player.GetComponent<ArtificialGravity>();
        initialPosition = transform.position; initialRotation = transform.rotation;
        wasPast = Past;
        moduleBody.isKinematic = !Past;
    }

    void Update()
    {
        if (Past != wasPast)
        {
            // Stage 2 deliberately has no cross-timeline repair state. Stage 3 owns that integration.
            Drop();
            if (!moduleBody.isKinematic) { moduleBody.linearVelocity = Vector3.zero; moduleBody.angularVelocity = Vector3.zero; }
            moduleBody.isKinematic = !Past;
            moduleBody.position = initialPosition; moduleBody.rotation = initialRotation;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            IsRepaired = false; wasPast = Past;
        }
        alignmentMarker.SetActive(Past && !IsRepaired);
        socketLabel.text = IsRepaired ? "A / REPAIRED" : "A / INSTALLATION SLOT";
        socketLabel.color = IsRepaired ? new Color(.35f, 1f, .65f) : Color.white;
        string instruction = IsRepaired ? "Module A repaired.\nModule B repair is not available yet." :
            !Past ? "Switch to the Past to install Module A." : !ZeroG ? "Activate Zero-G with Q.\nPick up Module A with E." :
            "Carry Module A up to the marker.\nAlign, then press E to install.";
        if (instruction == previousInstruction) return;
        previousInstruction = instruction;
        presentation.SetObjective("OBJECTIVE 01", "Power Restoration", instruction);
    }

    public override void onInteract(Interact owner)
    {
        if (GameSettingsMenu.GameplayInputBlocked || owner != player || !Past || IsRepaired) return;
        if (CanInstall)
        {
            Drop();
            moduleBody.linearVelocity = Vector3.zero; moduleBody.angularVelocity = Vector3.zero;
            moduleBody.isKinematic = true;
            moduleBody.position = installationPosition; moduleBody.rotation = initialRotation;
            transform.SetPositionAndRotation(installationPosition, initialRotation);
            IsRepaired = true;
        }
        else if (player.HeldObject == this || ZeroG) base.onInteract(owner);
    }
}
