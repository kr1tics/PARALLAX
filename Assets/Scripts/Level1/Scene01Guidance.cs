using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Observes tutorial navigation only; does not alter gravity or player controls.</summary>
[DefaultExecutionOrder(325)]
public sealed class Scene01Guidance : MonoBehaviour
{
    [SerializeField] Level1Controller level;
    [SerializeField] Rigidbody playerBody;
    [SerializeField] ArtificialGravity playerGravity;
    [SerializeField] TMP_FontAsset supportingFont;
    GameObject recoveryPanel;
    TMP_Text controls, recovery;
    readonly RaycastHit[] floorHits = new RaycastHit[32];
    public string RecoveryHint => recovery == null ? "" : recovery.text;
    public string ControlHint => controls == null ? "" : controls.text;

    void Start()
    {
        var hud = transform.Find("Scene 01 Objective UI");
        controls = Label(hud, "Contextual control hints", new Vector2(28, 24), new Vector2(1000, 32), 16);
        recoveryPanel = new GameObject("Objective 02 flight help", typeof(RectTransform), typeof(Image));
        recoveryPanel.transform.SetParent(hud, false);
        var panelRect = recoveryPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(28, -188);
        panelRect.sizeDelta = new Vector2(470, 88);
        var background = recoveryPanel.GetComponent<Image>();
        background.color = new Color(0, 0, 0, .78f); background.raycastTarget = false;
        recovery = Label(recoveryPanel.transform, "Hover recovery hint", new Vector2(16, -12), new Vector2(438, 64), 17);
        recovery.rectTransform.anchorMin = recovery.rectTransform.anchorMax = recovery.rectTransform.pivot = new Vector2(0, 1);
        recovery.color = new Color(220f / 255, 228f / 255, 232f / 255);
        recoveryPanel.SetActive(false);
    }
    void LateUpdate()
    {
        if (controls == null || GameSettingsMenu.GameplayInputBlocked) return;
        bool floating = !playerGravity.IsGravityActive;
        bool showRecovery = level.IntroSolved && !level.MainSolved && !level.Completed && floating && !HasFloor();
        recovery.text = showRecovery ? "Hold CTRL to descend while floating.\nSPACE — Thrust up" : "";
        recoveryPanel.SetActive(showRecovery);
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
