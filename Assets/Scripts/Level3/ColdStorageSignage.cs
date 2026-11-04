using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Cold Storage's mounted equipment plates, built with the shared <see cref="EquipmentPlate"/> like Cargo Bay's
/// signage: short identifiers, one muted detail line and a one-word state. Detailed reasons stay in interaction feedback.</summary>
[DefaultExecutionOrder(650)]
public sealed class ColdStorageSignage : MonoBehaviour
{
    [SerializeField] ColdStorageController plant;
    [SerializeField] TMP_FontAsset titleFont, bodyFont;
    sealed class Plate { public TMP_Text State; public Image Accent; }
    readonly List<GameObject> roots = new();
    readonly List<(Plate plate, int cell, bool future)> cellPlates = new();
    Plate valve, vault, s1, s2, r, pan, f3, console, reactor;
    InvestigationFocus focus;
    Interact interaction;
    bool reactorExamined, valveExamined, pastValveExamined, flangeExamined, chamberExamined, reactorThought;
    readonly bool[] cellExamined = new bool[2];
    int resetVersion;
    readonly List<(TMP_Text label, Transform root)> details = new();
    Renderer heatGlass, recessedSource;
    readonly List<Material> visualMaterials = new();
    MaterialPropertyBlock heatProperties, originalHeatProperties;
    public string GuidanceTarget { get; private set; }
    public bool Overheating => GlobalState.get().now == TimeState.Future && !plant.Completed;


    void Start()
    {
        focus = gameObject.AddComponent<InvestigationFocus>(); focus.UseFramedMarker();
        interaction = FindFirstObjectByType<Interact>();
        heatGlass = GameObject.Find("Exchanger sight glass")?.GetComponent<Renderer>();
        heatProperties = new MaterialPropertyBlock(); originalHeatProperties = new MaterialPropertyBlock();
        if (heatGlass != null) heatGlass.GetPropertyBlock(originalHeatProperties);
        reactor = Make("Reactor investigation plate", transform, new Vector3(0,2.75f,6.46f),0,new Vector2(1.6f,.38f),"REACTOR COOLING","",true);

        valve = Make("Plant valve plate", transform, new Vector3(-3.55f, 1.12f, -3.315f), 0, new Vector2(.9f, .5f), "COOLING VALVE", "LOOP / SERVICE");
        vault = Make("Vault plate", transform, new Vector3(-6, 4.95f, 2.825f), 0, new Vector2(1.6f, .46f), "COOLANT VAULT", "POWERED COOLING");
        s1 = Make("S1 plate", transform, new Vector3(0, 1.5f, 5.665f), 0, new Vector2(1.3f, .46f), "COOLANT SLOT 1", "S1 / SEALED LOOP");
        console = Make("Exchanger console plate", transform, new Vector3(-2.8f, 1.18f, 4.285f), 0, new Vector2(1.1f, .46f), "EXCHANGER", "START-UP");
        pan = Make("Balance pan plate", transform, new Vector3(2.6f, .36f, 2.215f), 0, new Vector2(1.1f, .46f), "P / BALANCE PAN", "UPPER CHAMBER ACCESS");
        r = Make("R plate", transform, new Vector3(8.085f, 8.75f, 6), 90, new Vector2(1.6f, .46f), "R / COLD CHAMBER", "UNPOWERED COOLING");
        s2 = Make("S2 plate", transform, new Vector3(5, 7.33f, 5.145f), 0, new Vector2(1f, .46f), "COOLANT SLOT 2", "S2 / OPEN STORAGE");
        f3 = Make("F3 tag", transform, new Vector3(2.9f, 4.12f, 7.58f), 0, new Vector2(.85f, .32f), "F3 / PIPE JOINT", "");
        Make("Plant header", transform, new Vector3(0, 8.3f, 10.775f), 0, new Vector2(2.4f, .32f), "COLD STORAGE / 03", "", true);
        Make("Trunk header", transform, new Vector3(0, 2.05f, 10.375f), 0, new Vector2(2.3f, .28f), "PRIMARY LOOP / REACTOR HALL", "", true);
        for (int cell = 0; cell < 2; cell++)
            foreach (bool future in new[] { false, true })
            {
                var body = (future ? plant.FutureCell(cell) : plant.PastCell(cell)).transform;
                string id = cell == 0 ? "A" : "B";
                for (int side = 0; side < 4; side++)
                {
                    var rotation = Quaternion.Euler(0, side * 90, 0);
                    if (side % 2 == 0)
                        cellPlates.Add((Make("Cell " + id + " plate " + side, body, body.TransformPoint(rotation * new Vector3(0, -.12f, -.515f)), body.eulerAngles.y + side * 90,
                            new Vector2(.6f, .3f), "CELL " + id, ""), cell, future));
                    else Stencil(body, id, rotation, cell == 0 ? EquipmentPlate.Amber : new Color32(125, 197, 220, 255));
                }
            }
        ImproveEquipmentReadability();
        Refresh();
    }

    void LateUpdate() { if (valve != null) Refresh(); }

    void Refresh()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        UpdateGuidance(past);
        foreach (var item in details) item.label.enabled = focus.CanInspect(item.root,4.5f);
        if (heatGlass != null)
        {
            if (Overheating)
            {
                heatGlass.GetPropertyBlock(heatProperties);
                heatProperties.SetColor("_Color",new Color(1,.32f,.07f));
                heatProperties.SetColor("_EmissionColor",new Color(.6f,.12f,.02f));
                heatGlass.SetPropertyBlock(heatProperties);
            }
            else heatGlass.SetPropertyBlock(originalHeatProperties);
        }
        Set(valve, past ? (plant.ServiceMode ? "SERVICE" : "LOOP") : "STUCK", past && !plant.ServiceMode);
        Set(vault, !past ? "FAILED" : plant.VaultOpen ? "OPEN" : "LOCKED", false);
        Set(s1, past ? (plant.ServiceMode ? "LOOP OPEN" : plant.FlangeRepaired ? "LOOP SEALED" : "LOOP LEAKING") : plant.DisplaySocketState(1).ToUpperInvariant(), past ? plant.LoopSealed : plant.SocketUsable(1));
        Set(s2, plant.DisplaySocketState(2).ToUpperInvariant(), !past && plant.SocketUsable(2));
        Set(r, plant.HatchObstructed ? "HELD OPEN" : plant.HatchOpen ? "OPEN" : "SEALED", !plant.HatchOpen);
        Set(pan, plant.PanLoaded ? "HATCH LIFTED" : plant.HatchObstructed ? "HATCH HELD" : "HATCH SEALED", !plant.HatchOpen);
        Set(f3, past ? (plant.FlangeRepaired ? "REPAIRED" : "CRACKED") : plant.FlangeRepaired ? "INTACT" : "BURST", plant.FlangeRepaired);
        Set(console, plant.Completed ? "RUNNING" : past ? "ONLINE" : plant.Ready ? "READY" : "COOLANT REQUIRED", plant.Completed || plant.Ready);
        foreach (var (plate, cell, future) in cellPlates)
        {
            bool usable = !future || plant.CellUsable(cell);
            Set(plate, "", usable);
        }
    }

    void UpdateGuidance(bool past)
    {
        if (resetVersion != plant.GuidanceResetVersion)
        {
            resetVersion=plant.GuidanceResetVersion;
            reactorExamined=valveExamined=pastValveExamined=flangeExamined=chamberExamined=reactorThought=false;
            System.Array.Clear(cellExamined,0,cellExamined.Length);
        }
        if (!Parallax.Settings.GameSettingsMenu.GameplayInputBlocked)
        {
            reactorExamined |= focus.CanInspect(reactor.State.transform.parent,5) || focus.CanInspect(console.State.transform.parent,3);
            if (!past && reactorExamined && !reactorThought)
            {
                reactorThought = true; plant.ObserveEquipment("Reactor");
            }
            var chamberPoint = new Vector3(8.1f,9.8f,6);
            var eye = Camera.main.transform.position;
            if (!chamberExamined && Vector3.Distance(eye,chamberPoint)<2.5f &&
                Vector3.Dot(Camera.main.transform.forward,(chamberPoint-eye).normalized)>.8f &&
                (focus.CanInspect(r.State.transform.parent,3.5f) || plant.HatchOpen && eye.x>7.2f && eye.x<9.8f && eye.y>8.8f && eye.y<11))
            {
                chamberExamined = true; plant.ObserveEquipment("Chamber");
            }
            bool valveRead=focus.CanInspect(valve.State.transform.parent,3);
            valveExamined |= valveRead; pastValveExamined |= past && valveRead;
            flangeExamined |= focus.CanInspect(f3.State.transform.parent,3);
            var ray=interaction.fireInteractRay(true);
            for (int i=0;i<2;i++)
            {
                var body=past ? plant.PastCell(i) : plant.FutureCell(i);
                cellExamined[i] |= interaction.HeldObject==body.GetComponent<MovableObject>() ||
                    (ray.Item1 && ray.Item2.collider.attachedRigidbody==body);
            }
        }
        int section=0; string target="Reactor";
        Vector3 point=new Vector3(1.5f,2.6f,6.25f); Transform bodyTarget=null;
        if (plant.Completed || plant.Starting) { target=null; section=-1; }
        else if (reactorExamined)
        {
            if (plant.Ready) { target="Exchanger"; section=10; point=new Vector3(-2.8f,1.9f,3.9f); }
            else if (!plant.FlangeRepaired && past && plant.ServiceMode)
            { target="F3"; section=2; point=new Vector3(2.9f,4.8f,7.6f); }
            else if ((!plant.FlangeRepaired && (!valveExamined || (past && !pastValveExamined))) || (plant.FlangeRepaired && plant.ServiceMode))
            { target="Cooling valve"; section=1; point=new Vector3(-3.55f,1.85f,-3.6f); }
            else if (!plant.FlangeRepaired && !flangeExamined)
            { target="F3"; section=2; point=new Vector3(2.9f,4.8f,7.6f); }
            else if (!past && plant.CellUsable(0) && plant.CellUsable(1))
            {
                bool first=!plant.SocketUsable(1); target=first ? "Slot 1" : "Slot 2"; section=first ? 5 : 6;
                point=first ? new Vector3(0,1.8f,4.2f) : new Vector3(5,8.5f,4.9f);
            }
            else
            {
                int cell=!cellExamined[0] ? 0 : !cellExamined[1] ? 1 : -1;
                section=cell==0 ? 3 : cell==1 ? 4 : 0; target=cell<0 ? null : "Cell "+(cell==0 ? "A" : "B");
                if(cell>=0)
                {
                    var body=past ? plant.PastCell(cell) : plant.FutureCell(cell);
                    point=body.position+new Vector3(0,1.1f,-.85f); bodyTarget=body.transform;
                }
            }
        }
        GuidanceTarget=target ?? "";
        focus.SetFocus(section,target,point,bodyTarget);
    }

    void ImproveEquipmentReadability()
    {
        var backing = GameObject.Find("R hull backing");
        if (backing != null)
        {
            var source = backing.GetComponent<MeshRenderer>();
            var visual = new GameObject("R hull backing surface", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(backing.transform, false);
            visual.transform.position = backing.transform.position + Vector3.right * .14f;
            visual.GetComponent<MeshFilter>().sharedMesh = backing.GetComponent<MeshFilter>().sharedMesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            recessedSource = source; source.enabled = false;
            roots.Add(visual);
        }
        foreach (string name in new[] { "F3 crack vapour (2026)", "F3 burst and ice (2176)" })
        {
            var parent = GameObject.Find(name);
            if (parent == null) parent = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == name && t.gameObject.scene == gameObject.scene)?.gameObject;
            if (parent == null) continue;
            var split = GameObject.CreatePrimitive(PrimitiveType.Cube);
            split.name = "F3 exposed fracture";
            Destroy(split.GetComponent<Collider>());
            split.transform.SetParent(parent.transform, false);
            split.transform.position = new Vector3(2.9f, 4.62f, 7.995f);
            split.transform.rotation = Quaternion.Euler(0, 0, 28);
            split.transform.localScale = new Vector3(.36f, .065f, .045f);
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(.035f,.045f,.05f);
            visualMaterials.Add(material);
            split.GetComponent<Renderer>().sharedMaterial = material;
            roots.Add(split);
        }
    }

    static void Set(Plate plate, string text, bool live)
    {
        if (plate.State.text != text) plate.State.text = text;
        plate.State.color = plate.Accent.color = live ? EquipmentPlate.Green : EquipmentPlate.Amber;
    }

    Plate Make(string name, Transform parent, Vector3 position, float yaw, Vector2 size, string heading, string detail, bool header = false)
    {
        var root = EquipmentPlate.Create(name, parent, position, yaw, size, heading, detail, titleFont, bodyFont, header, out var state, out var accent);
        roots.Add(root);
        int section = name.StartsWith("Reactor") || name=="Plant header" || name=="Trunk header" ? 0 :
            name.StartsWith("Plant valve") ? 1 : name.StartsWith("F3") ? 2 : name.StartsWith("Cell A") ? 3 : name.StartsWith("Cell B") ? 4 :
            name.StartsWith("S1") ? 5 : name.StartsWith("S2") ? 6 : name.StartsWith("R plate") ? 7 : name.StartsWith("Balance") ? 8 : name.StartsWith("Vault") ? 9 : 10;
        focus.Register(root,section);
        var detailLabel = root.transform.Find("Equipment detail").GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(detail)) detailLabel.gameObject.SetActive(false);
        else details.Add((detailLabel,root.transform));
        return new Plate { State = state, Accent = accent };
    }

    // A painted identity letter on the cell body, no backing.
    void Stencil(Transform body, string id, Quaternion rotation, Color colour)
    {
        var go = new GameObject("Cell " + id + " stencil", typeof(TextMeshPro)); roots.Add(go);
        go.transform.SetParent(body, false);
        go.transform.localPosition = rotation * new Vector3(0, .02f, -.506f); go.transform.localRotation = rotation;
        var scale = body.lossyScale; go.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        var text = go.GetComponent<TextMeshPro>(); text.font = titleFont; text.text = id; text.fontSize = 5.5f; text.color = colour;
        text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(.8f, .8f);
        focus.RegisterProjection(text.GetComponent<Renderer>(),id=="A" ? 3 : 4,-1);
    }

    void OnDestroy()
    {
        if (recessedSource != null) recessedSource.enabled = true;
        foreach (var material in visualMaterials) Destroy(material);
        if (heatGlass != null) heatGlass.SetPropertyBlock(originalHeatProperties);
        foreach (var root in roots) if (root != null) Destroy(root);
    }
}
