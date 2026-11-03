using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(700)]
public sealed class PowerInteractionFeedback : MonoBehaviour
{
    static PowerInteractionFeedback instance;
    const float Duration = 2.7f;
    RectTransform panel;
    CanvasGroup group;
    TMP_Text label;
    Image accent;
    float age = Duration, lastAttempt = -1;
    public string Message => label != null ? label.text : "";
    public float Opacity => group != null ? group.alpha : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => instance = null;

    public void Initialize(Transform parent, TMP_FontAsset font)
    {
        instance = this;
        var root = new GameObject("Power interaction notification", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        panel = root.GetComponent<RectTransform>(); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .12f);
        panel.sizeDelta = new Vector2(480, 48);
        var background = root.GetComponent<Image>(); background.color = new Color(.035f, .038f, .042f, .88f); background.raycastTarget = false;
        group = root.GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
        var line = new GameObject("Notification accent", typeof(RectTransform), typeof(Image)); line.transform.SetParent(root.transform, false);
        var lr = line.GetComponent<RectTransform>(); lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0, 1);
        lr.anchoredPosition = new Vector2(8, 0); lr.sizeDelta = new Vector2(2, -16); accent = line.GetComponent<Image>(); accent.raycastTarget = false;
        var text = new GameObject("Interaction result", typeof(RectTransform)); text.transform.SetParent(root.transform, false);
        label = text.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 19; label.color = new Color32(232, 229, 221, 255);
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; label.alignment = TextAlignmentOptions.Center;
        var tr = label.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(24, 8); tr.offsetMax = new Vector2(-18, -8);
    }
    public static bool Notify(Interact owner, Interactable target, string message, bool success = false)
    {
        if (instance == null || string.IsNullOrEmpty(message) || GameSettingsMenu.GameplayInputBlocked || owner == null ||
            owner.HeldObject != null || target == null || owner.gameObject.scene != target.gameObject.scene ||
            instance.gameObject.scene != target.gameObject.scene) return false;
        var cast = owner.fireInteractRay(true);
        if (!cast.Item1 || cast.Item2.collider.GetComponentInParent<Interactable>() != target) return false;
        return instance.Show(message, success);
    }
    public bool NotifySurfaceAttempt(Interact owner, Collider target, string message)
    {
        if (string.IsNullOrEmpty(message) || GameSettingsMenu.GameplayInputBlocked || owner == null || owner.HeldObject != null ||
            target == null || owner.gameObject.scene != target.gameObject.scene || gameObject.scene != target.gameObject.scene) return false;
        var cast = owner.fireInteractRay(false);
        if (!cast.Item1 || cast.Item2.collider != target) return false;
        return Show(message, false);
    }
    public bool NotifyEnvironmentEvent(string message, bool success = false)
    {
        if (GameSettingsMenu.GameplayInputBlocked || string.IsNullOrEmpty(message)) return false;
        return Show(message, success);
    }
    bool Show(string message, bool success)
    {
        if (Message == message && Time.unscaledTime - lastAttempt < .25f) return false;
        lastAttempt = Time.unscaledTime;
        label.text = message; age = 0;
        accent.color = success ? new Color32(112, 222, 164, 255) : new Color32(232, 174, 105, 255);
        float width = Mathf.Clamp(label.GetPreferredValues(message).x + 52, 240, 700);
        float canvasWidth = ((RectTransform)panel.parent).rect.width;
        if (canvasWidth > 0) width = Mathf.Min(width, canvasWidth - 56);
        panel.sizeDelta = new Vector2(width, Mathf.Max(48, label.GetPreferredValues(message, width - 42, 0).y + 16));
        return true;
    }
    void LateUpdate()
    {
        if (label == null) return;
        if (GameSettingsMenu.GameplayInputBlocked) { group.alpha = 0; return; }
        age += Time.deltaTime;
        group.alpha = Mathf.Clamp01(age / .12f) * Mathf.Clamp01((Duration - age) / .55f);
        if (age >= Duration) label.text = "";
    }
    public void Clear()
    {
        age = Duration;
        if (label != null) label.text = "";
        if (group != null) group.alpha = 0;
    }
    void OnDestroy() { if (instance == this) instance = null; }
}
