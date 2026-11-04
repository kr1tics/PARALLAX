using Parallax.Settings;
using UnityEngine;

/// <summary>2026 feed selector at the foot of B's two wall routes. Seized by 2176, so the choice is made in the Past.</summary>
public sealed class PowerRouteSelector : Interactable
{
    [SerializeField] Interact player;
    [SerializeField] PowerCircuit circuit;
    [SerializeField] Transform lever;

    public string InteractionHint => GlobalState.get().now != TimeState.Past ? "Selector seized" :
        "E — Switch Power Route";

    public override void onInteract(Interact owner)
    {
        if (GameSettingsMenu.GameplayInputBlocked || owner != player || GlobalState.get().now != TimeState.Past) return;
        circuit.SelectRoute(circuit.Route == 1 ? 2 : 1);
    }

    void LateUpdate() => lever.localRotation = Quaternion.Euler(circuit.Route == 1 ? 35 : -35, 0, 0);
}
