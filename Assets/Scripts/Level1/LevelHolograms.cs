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
    Level1Presentation presentation;
    InvestigationFocus focus;
    Mesh routeMesh;
    GameObject leftArrow;
    public bool LeftArrowVisible => leftArrow != null && leftArrow.activeSelf;
    public string ActiveCue => focus != null && focus.VisibleMarkerCount > 0 ? presentation.CueLabel : "";

    void Start()
    {
        presentation = level.GetComponent<Level1Presentation>();
        objectiveText.rectTransform.sizeDelta = new Vector2(4.6f, .65f);
        objectiveText.rectTransform.anchoredPosition = new Vector2(0, .38f);
        objectiveText.fontSize = 1.5f;
        storageText.enabled = consoleText.enabled = false;
        storagePanel.enabled = consolePanel.enabled = false;
        foreach (var renderer in objectiveMarker.GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.GetComponent<TMP_Text>() == null) renderer.enabled = false;
        focus = gameObject.AddComponent<InvestigationFocus>();
        focus.UseFramedMarker();
        BuildLeftArrow();
        foreach (var text in level.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == objectiveText || text == storageText || text == consoleText || (text.GetComponentInParent<Canvas>() != null && text.GetComponentInParent<Canvas>().renderMode != RenderMode.WorldSpace)) continue;
            string label = text.text.ToUpperInvariant();
            int group = label.Contains("STATION 01") || label.Contains("ARRIVAL") ? 0 : label.Contains("GRAVITY") ? 1 :
                label.Contains("STORAGE") ? 2 : label.Contains("STATION 02") ? 3 : label.Contains("EXIT") ? 4 : -1;
            var renderer = text.GetComponent<Renderer>();
            if (renderer != null) focus.RegisterProjection(renderer, group, -1);
            else focus.Register(text.gameObject, group);
            text.fontSize = Mathf.Min(text.fontSize, 2.1f);
        }
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
        int group = presentation == null ? -1 : presentation.Stage == Level1Presentation.GuidanceStage.AccessRelay ? 0 :
            presentation.Stage == Level1Presentation.GuidanceStage.FindConsole || presentation.Stage == Level1Presentation.GuidanceStage.UnlockStorage ||
            presentation.Stage == Level1Presentation.GuidanceStage.RestoreGravity || presentation.Stage == Level1Presentation.GuidanceStage.ReturnFuture ? 1 :
            presentation.Stage == Level1Presentation.GuidanceStage.FindCube ? 2 : presentation.Stage == Level1Presentation.GuidanceStage.MoveCube ? 3 : 4;
        if (target == null && presentation != null)
            target = level.transform.Find(group == 0 ? "Pressure Plate 01" : "Pressure Plate 02");
        Vector3 position = target == null ? Vector3.zero : target.position + Vector3.up * .85f;
        if (target != null && presentation.CueLabel == "STORAGE AREA") position = new Vector3(-6, .95f, 11.5f);
        focus.SetFocus(level.Completed ? -1 : group, target == null || level.Completed ? null : "Tutorial section " + group + "/" + target.name, position, target);
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
