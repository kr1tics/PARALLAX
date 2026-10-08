using System;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float cameraSensitivity = 0.2f;
    
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

    [SerializeField] private Interact interactionController;
    
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
        if (GameSettingsMenu.GameplayInputBlocked)
        {
            movementIntent = Vector3.zero;
            lastIntent = Vector3.zero;
            jumpForce = 0f;
            return;
        }
        if (Keyboard.current == null || Mouse.current == null) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            Debug.Log("Toggling jetpack to " + (!isInJetpack).ToString());
            isInJetpack = !isInJetpack;
            grav.effective = !isInJetpack;
        }

        Look();
        
        DirectionIntent();
        
        
        // Temporary setup to allow for timetravel swap
        if (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.tKey.wasPressedThisFrame)
        {
            GlobalState.get().SwapTime();
        }
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            TryDistortObject();
        }

        if (Keyboard.current.eKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
        {
            interactionController.TryInteract();
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
        float sensitivity = cameraSensitivity * GameSettings.MouseSensitivity;
        float verticalDirection = GameSettings.InvertLook ? -1f : 1f;
        float newX = oldView.x + mouse.x * -sensitivity;

        float newY = Mathf.Clamp(oldView.y + mouse.y * sensitivity * 0.005f * verticalDirection, -0.5f, 0.5f);


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
            movementIntent += Vector3.down;
        }
    }
    
    void FixedUpdate()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
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