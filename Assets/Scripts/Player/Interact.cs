using System;
using Parallax.Settings;
using UnityEngine;

public class Interact : MonoBehaviour
{
    [SerializeField] public Transform rayOrigin;
    [SerializeField] public float reachDistance = 1.2f;
    [SerializeField] private LayerMask interactibleLayers;
    [SerializeField] private GameObject interactIndicator;
    [SerializeField] private bool automaticInteractionEffects = true;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    public MovableObject HeldObject { get; private set; }

    public Tuple<bool, RaycastHit> fireInteractRay(bool useLayers)
    {
        if (rayOrigin == null) return new Tuple<bool, RaycastHit>(false, default);
        int count = Physics.RaycastNonAlloc(rayOrigin.position, rayOrigin.forward, hits,
            reachDistance, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit closest = default;
        float distance = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            var hit = hits[index];
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (HeldObject != null && hit.collider.GetComponentInParent<MovableObject>() == HeldObject) continue;
            if (hit.distance < distance) { closest = hit; distance = hit.distance; }
        }
        if (closest.collider == null) return new Tuple<bool, RaycastHit>(false, default);
        if (!useLayers) return new Tuple<bool, RaycastHit>(true, closest);
        var target = closest.collider.GetComponentInParent<Interactable>();
        bool eligible = target != null && ((interactibleLayers.value &
            ((1 << closest.collider.gameObject.layer) | (1 << target.gameObject.layer))) != 0);
        // The nearest solid surface blocks interaction, even when it is not interactable.
        return new Tuple<bool, RaycastHit>(eligible, closest);
    }

    private void LateUpdate()
    {
        if (interactIndicator != null)
            interactIndicator.SetActive(!GameSettingsMenu.GameplayInputBlocked &&
                (HeldObject != null || fireInteractRay(true).Item1));
    }

    public void TryInteract()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        if (HeldObject != null)
        {
            HeldObject.onInteract(this);
            return;
        }
        var cast = fireInteractRay(true);
        if (cast.Item1)
        {
            var target = cast.Item2.collider.GetComponentInParent<Interactable>();
            target.onInteract(this);
            // Carrying controls the effect for movable objects; switches get a brief pulse.
            if (automaticInteractionEffects && target != null && !(target is MovableObject))
                InteractionDistortion.Play(target.gameObject);
        }
    }

    internal void Hold(MovableObject value)
    {
        HeldObject = value;
        if (automaticInteractionEffects) InteractionDistortion.BeginHolding(value.gameObject);
    }
    internal void Release(MovableObject value)
    {
        if (HeldObject != value) return;
        HeldObject = null;
        if (automaticInteractionEffects) InteractionDistortion.EndHolding(value.gameObject);
    }
    public void DropHeldObject() { if (HeldObject != null) HeldObject.Drop(); }
    private void OnDisable() => DropHeldObject();
}
