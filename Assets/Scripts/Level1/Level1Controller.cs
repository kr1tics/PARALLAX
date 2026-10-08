using Parallax.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Wires the existing time, gravity, pressure plate and door mechanics into Level 1.</summary>
[DefaultExecutionOrder(-200)]
public sealed class Level1Controller : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Door introDoor, storageDoor, exitDoor;
    [SerializeField] private StorageInterlock storageInterlock;
    [SerializeField] private Switch gravitySwitch;
    [SerializeField] private PressureButton introPlate, mainPlate;
    [SerializeField] private Entangler introEntangler, mainEntangler;
    [SerializeField] private Rigidbody[] introCubes, mainCubes;
    [SerializeField] private Vector3 arrival = new Vector3(3f, 0.02f, 9f);
    [SerializeField] private Vector3 engineeringCheckpoint = new Vector3(-1.5f, 0.02f, 10f);
    public bool IntroSolved { get; private set; }
    public bool StorageReleased => storageInterlock.IsOpen;
    public bool StorageAccessGranted { get; private set; }
    public bool MainSolved { get; private set; }
    public bool Completed { get; private set; }
    private Vector3[] introPositions, mainPositions;
    private Quaternion[] introRotations, mainRotations;
    private GUIStyle titleStyle, bodyStyle;

    void Awake()
    {
        // Also handles loading StartScene directly in the Editor or reloading gameplay.
        GlobalState.ResetForNewGame();
        Capture(introCubes, out introPositions, out introRotations);
        Capture(mainCubes, out mainPositions, out mainRotations);
    }

    void Start()
    {
        player.transform.position = arrival;
        Camera.main.GetComponent<Look>().Rotate(new Vector2(90f, 0f));
    }

    void Update()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetCurrentPuzzle();
        // Recover a fall through geometry; ordinary open-space physics remains unchanged.
        Vector3 p = player.transform.position;
        if (p.y < -8)
            ResetCurrentPuzzle();
    }

    void LateUpdate()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        bool zeroGravity = GlobalState.get().gravity < 0.1f;
        if (IntroSolved && GlobalState.get().now == TimeState.Past && zeroGravity)
            StorageAccessGranted = true;
        // The console works in the past; its global gravity setting persists into the future.
        storageInterlock.Refresh(StorageAccessGranted && zeroGravity);
    }

    public void PowerRelay(int stage)
    {
        if (GlobalState.get().now != TimeState.Future || GlobalState.get().gravity < 4.5f) return;
        if (stage == 1 && introPlate.IsPressed)
        {
            IntroSolved = true;
            introDoor.onStartTrigger();
        }
        if (stage == 2 && IntroSolved && StorageAccessGranted && mainPlate.IsPressed)
        {
            MainSolved = true;
            exitDoor.onStartTrigger();
        }
    }

    public void TryComplete()
    {
        if (IntroSolved && MainSolved && GlobalState.get().now == TimeState.Future) Completed = true;
    }

    public void ResetCurrentPuzzle()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        bool restartAll = !IntroSolved || Completed;
        player.GetComponent<Interact>().DropHeldObject();
        if (restartAll)
        {
            IntroSolved = false;
            introPlate.ResetButton();
            Restore(introCubes, introPositions, introRotations);
            introEntangler.RebaseAfterReset();
            introDoor.onEndTrigger();
        }
        mainPlate.ResetButton();
        Restore(mainCubes, mainPositions, mainRotations);
        mainEntangler.RebaseAfterReset();
        MainSolved = StorageAccessGranted = Completed = false;
        exitDoor.onEndTrigger();
        gravitySwitch.ResetSwitch();
        GlobalState.get().gravity = GlobalState.StationGravityStrength;
        GlobalState.get().ResetTimelineForCheckpoint();
        player.ResetForCheckpoint(restartAll ? arrival : engineeringCheckpoint);
        Camera.main.GetComponent<Look>().Rotate(new Vector2(restartAll ? 90f : 180f, 0f));
        Physics.SyncTransforms();
        storageInterlock.ResetClosed();
    }

    static void Capture(Rigidbody[] bodies, out Vector3[] positions, out Quaternion[] rotations)
    {
        positions = new Vector3[bodies.Length]; rotations = new Quaternion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) { positions[i] = bodies[i].position; rotations[i] = bodies[i].rotation; }
    }

    static void Restore(Rigidbody[] bodies, Vector3[] positions, Quaternion[] rotations)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].position = positions[i]; bodies[i].rotation = rotations[i];
            bodies[i].transform.SetPositionAndRotation(positions[i], rotations[i]);
            bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero;
            bodies[i].GetComponent<ArtificialGravity>().effective = true;
            bodies[i].WakeUp();
        }
    }

    void OnGUI()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            titleStyle.normal.textColor = new Color(0.60f, 0.91f, 0.94f);
            bodyStyle.normal.textColor = new Color(0.90f, 0.94f, 0.98f);
        }
        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 540f);
        Matrix4x4 previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        string objective = !IntroSolved ? "01 / Restore the access relay" : !StorageAccessGranted ? "02 / Release the engineering cube" : !MainSolved ? "02 / Restore the engineering relay" : "03 / Reach the exit in the future";
        string hint = !IntroSolved ? "Move the cube onto the marked plate in the PAST. Switch to the FUTURE to restore access." :
            !StorageAccessGranted ? "In the PAST, use the gravity switch to turn station gravity OFF. The storage interlock will release." :
            !MainSolved ? "Take the cube OUT of storage to plate 02. Restore gravity, then return to the FUTURE. Gravity ON locks storage." :
            "Follow the green route through the west door to the exit marker.";
        GUI.Box(new Rect(16, 16, 460, 151), GUIContent.none);
        GUI.Label(new Rect(30, 24, 430, 30), Completed ? "LEVEL 1 COMPLETE" : objective, titleStyle);
        GUI.Label(new Rect(30, 57, 425, 65), Completed ? "Access restored. Engineering relay stable.\nYou have reached the end of Level 1." : hint, bodyStyle);
        GUI.Label(new Rect(30, 124, 430, 30), GlobalState.get().now.ToString().ToUpperInvariant() +
            (GlobalState.get().gravity > 0 ? "  /  STATION GRAVITY ON" : "  /  STATION GRAVITY OFF"), bodyStyle);
        GUI.Label(new Rect(20, Screen.height / scale - 38, 900, 30),
            "F / T  Switch time     E / Click  Interact     R  " + (Completed ? "Replay level" : "Reset checkpoint") + "     Esc / P  Settings & controls", bodyStyle);
        GUI.matrix = previous;
    }
}
