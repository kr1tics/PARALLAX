using UnityEngine;

public class Look : MonoBehaviour
{

    [SerializeField] private Transform pivot;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private Vector2 lookAngle = Vector2.zero;
    
    public Vector2 currentAngles()
    {
        return lookAngle;
    }
    
    public void Rotate(Vector2 angles)
    {
        // Move the camera to the appropriate x
        // Basically, we are 0.25m away from the camera pivot, so we get a new unit vector, then 
        transform.localPosition = new Vector3(Mathf.Cos(angles.x * Mathf.Deg2Rad), 0, Mathf.Sin(angles.x * Mathf.Deg2Rad)) * 0.25f;

        // Look in the angle away from the pivot
        transform.rotation = Quaternion.LookRotation(
            transform.localPosition + (Vector3.up * angles.y)
        );

        lookAngle = angles;
    }
}
