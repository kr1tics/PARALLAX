using UnityEngine;
using System;
using Parallax.Settings;

public class Interact : MonoBehaviour
{
    [SerializeField] public Transform rayOrigin;
    [SerializeField] public float reachDistance = 1.2f;
    [SerializeField] private LayerMask interactibleLayers;

	[SerializeField] private GameObject interactIndicator;

	private long lastFrame = -1;
	private Tuple<bool, RaycastHit> lastInteract = null;
	private Tuple<bool, RaycastHit> lastCast = null;

	public Tuple<bool, RaycastHit> fireInteractRay(bool useLayers) {
		if (lastFrame == Time.frameCount) {
			if (useLayers && lastInteract != null) {
				return lastInteract;
			} else if (lastCast != null) {
				return lastCast;
			}
		}
		RaycastHit hit;
		if (useLayers) {
			lastInteract = new(Physics.Raycast(rayOrigin.position, rayOrigin.localPosition + (rayOrigin.forward), out hit, reachDistance,
                interactibleLayers), hit);
		} else {
			lastCast = new(Physics.Raycast(rayOrigin.position, rayOrigin.localPosition + (rayOrigin.forward), out hit, reachDistance), hit);
		}
        return useLayers ? lastInteract : lastCast;
	}

	void Update() {
        interactIndicator.SetActive(!GameSettingsMenu.GameplayInputBlocked && fireInteractRay(true).Item1);
	}

    public void TryInteract()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        Debug.DrawRay(rayOrigin.position, (rayOrigin.localPosition + rayOrigin.forward).normalized * reachDistance, Color.red, 10.0f);
		var cast = fireInteractRay(true);
        if (cast.Item1)
        {
            cast.Item2.collider.gameObject.GetComponent<Interactable>().onInteract(this);
        }
    }
}
