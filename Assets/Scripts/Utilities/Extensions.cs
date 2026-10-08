using UnityEngine;

public static class Extensions
{
    public static (Vector3 position, float rotationAngle, Vector3 scale) delta(this Transform transform, Transform other)
    {
        return (transform.position - other.position, Quaternion.Angle(transform.rotation, other.rotation),
            transform.localScale - other.localScale);
    }
    
    public static void setTo(this Transform transform, Transform other)
    {
        transform.position = other.position;
        transform.rotation = other.rotation;
        transform.localScale = other.localScale;
    }
}
