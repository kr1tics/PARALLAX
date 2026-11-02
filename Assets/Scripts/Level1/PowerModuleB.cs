using Parallax.Settings;
using UnityEngine;

/// <summary>Past repair at the elevated junction, which only has power once module A is installed. Reaching it means
/// flying; movement and input remain player-owned.</summary>
public sealed class PowerModuleB : Interactable
{
    [SerializeField] Interact player;
    [SerializeField] PowerCircuit circuit;
    [SerializeField] TextMesh repairLabel, connectionLabel;
    public bool IsRepaired { get; private set; }
    bool Past => GlobalState.get().now == TimeState.Past;

    public bool CanRepair
    {
        get
        {
            if (GameSettingsMenu.GameplayInputBlocked || !Past || IsRepaired || !circuit.AFeed || player.HeldObject != null ||
                Mathf.Abs(player.transform.position.y - transform.position.y) > 1.5f) return false;
            // The shared ray supplies the actual range and nearest-solid-surface occlusion checks.
            var cast = player.fireInteractRay(true);
            return cast.Item1 && cast.Item2.collider.GetComponentInParent<PowerModuleB>() == this;
        }
    }

    public string InteractionHint => IsRepaired ? "Module B repaired" : !Past ? "F — Switch to Past" :
        !circuit.AFeed ? "No feed — module A offline" : CanRepair ? "E — Repair Module B" : "Float up beside the repair panel";

    void Awake() => RefreshFeedback();
    void LateUpdate() => RefreshFeedback();

    public override void onInteract(Interact owner)
    {
        if (owner != player || !CanRepair) return;
        IsRepaired = true;
        RefreshFeedback();
    }

    void RefreshFeedback()
    {
        bool fed = circuit.BFeed;
        string status = !IsRepaired ? (circuit.AFeed && Past ? "FEED LIVE" : "CONNECTION LOST") : fed ? "REPAIR COMPLETE" : "NO FEED";
        if (connectionLabel.text == status && repairLabel.text == (IsRepaired ? "B / REPAIRED" : "B / REPAIR")) return;
        repairLabel.text = IsRepaired ? "B / REPAIRED" : "B / REPAIR";
        connectionLabel.text = status;
        repairLabel.color = IsRepaired ? new Color(.35f, 1f, .65f) : Color.white;
        connectionLabel.color = IsRepaired && fed ? new Color(.35f, 1f, .65f) : Color.white;
    }
}
