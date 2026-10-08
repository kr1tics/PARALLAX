using UnityEngine;
using UnityEngine.InputSystem;
using Parallax.Settings;

public class MovableObject : Interactable
{
	private Interact carrying = null;
	private bool grabbedThisFrame = false;
	private Vector3 grabPoint = Vector3.zero;
	private float grabDistance = 0f;
	[SerializeField] private Rigidbody body;

    public override void onInteract(Interact i)
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        carrying = i;
        var ray = i.fireInteractRay(true).Item2;
		grabPoint = ray.point - transform.localPosition;
		grabDistance = ray.distance;
		grabbedThisFrame = true;
    }

    void Update()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
	    if (carrying != null)
	    {
		    var ray = carrying.fireInteractRay(true);
		    if (((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)) && !grabbedThisFrame)
		    {
			    carrying = null;
		    }

		    grabbedThisFrame = false;
	    }
    }

	void FixedUpdate() {
        if (GameSettingsMenu.GameplayInputBlocked) return;
		if (carrying != null) {
			Vector3 worldGrabPos = grabPoint + transform.localPosition;
			var interactRay = carrying.rayOrigin.position + (carrying.rayOrigin.localPosition + carrying.rayOrigin.forward) * grabDistance;
			Debug.DrawRay(Vector3.zero, interactRay, Color.yellow);
			Vector3 pull = interactRay - worldGrabPos;
			Debug.DrawRay(worldGrabPos, pull, Color.green);
			if (pull.magnitude > 0.5f)
			{
				body.AddForceAtPosition(pull.magnitude > 1.25f ? pull.normalized : pull, worldGrabPos, ForceMode.VelocityChange);
			}
			else
			{
				body.linearVelocity /= 4;
				body.angularVelocity /= 4;
			}
		}
	}
}
