using Parallax.Settings;
using UnityEngine;

/// <summary>Local Past repair at the existing elevated panel; movement and input remain player-owned.</summary>
public sealed class PowerModuleB : Interactable
{
    [SerializeField] Interact player;
    [SerializeField] TextMesh repairLabel, connectionLabel;
    ArtificialGravity playerGravity;
    public bool IsRepaired { get; private set; }

    public bool CanRepair
    {
        get
        {
            if (GameSettingsMenu.GameplayInputBlocked || GlobalState.get().now != TimeState.Past || IsRepaired ||
                playerGravity.effective || player.HeldObject != null ||
                Mathf.Abs(player.transform.position.y - transform.position.y) > 1.5f) return false;
            // The shared ray supplies the actual range and nearest-solid-surface occlusion checks.
            var cast = player.fireInteractRay(true);
            return cast.Item1 && cast.Item2.collider.GetComponentInParent<PowerModuleB>() == this;
        }
    }

    public string InteractionHint => IsRepaired ? "Module B repaired" : GlobalState.get().now != TimeState.Past ?
        "F — Switch to Past" : playerGravity.effective ? "Q — Activate Zero-G" :
        CanRepair ? "E — Repair Module B" : "Float up beside the repair panel";

    void Awake()
    {
        playerGravity = player.GetComponent<ArtificialGravity>();
        RefreshFeedback();
    }

    public override void onInteract(Interact owner)
    {
        if (owner != player || !CanRepair) return;
        IsRepaired = true;
        RefreshFeedback();
    }

    void RefreshFeedback()
    {
        repairLabel.text = IsRepaired ? "B / REPAIRED" : "B / REPAIR";
        connectionLabel.text = IsRepaired ? "REPAIR COMPLETE" : "CONNECTION LOST";
        repairLabel.color = connectionLabel.color = IsRepaired ? new Color(.35f, 1f, .65f) : Color.white;
    }
}
