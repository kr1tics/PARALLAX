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
    
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private bool allowManualDistortion = true;

    private bool isInJetpack = false;

    private Vector3 movementIntent = new Vector3();
    [SerializeField] private Transform feet;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float speed = 7.5f;
    [SerializeField] private float jetpackDampening = 0.15f;
    [SerializeField] private float jumpSpeed = 6f;
    private bool jumpRequested;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    public void ResetForCheckpoint(Vector3 position)
    {
        interactionController.DropHeldObject();
        isInJetpack = false;
        grav.effective = true;
        movementIntent = Vector3.zero;
        jumpRequested = false;
        body.position = position;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    [SerializeField] private Interact interactionController;
    
    void Start()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        
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
            jumpRequested = false;
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
        jumpRequested |= Keyboard.current.spaceKey.wasPressedThisFrame;
        
        
        // Temporary setup to allow for timetravel swap
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            GlobalState.get().SwapTime();
        }
        if (allowManualDistortion && Keyboard.current.gKey.wasPressedThisFrame)
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
        Vector3 direction = cam.transform.forward;
        Vector3 perpendicular = -cam.transform.right;
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
        
        if (Keyboard.current.ctrlKey.isPressed)
        {
            movementIntent += Vector3.down;
        }
    }
    
    void FixedUpdate()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        switch (isInJetpack || !grav.IsGravityActive)
        {
            case true:
                movePlayerJetpack();
                jumpRequested = false;
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
    
    // With station gravity and the jetpack off, the player must be grounded to walk.
    void movePlayerFeet()
    {
        Vector3 origin = feet != null ? feet.position : body.position - Vector3.up * 0.85f;
        int count = Physics.RaycastNonAlloc(origin + Vector3.up * 0.25f, Vector3.down,
            groundHits, 0.65f, groundMask, QueryTriggerInteraction.Ignore);
        bool grounded = false;
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].rigidbody == body || groundHits[i].normal.y < 0.6f) continue;
            grounded = true;
            break;
        }
        if (grounded && body.linearVelocity.y <= 0.5f)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(movementIntent, Vector3.up);
            Vector3 velocity = horizontal.normalized * speed;
            velocity.y = jumpRequested ? jumpSpeed : body.linearVelocity.y;
            body.linearVelocity = velocity;
        }
        // A press gives one jump; holding Space does not repeatedly jump on landing.
        jumpRequested = false;
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
