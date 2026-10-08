using UnityEngine;

public sealed class Level1Exit : MonoBehaviour
{
    [SerializeField] private Level1Controller level;
    void OnTriggerEnter(Collider other) => Check(other);
    void OnTriggerStay(Collider other) => Check(other);
    void Check(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) level.TryComplete();
    }
}
