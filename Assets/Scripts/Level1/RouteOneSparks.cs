using TMPro;
using UnityEngine.UI;
using UnityEngine;

[DefaultExecutionOrder(750)]
public sealed class RouteOneSparks : MonoBehaviour
{
    [SerializeField] ParticleSystem sparks;
    [SerializeField] ParticleSystem upperSparks;
    [SerializeField] Vector2 burstInterval = new Vector2(.5f, 1.25f);
    [SerializeField] Vector2Int burstCount = new Vector2Int(12, 20);
    readonly System.Random timing = new System.Random(20917);
    float remaining, upperRemaining;

    CanvasGroup status;

    void Start()
    {
        var root = new GameObject("Route 1 connection status", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        root.transform.SetPositionAndRotation(new Vector3(8.70f, 6.48f, 4.15f), Quaternion.Euler(0, 90, 0));
        root.transform.localScale = Vector3.one * .01f;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(94, 25);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        status = root.GetComponent<CanvasGroup>(); status.alpha = 0; status.interactable = false; status.blocksRaycasts = false;
        var panel = root.AddComponent<Image>(); panel.color = new Color32(17, 24, 29, 242); panel.raycastTarget = false;
        var textRoot = new GameObject("Connection lost", typeof(RectTransform)); textRoot.transform.SetParent(root.transform, false);
        var text = textRoot.AddComponent<TextMeshProUGUI>();
        var intro = Resources.Load<GameObject>("UI/ContextIntro");
        if (intro != null) text.font = intro.GetComponent<ContextIntro>().metadataFont;
        text.text = "CONNECTION LOST"; text.fontSize = 9; text.color = new Color32(232, 174, 105, 255);
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(5, 2); text.rectTransform.offsetMax = new Vector2(-5, -2);
    }
    void RefreshStatus()
    {
        if (status == null) return;
        var camera = Camera.main;
        bool near = camera != null && GlobalState.get().now == TimeState.Future &&
            (camera.transform.position - status.transform.position).sqrMagnitude < 25;
        status.alpha = Mathf.MoveTowards(status.alpha, near ? 1 : 0, Time.deltaTime * 4);
    }
    void OnEnable()
    {
        remaining = .18f; upperRemaining = .48f;
        ResetEffect(sparks); ResetEffect(upperSparks);
    }
    static void ResetEffect(ParticleSystem effect)
    {
        if (effect == null) return;
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (GlobalState.get().now == TimeState.Future) effect.Play(true);
    }
    void LateUpdate()
    {
        RefreshStatus();
        if (GlobalState.get().now != TimeState.Future)
        {
            Clear(sparks); Clear(upperSparks);
            return;
        }
        if (Time.deltaTime <= 0) return;
        Tick(sparks, ref remaining); Tick(upperSparks, ref upperRemaining);
    }
    void Tick(ParticleSystem effect, ref float delay)
    {
        if (effect == null) return;
        var main = effect.main;
        main.gravityModifier = .55f * Mathf.Clamp01(GlobalState.get().gravity / 9.81f);
        delay -= Time.deltaTime;
        if (delay > 0) return;
        effect.Emit(timing.Next(burstCount.x, burstCount.y + 1));
        delay = Mathf.Lerp(burstInterval.x, burstInterval.y, (float)timing.NextDouble());
    }
    static void Clear(ParticleSystem effect)
    {
        if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void OnDisable()
    {
        if (status != null) status.alpha = 0;
        Clear(sparks); Clear(upperSparks);
    }
}
