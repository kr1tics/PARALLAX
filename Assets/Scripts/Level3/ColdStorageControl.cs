using Parallax.Settings;

/// <summary>Plant valve, flange F3 and exchanger console. Uses the shared interaction ray, including range and occlusion.</summary>
public sealed class ColdStorageControl : Interactable
{
    public enum Kind { Valve, Flange, Console }
    [UnityEngine.SerializeField] ColdStorageController plant;
    [UnityEngine.SerializeField] Kind kind;
    public string Hint => plant.Hint(kind);
    public void Configure(ColdStorageController owner, Kind value) { plant = owner; kind = value; }

    public override void onInteract(Interact player)
    {
        if (player == null || GameSettingsMenu.GameplayInputBlocked || player.HeldObject != null) return;
        var hit = player.fireInteractRay(true);
        if (!hit.Item1 || hit.Item2.collider.GetComponentInParent<ColdStorageControl>() != this) return;
        // A refused action is explained once per press through the shared notice, as in the earlier levels.
        string refusal = plant.Operate(kind);
        if (refusal != null) PowerInteractionFeedback.Notify(player, this, refusal);
    }
}
