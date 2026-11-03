using TMPro;
using Parallax.Settings;
using UnityEngine;

/// <summary>Small unlit projections; text keeps its own font shader and never flickers.</summary>
[DefaultExecutionOrder(310)]
public sealed class LevelHolograms : MonoBehaviour
{
    [SerializeField] private Level1Controller level;
    [SerializeField] private StorageInterlock interlock;
    [SerializeField] private Renderer storagePanel, consolePanel;
    [SerializeField] private TMP_Text storageText, consoleText, objectiveText;
    [SerializeField] private Transform objectiveMarker;
    [SerializeField] private Material guidance, locked, ready;
    private string previousStorage, previousConsole;
    private Camera view;
    Level1Presentation presentation;
    Mesh routeMesh;
    GameObject leftArrow;
    public bool LeftArrowVisible => leftArrow != null && leftArrow.activeSelf;
    static readonly Color TargetYellow = new Color(1f, .80f, .18f);
    readonly RaycastHit[] sightHits = new RaycastHit[32];
    public string ActiveCue => objectiveMarker.gameObject.activeSelf ? objectiveText.text : "";

    void Start()
    {
        view = Camera.main;
        presentation = level.GetComponent<Level1Presentation>();
        objectiveText.rectTransform.sizeDelta = new Vector2(4.6f, .65f);
        objectiveText.rectTransform.anchoredPosition = new Vector2(0, .38f);
        objectiveText.fontSize = 1.5f;
        storageText.enabled = consoleText.enabled = false;
        storagePanel.enabled = consolePanel.enabled = false;
        foreach (var renderer in objectiveMarker.GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.GetComponent<TMP_Text>() == null) renderer.enabled = false;
        Chevron(-1); Chevron(1);
        BuildLeftArrow();
        objectiveMarker.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        UpdateCue();
        leftArrow.SetActive(presentation != null && presentation.Stage == Level1Presentation.GuidanceStage.FindConsole &&
            !GameSettingsMenu.GameplayInputBlocked && !level.Completed);
        string storage = interlock.ClosingDeferred ? "STORAGE / SAFETY HOLD" :
            interlock.IsOpen ? "STORAGE / OPEN" :
            !level.IntroSolved ? "STORAGE / LOCKED\nRESTORE ACCESS RELAY" :
            "STORAGE / LOCKED\nPAST + GRAVITY OFF";
        if (storage != previousStorage)
        {
            storageText.text = previousStorage = storage;
            storagePanel.sharedMaterial = interlock.IsOpen && !interlock.ClosingDeferred ? ready : locked;
        }
        bool past = GlobalState.get().now == TimeState.Past;
        bool gravityOn = GlobalState.get().gravity > 0.1f;
        string console = (past ? "GRAVITY CONTROL" : "CONTROL OFFLINE") +
            "\nSTATION GRAVITY " + (gravityOn ? "ON" : "OFF");
        if (console != previousConsole)
        {
            consoleText.text = previousConsole = console;
            consolePanel.sharedMaterial = past ? guidance : locked;
        }
    }

    void UpdateCue()
    {
        var target = presentation != null ? presentation.CueTarget : null;
        bool show = target != null && !level.Completed && !GameSettingsMenu.GameplayInputBlocked;
        Vector3 position = target != null ? target.position + Vector3.up * .85f : Vector3.zero;
        if (target != null && presentation.CueLabel == "STORAGE AREA") position = new Vector3(-6, .95f, 11.5f);
        if (show && view != null)
        {
            Vector3 viewport = view.WorldToViewportPoint(position);
            show = viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1;
            Vector3 delta = position - view.transform.position;
            int count = Physics.RaycastNonAlloc(view.transform.position, delta.normalized, sightHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && show; i++)
            {
                var hit = sightHits[i].collider.transform;
                if (hit.GetComponentInParent<PlayerController>() != null || hit == target || hit.IsChildOf(target)) continue;
                show = false;
            }
        }
        objectiveMarker.gameObject.SetActive(show);
        if (!show) return;
        objectiveMarker.position = position;
        objectiveText.text = presentation.CueLabel;
        objectiveText.color = TargetYellow;
        objectiveText.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        if (view != null) objectiveMarker.rotation = view.transform.rotation;
    }
    void Chevron(float side)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Yellow target chevron";
        var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.transform.SetParent(objectiveMarker, false);
        go.transform.localPosition = new Vector3(side * .065f, -.04f, 0);
        go.transform.localRotation = Quaternion.Euler(0, 0, -side * 34);
        go.transform.localScale = new Vector3(.035f, .25f, .025f);
        var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = presentation.PlacementHologram;
        var properties = new MaterialPropertyBlock(); properties.SetFloat("_Alpha", .65f);
        renderer.SetPropertyBlock(properties);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
    void BuildLeftArrow()
    {
        leftArrow = new GameObject("Console route left arrow", typeof(MeshFilter), typeof(MeshRenderer));
        leftArrow.transform.SetParent(level.transform, false);
        leftArrow.transform.position = new Vector3(.9f, -.955f, 13.45f);
        routeMesh = new Mesh { name = "Yellow left turn floor marking" };
        routeMesh.vertices = new[] { new Vector3(-.9f,0,0), new Vector3(-.25f,0,.48f),
            new Vector3(-.25f,0,.19f), new Vector3(.9f,0,.19f), new Vector3(.9f,0,-.19f),
            new Vector3(-.25f,0,-.19f), new Vector3(-.25f,0,-.48f) };
        var vertices = routeMesh.vertices;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            uv[i] = new Vector2((vertices[i].z + .48f) / .96f, (vertices[i].x + .9f) / 1.8f);
        routeMesh.uv = uv;
        routeMesh.triangles = new[]{0,1,6,2,3,4,2,4,5}; routeMesh.RecalculateNormals(); routeMesh.RecalculateBounds();
        leftArrow.GetComponent<MeshFilter>().sharedMesh = routeMesh;
        var renderer = leftArrow.GetComponent<Renderer>(); renderer.sharedMaterial = presentation.PlacementHologram;
        var properties = new MaterialPropertyBlock(); properties.SetFloat("_Alpha", .65f);
        renderer.SetPropertyBlock(properties);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        leftArrow.SetActive(false);
    }
    void OnDestroy()
    {
        if (routeMesh != null) Destroy(routeMesh);
        if (leftArrow != null) Destroy(leftArrow);
    }

}
