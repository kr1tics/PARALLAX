using UnityEngine;
using Parallax.Settings;

// Scene-local presentation only. Movement, input, gravity and time travel stay in the existing systems.
[DefaultExecutionOrder(-100)]
public sealed class PowerRestorationPresentation : MonoBehaviour
{
    [SerializeField] private Look playerLook;
    [SerializeField] private ArtificialGravity playerGravity;
    [SerializeField] private Light[] roomLights;
    private TimeState? displayedTime;
    private GUIStyle heading, caption;

    private void Awake()
    {
        GlobalState.ResetForNewGame();
        playerLook.Rotate(new Vector2(90f, 0f));
    }

    private void LateUpdate()
    {
        var time = GlobalState.get().now;
        if (displayedTime == time) return;
        foreach (var light in roomLights)
            light.intensity = time == TimeState.Future ? 1.4f : 2.6f;
        displayedTime = time;
    }

    private void OnGUI()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        if (heading == null)
        {
            heading = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            caption = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            heading.normal.textColor = new Color(0.45f, 0.9f, 1f);
            caption.normal.textColor = new Color(0.9f, 0.94f, 0.96f);
        }
        var previous = GUI.matrix;
        float scale = Mathf.Min(1f, Screen.width / 1000f, Screen.height / 600f);
        GUI.matrix = previous * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float bottom = Screen.height / scale - 130;
        GUI.Box(new Rect(20, bottom, width - 40, 110), GUIContent.none);
        GUI.Label(new Rect(36, bottom + 8, width - 72, 28),
            "01 / POWER RESTORATION     •     " + GlobalState.get().now.ToString().ToUpperInvariant()
            + "     •     GRAVITY " + (playerGravity.IsGravityActive ? "ON" : "OFF"), heading);
        GUI.Label(new Rect(36, bottom + 37, width - 72, 25),
            "WASD  Move     Mouse  Look     Q  Jetpack / gravity     Space / Ctrl  Ascend / descend", caption);
        GUI.Label(new Rect(36, bottom + 65, width - 72, 25),
            "F / T  Switch time     E / Left click  Interact     G  Distortion", caption);
        GUI.Label(new Rect(24, 18, width - 48, 25), "REACTOR OFFLINE   /   Emergency lighting only", caption);
        GUI.Label(new Rect(width / 2 - 5, Screen.height / scale / 2 - 12, 20, 24), "+", caption);
        GUI.matrix = previous;
    }
}
