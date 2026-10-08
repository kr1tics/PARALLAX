using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArtificialGravity : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    public bool effective = true;

    public bool IsOnStation => rb != null && StationGravity.Contains(rb.worldCenterOfMass);
    public bool IsGravityActive => effective && IsOnStation && GlobalState.get().gravity > 0f;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        // Only the station's artificial gravity applies; open space is weightless.
        rb.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (IsGravityActive)
            rb.AddForce(Vector3.down * GlobalState.get().gravity, ForceMode.Acceleration);
    }
}
