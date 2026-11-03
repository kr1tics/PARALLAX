using UnityEngine;
using Parallax.Settings;

// Input and gravity remain owned by the player controller.
public sealed class FutureDecayControls : MonoBehaviour
{
    [SerializeField] private ArtificialGravity playerGravity;
    private GUIStyle title, label;
    public void Configure(ArtificialGravity gravity) { playerGravity = gravity; }

    private void OnGUI()
    {
        if (GameSettingsMenu.GameplayInputBlocked) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            title.normal.textColor = label.normal.textColor = new Color(0.92f, 0.94f, 0.95f);
        }
        float scale = Mathf.Min(1f, Mathf.Min(Screen.width / 370f, Screen.height / 340f));
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.Box(new Rect(16, 16, 330, 306), GUIContent.none);
        GUI.Label(new Rect(30, 28, 300, 30), GlobalState.get().now == TimeState.Future
            ? "FUTURE · 150 YEARS LATER" : "PAST · 2026", title);
        Row(68, "WASD / Mouse", "Move / look");
        Row(98, "T", "Switch time");
        Row(128, "E / Left click", "Interact / grab");
        Row(158, "Q", "Toggle jetpack");
        Row(188, "Space / Ctrl", "Jump / ascend / descend");
        Row(218, "G", "Distort object");
        Row(248, "Esc / P", "Settings / resume");
        string gravity = playerGravity == null ? "—" : playerGravity.IsGravityActive ? "ON" : "OFF";
        GUI.Label(new Rect(30, 284, 300, 26), "Gravity: " + gravity, label);
        GUI.matrix = previousMatrix;
    }

    private void Row(float y, string binding, string action)
    {
        GUI.Label(new Rect(30, y, 135, 26), binding, label);
        GUI.Label(new Rect(170, y, 170, 26), action, label);
    }
}
