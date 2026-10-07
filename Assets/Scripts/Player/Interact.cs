using UnityEngine;

public class Interact : MonoBehaviour
{
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float reachDistance = 1.2f;
    [SerializeField] private LayerMask interactibleLayers;

    public void TryInteract()
    {
        RaycastHit hit;
        Debug.DrawRay(rayOrigin.position, rayOrigin.localPosition * reachDistance, Color.red, 10.0f);
        if (Physics.Raycast(rayOrigin.position, rayOrigin.localPosition, out hit, reachDistance,
                interactibleLayers))
        {
            hit.collider.gameObject.GetComponent<Interactable>().onInteract();
        }
    }
}
