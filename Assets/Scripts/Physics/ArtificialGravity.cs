using UnityEngine;

public class ArtificialGravity : MonoBehaviour
{
    [SerializeField]
    private Rigidbody rb = null;

    public bool effective;
    
    void Setup()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }
    void FixedUpdate()
    {
        if (!effective) return;
        
        rb.AddForce(Vector3.down * GlobalState.get().gravity);
    }
}
