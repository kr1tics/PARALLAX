using UnityEngine;

// Read-only HUD: input and gravity remain owned by the existing player controller.
public sealed class FutureDecayControls : MonoBehaviour
{
    [SerializeField] private ArtificialGravity playerGravity;
    private GUIStyle title, key, label;
    public void Configure(ArtificialGravity gravity) { playerGravity = gravity; }
    private void OnGUI()
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            key = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 28 };
            title.normal.textColor = key.normal.textColor = label.normal.textColor = new Color(0.92f,0.94f,0.95f);
        }
        // Large native font sizes; only shrink to fit genuinely small Game views.
        float scale = Mathf.Min(1f, Mathf.Min(Screen.width / 590f, Screen.height / 470f));
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = 550;
        GUI.Box(new Rect(20,20,width,430),GUIContent.none);
        GUI.Label(new Rect(40,36,width-40,44), GlobalState.get().now == TimeState.Future
            ? "FUTURE · 150 YEARS LATER" : "Past - 2026",title);
        GUI.Label(new Rect(85,94,40,40),"W",key);
        GUI.Label(new Rect(40,132,40,40),"A",key);
        GUI.Label(new Rect(85,132,40,40),"S",key);
        GUI.Label(new Rect(130,132,40,40),"D",key);
        GUI.Label(new Rect(185,113,365,40),"Move",label);
        Row(198,"Mouse","Look");
        Row(242,"T","Switch Time");
        Row(286,"Q","Toggle Jetpack");
        Row(330,"Space","Jump / Ascend");
        string gravity = playerGravity == null ? "—" : playerGravity.effective ? "ON" : "OFF";
        GUI.Label(new Rect(40,396,width-40,42),"Gravity: " + gravity,label);
        GUI.matrix = previousMatrix;
    }
    private void Row(float y,string binding,string action)
    {
        GUI.Label(new Rect(40,y,135,40),binding,key);
        GUI.Label(new Rect(185,y,365,40),action,label);
    }
}
