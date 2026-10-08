using System.Collections.Generic;
using Parallax.Settings;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovableObject : Interactable
{
    [SerializeField] private Rigidbody body;
    [SerializeField] private float springStrength = 45f;
    [SerializeField] private float damping = 12f;
    [SerializeField] private float maxAcceleration = 60f;
    private Interact carrying;
    private Vector3 localGrabPoint;
    private float grabDistance;
    private bool previousUseGravity;
    private ArtificialGravity artificialGravity;
    private bool previousEffective;
    private CollisionDetectionMode previousCollisionMode;
    private RigidbodyInterpolation previousInterpolation;
    private readonly List<(Collider item, Collider player, bool wasIgnored)> collisionPairs =
        new List<(Collider, Collider, bool)>();

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        artificialGravity = GetComponent<ArtificialGravity>();
    }

    public override void onInteract(Interact owner)
    {
        if (GameSettingsMenu.GameplayInputBlocked || body.isKinematic) return;
        if (carrying != null) { Drop(); return; }
        if (owner.HeldObject != null) return;
        var cast = owner.fireInteractRay(true);
        if (!cast.Item1 || cast.Item2.collider.GetComponentInParent<MovableObject>() != this) return;
        carrying = owner;
        localGrabPoint = transform.InverseTransformPoint(cast.Item2.point);
        grabDistance = Mathf.Clamp(cast.Item2.distance, 0.8f, owner.reachDistance);
        previousUseGravity = body.useGravity;
        body.useGravity = false;
        if (artificialGravity != null)
        {
            previousEffective = artificialGravity.effective;
            artificialGravity.effective = false;
        }
        previousCollisionMode = body.collisionDetectionMode;
        previousInterpolation = body.interpolation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        foreach (var item in GetComponentsInChildren<Collider>())
        foreach (var player in owner.GetComponentsInChildren<Collider>())
        {
            bool ignored = Physics.GetIgnoreCollision(item, player);
            collisionPairs.Add((item, player, ignored));
            Physics.IgnoreCollision(item, player, true);
        }
        owner.Hold(this);
    }

    private void FixedUpdate()
    {
        if (GameSettingsMenu.GameplayInputBlocked || carrying == null) return;
        if (carrying.rayOrigin == null) { Drop(); return; }
        Vector3 target = carrying.rayOrigin.position + carrying.rayOrigin.forward * grabDistance;
        Vector3 offset = target - transform.TransformPoint(localGrabPoint);
        if (offset.magnitude > carrying.reachDistance * 2f) { Drop(); return; }
        var carrierBody = carrying.GetComponent<Rigidbody>();
        Vector3 carrierVelocity = carrierBody != null ? carrierBody.linearVelocity : Vector3.zero;
        // A damped, bounded spring follows the grab point without applying tumbling impulses.
        Vector3 acceleration = offset * springStrength - (body.linearVelocity - carrierVelocity) * damping;
        body.AddForce(Vector3.ClampMagnitude(acceleration, maxAcceleration), ForceMode.Acceleration);
        body.AddTorque(-body.angularVelocity * damping, ForceMode.Acceleration);
    }

    public void Drop()
    {
        if (carrying == null) return;
        Interact owner = carrying;
        carrying = null;
        owner.Release(this);
        body.useGravity = previousUseGravity;
        if (artificialGravity != null) artificialGravity.effective = previousEffective;
        body.collisionDetectionMode = previousCollisionMode;
        body.interpolation = previousInterpolation;
        foreach (var pair in collisionPairs)
            if (pair.item != null && pair.player != null)
                Physics.IgnoreCollision(pair.item, pair.player, pair.wasIgnored);
        collisionPairs.Clear();
    }

    private void OnDisable() => Drop();
}
