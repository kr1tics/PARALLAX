using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Observes tutorial navigation only; does not alter gravity or player controls.</summary>
[DefaultExecutionOrder(325)]
public sealed class Scene01Guidance : MonoBehaviour
{
    [SerializeField] Level1Controller level;
    [SerializeField] Rigidbody playerBody;
    [SerializeField] ArtificialGravity playerGravity;
    [SerializeField] TMP_FontAsset supportingFont;
    [SerializeField, Min(1)] float hoverDelay = 8;
    [SerializeField, Min(.1f)] float stationarySpeed = 1.5f;
    float hoverTime;
    TMP_Text controls, recovery;
    readonly RaycastHit[] floorHits = new RaycastHit[32];
    public string RecoveryHint => recovery == null ? "" : recovery.text;
    public string ControlHint => controls == null ? "" : controls.text;

    void Start()
    {
        var hud = transform.Find("Scene 01 Objective UI");
        controls = Label(hud, "Contextual control hints", new Vector2(28, 24), new Vector2(1000, 32), 16);
        recovery = Label(hud, "Hover recovery hint", new Vector2(28, 70), new Vector2(880, 58), 18);
    }
    void LateUpdate()
    {
        if (controls == null || GameSettingsMenu.GameplayInputBlocked) return;
        bool floating = !playerGravity.IsGravityActive;
        bool verticalInput = Keyboard.current != null && (Keyboard.current.ctrlKey.isPressed || Keyboard.current.spaceKey.isPressed);
        bool eligible = level.IntroSolved && !level.MainSolved && !level.Completed && floating &&
            !HasFloor() && playerBody.linearVelocity.magnitude < stationarySpeed && !verticalInput;
        hoverTime = eligible ? hoverTime + Time.deltaTime : 0;
        bool showRecovery = eligible && hoverTime >= hoverDelay;
        recovery.text = showRecovery ? "CTRL — Thrust down   ·   SPACE — Thrust up\n" +
            (GlobalState.get().gravity < .1f ? "Restore station gravity at the Past console." : "Q — Toggle jetpack to return to walking.") : "";
        controls.text = "ESC / P — Settings / Resume" +
            (level.Completed ? "    ·    R — Replay" : floating ? "    ·    Q — Toggle jetpack" : "") +
            (showRecovery ? "    ·    R — Reset checkpoint" : "");
    }
    bool HasFloor()
    {
        var collider = playerBody.GetComponent<Collider>();
        Vector3 feet = collider != null ? new Vector3(collider.bounds.center.x, collider.bounds.min.y, collider.bounds.center.z) : playerBody.position - Vector3.up * .85f;
        int count = Physics.RaycastNonAlloc(feet + Vector3.up * .25f, Vector3.down, floorHits, .65f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (floorHits[i].rigidbody != playerBody && !floorHits[i].collider.transform.IsChildOf(playerBody.transform) && floorHits[i].normal.y >= .6f) return true;
        return false;
    }
    TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var text = root.AddComponent<TextMeshProUGUI>(); text.font = supportingFont; text.fontSize = fontSize;
        text.color = new Color(.70f, .73f, .75f); text.raycastTarget = false;
        var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position; rect.sizeDelta = size; return text;
    }
}
