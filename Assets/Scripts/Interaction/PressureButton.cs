using System.Collections.Generic;
using UnityEngine;

public class PressureButton : MonoBehaviour
{
    [SerializeField] private Trigger[] triggers;
    [SerializeField] private float gravityRequirement = 4.5f;
    [SerializeField] private Rigidbody requiredBody;
    private readonly HashSet<Collider> contacts = new();
    private bool held = false;
    public bool IsPressed => held;

    void OnCollisionEnter(Collision collision) => Track(collision);
    void OnCollisionStay(Collision collision) => Track(collision);

    void Track(Collision collision)
    {
        var body = collision.rigidbody;
        if (body == null || body.GetComponent<MovableObject>() == null ||
            (requiredBody != null && body != requiredBody)) return;
        float top = GetComponent<Collider>().bounds.max.y;
        bool supported = false;
        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).point.y >= top - 0.03f && body.worldCenterOfMass.y > top)
                supported = true;
        if (supported) contacts.Add(collision.collider);
        else contacts.Remove(collision.collider);
        Evaluate();
    }

    void FixedUpdate() => Evaluate();

    void Evaluate()
    {
        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        bool pressed = false;
        if (GlobalState.get().gravity > gravityRequirement)
        {
            foreach (var contact in contacts)
            {
                var body = contact.attachedRigidbody;
                if (body == null) continue;
                var gravity = body.GetComponent<ArtificialGravity>();
                // A carried cube has gravity disabled and must not count as a weight.
                if (gravity != null ? gravity.IsGravityActive : body.useGravity) pressed = true;
            }
        }
        SetPressed(pressed);
    }

    void SetPressed(bool value)
    {
        if (held == value) return;
        held = value;
        if (triggers == null) return;
        foreach (var trigger in triggers)
        {
            if (trigger == null) continue;
            if (held) trigger.onStartTrigger();
            else trigger.onEndTrigger();
        }
    }

    void OnCollisionExit(Collision collision)
    {
        contacts.Remove(collision.collider);
        Evaluate();
    }

    public void ResetButton() { contacts.Clear(); SetPressed(false); }
    void OnDisable() => ResetButton();
}
