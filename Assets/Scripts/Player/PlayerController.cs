using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float cameraSensitivity = 0.000025f;
    
    [SerializeField] private Rigidbody body = null;

    [SerializeField] private ArtificialGravity grav = null;

    [SerializeField] private Camera cam;
    private Look cameraLook = null;
    
    [SerializeField]  private Collider floor;

    [SerializeField] private float interactionDistance = 4f;

    private bool isInJetpack = false;

    private Vector3 movementIntent = new Vector3();
    [SerializeField] private Transform feet;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float speed = 7.5f;
    [SerializeField] private float jetpackDampening = 0.15f;
    private float jumpForce = 0f;
    private Vector3 lastIntent = new();
    
    void Start()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
        body.freezeRotation = true;
        
        
        if (grav == null)
        {
            grav = GetComponent<ArtificialGravity>();
            
            grav.effective = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        Cursor.lockState = CursorLockMode.Locked;
        if (Keyboard.current.qKey.wasReleasedThisFrame)
        {
            Debug.Log("Toggling jetpack to " + (!isInJetpack).ToString());
            isInJetpack = !isInJetpack;
            grav.effective = !isInJetpack;
        }

        Look();
        
        DirectionIntent();
        
        
        // Temporary setup to allow for timetravel swap
        if (Keyboard.current.tKey.wasReleasedThisFrame)
        {
            GlobalState.get().SwapTime();
        }
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryDistortObject();
        }
    }

    void Look()
    {
        if (cameraLook == null)
        {
            cameraLook = cam.GetComponent<Look>();
        }

        Vector2 oldView = cameraLook.currentAngles();
        Vector2 mouse = Mouse.current.delta.value;
        float newX = oldView.x + mouse.x * -cameraSensitivity;

        float newY = Mathf.Clamp(oldView.y + mouse.y * cameraSensitivity * 0.005f, -0.5f, 0.5f);


        cameraLook.Rotate(new Vector2(newX, newY));
    }

    void DirectionIntent()
    {
        Vector3 direction = cam.transform.localPosition.normalized;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
        movementIntent = new();
        
        if (Keyboard.current.wKey.isPressed)
        {
            movementIntent += direction;
        }
        if (Keyboard.current.aKey.isPressed)
        { 
            movementIntent += perpendicular;
        }
        if (Keyboard.current.sKey.isPressed)
        {
            movementIntent -= direction;
        }
        if (Keyboard.current.dKey.isPressed)
        { 
            movementIntent -= perpendicular;
        }

        if (Keyboard.current.spaceKey.isPressed)
        {
            movementIntent += Vector3.up;
        }
        
        // TODO: Make crouching happen if we 
        if (Keyboard.current.ctrlKey.isPressed)
        {
            movementIntent += Vector3.up;
        }
    }
    
    void FixedUpdate()
    {
        switch (isInJetpack)
        {
            case true:
                movePlayerJetpack();
                break;
            case false:
                movePlayerFeet();
                break;
        }
    }

    // The player has a jetpack so that they can move in zero-gravity.
    void movePlayerJetpack()
    {
        // If the player wants to move, let them.
        if (movementIntent.magnitude > 0.1f)
        {
            body.AddForce(movementIntent.normalized * speed);
        }
        // Otherwise the jetpack tries to stabilise the player in place.
        else
        {
            body.AddForce(body.linearVelocity * -jetpackDampening * speed);
        }
    }
    
    // The jetpack is off, the player feels gravity and must be touching the ground to walk.
    void movePlayerFeet()
    {
        RaycastHit hit; 
        bool groundCheck = Physics.Raycast(feet.position, Vector3.down, out hit, 0.5f, groundMask);
        if (!groundCheck)
        {
            jumpForce = 0f;
            return;
        }

        if (jumpForce < 0.1f && groundCheck)
        {
            lastIntent = movementIntent;
        }
        
        body.linearVelocity = (lastIntent - lastIntent.y * Vector3.up).normalized * speed;
        
        jumpForce = Mathf.Max((lastIntent.y * 10), jumpForce);
        body.linearVelocity += (jumpForce * Vector3.up);
    }
    private void TryDistortObject()
    {
        Ray ray = new Ray(
            cam.transform.position,
            cam.transform.forward
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance
        ))
        {
            DistortableObject distortable =
                hit.collider.GetComponentInParent<DistortableObject>();

            if (distortable != null)
            {
                distortable.ActivateDistortion();

                Debug.Log(
                    "Distorting: " + hit.collider.gameObject.name
                );
            }
        }
    }
}