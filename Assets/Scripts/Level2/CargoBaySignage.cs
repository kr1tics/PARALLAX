using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(650)]
public sealed class CargoBaySignage : MonoBehaviour
{
    [SerializeField] CargoBayController level;
    [SerializeField] Rigidbody[] manifests;
    [SerializeField] PressureButton counterweight, receivingOne, receivingTwo;
    [SerializeField] TMP_FontAsset titleFont, bodyFont;
    [SerializeField] Renderer[] legacyLabels;
    [SerializeField] TMP_Text[] legacyTexts;
    readonly List<bool> originalTextEnabled = new();
    [SerializeField] Bounds containment;
    readonly List<bool> originalVisibility = new();
    readonly List<Plate> manifestPlates = new();
    readonly List<GameObject> roots = new();
    readonly List<Plate> contextualStates = new();
    Plate staging, weight, stagingBack, weightBack, linkage, linkageBack, freightHeader, maintenanceHeader, r1, r2, clamp, cage;
    Interact interaction;
    InvestigationFocus focus;
    Bounds freightEntrance;
    Renderer aSignal, pSignal;
    readonly List<Renderer> counterweightCables = new();
    MaterialPropertyBlock signalProperties;
    Vector3 previousPlayerPosition;
    bool c1Examined, c1Carried, gatePassed, c2Examined, weightExamined;
    float inspectionAge, linkageLookAge;
    Collider inspectedSurface;
    int resetVersion;
    public string GuidanceTarget { get; private set; }
    public bool FreightGatePassed => gatePassed;
    static readonly Color Ink = new Color32(220, 228, 232, 255);
    static readonly Color Muted = new Color32(149, 174, 182, 255);
    static readonly Color Amber = new Color32(232, 174, 105, 255);
    static readonly Color Green = new Color32(112, 222, 164, 255);
    sealed class Plate { public TMP_Text State; public Image Accent, Lamp; public int Manifest = -1; }
    void Start()
    {
        signalProperties = new MaterialPropertyBlock();
        focus = gameObject.AddComponent<InvestigationFocus>();
        focus.UseFramedMarker();
        var gateCollider = level.FreightGate.GetComponentInChildren<Collider>();
        freightEntrance = gateCollider.bounds;
        interaction = FindFirstObjectByType<Interact>();
        foreach (var text in legacyTexts) { originalTextEnabled.Add(text.enabled); text.enabled = false; }
        foreach (var renderer in legacyLabels) { originalVisibility.Add(renderer.enabled); renderer.enabled = false; }
        staging = Make("A freight plate", transform, new Vector3(-10.5f, -.48f, -4.245f), 0, new Vector2(.85f, .3f), "A / C1", "");
        weight = Make("P counterweight plate", transform, new Vector3(-10.8f, -.48f, 1.155f), 0, new Vector2(.85f, .3f), "P / CLAMP", "");
        stagingBack = Make("A freight reverse plate", transform, new Vector3(-10.5f,-.48f,-4.13f),180,new Vector2(.85f,.3f),"A / C1","");
        weightBack = Make("P counterweight reverse plate", transform, new Vector3(-10.8f,-.48f,1.27f),180,new Vector2(.85f,.3f),"P / CLAMP","");
        linkage = Make("P counterweight linkage",transform,new Vector3(-13.095f,2.18f,2.4f),270,new Vector2(.65f,.31f),"P","");
        linkageBack = Make("P counterweight linkage reverse",transform,new Vector3(-13.205f,2.18f,2.4f),90,new Vector2(.65f,.31f),"P","");
        r1 = Make("R1 receiving plate", transform, new Vector3(13.5f, 1.05f, 13.105f), 0, new Vector2(.95f, .3f), "R1 / C1", "");
        r2 = Make("R2 receiving plate", transform, new Vector3(-11.925f, 1.1f, 11.55f), 270, new Vector2(.95f, .3f), "R2 / C2", "");
        clamp = Make("C2 rack identification", transform, new Vector3(-13.095f, 2.72f, 2.6f), 270, new Vector2(.85f, .3f), "C2 / P", "");
        cage = Make("C1 containment identification", transform, new Vector3(containment.min.x + 1.05f, 1.85f, containment.min.z - .035f), 0,
            new Vector2(1.5f, .5f), "C1", "");
        Make("Cargo hall header", transform, new Vector3(0, 4.3f, -8.82f), 0, new Vector2(2.1f, .3f), "CARGO BAY / 02", "", true);
        freightHeader = Make("Freight gate header", transform, new Vector3(10.5f, 2.1f, 7.025f), 0, new Vector2(2.35f, .25f), "FREIGHT ACCESS / 02", "", true);
        maintenanceHeader = Make("Maintenance gate header", transform, new Vector3(10.5f, 2.1f, 13.025f), 0, new Vector2(2.35f, .25f), "MAINTENANCE ACCESS", "", true);
        Make("Archive rail identification", transform, new Vector3(-13.095f, 2.75f, -4.8f), 270, new Vector2(1.75f, .28f), "ARCHIVE RACKS", "", true);
        for (int index = 0; index < manifests.Length; index++)
        {
            var body = manifests[index].transform;
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90;
                var rotation = Quaternion.Euler(0, yaw, 0);
                var local = rotation * new Vector3(0, .08f, -.515f);
                var plate = Make("Cargo unit " + index + " plate " + side, body, body.TransformPoint(local), yaw,
                    new Vector2(.46f, .23f), index < 2 ? "C1" : "C2", "");
                plate.Manifest = index; manifestPlates.Add(plate);
            }
        }
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer.name == "Counterweight cable") counterweightCables.Add(renderer);
            else if (renderer.name.Contains("edge marking") || renderer.name == "R2 manifest marking" || renderer.name == "Pallet strapping" || renderer.name == "Freight route strip")
                focus.RegisterProjection(renderer, -3, .045f);
            else if (renderer.name == "C2 clamp jaw") focus.RegisterProjection(renderer, 1, .20f, equipment: true);
            else if (renderer.name == "Booth console screen") focus.RegisterProjection(renderer, 2, .20f, equipment: true);
            else if (renderer.name is "A - Staging indicator" or "P - Counterweight indicator" or "Cargo pad indicator" or "R2 - Receiving indicator")
            {
                if (renderer.name.StartsWith("A -")) aSignal=renderer;
                if (renderer.name.StartsWith("P -")) pSignal=renderer;
                focus.RegisterProjection(renderer, renderer.name.StartsWith("A -") ? 0 : renderer.name.StartsWith("P -") ? 1 : 2, .25f, equipment: true);
            }
            else if (renderer.GetComponentsInParent<Transform>(true).Any(t => t.name == "Cargo target hologram"))
                focus.RegisterProjection(renderer, 2, .20f, () => interaction != null && interaction.HeldObject != null);
        ResetGuidance();
        Refresh();
    }
    void LateUpdate() { if (staging != null) Refresh(); }
    void Refresh()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        if (resetVersion != level.GuidanceResetVersion) ResetGuidance();
        ObserveProgress(past);
        UpdateFocus(past);
        string aState = level.FreightAccessGranted ? "GATE OPEN" : level.StagingOccupied ? "LOAD DETECTED" : "AWAITING C1";
        foreach (var sign in new[]{staging,stagingBack})
        {
            Set(sign,aState,level.FreightAccessGranted);
            if (!level.FreightAccessGranted && level.StagingOccupied) sign.State.color=sign.Accent.color=sign.Lamp.color=new Color(.6f,.87f,.95f);
        }
        Signal(aSignal,level.FreightAccessGranted ? Green : level.StagingOccupied ? new Color(.6f,.87f,.95f) : Amber);
        bool released = level.RackReleased || !manifests[past ? 2 : 3].isKinematic;
        string pState = level.RackReleased ? "CARGO CLEAR" : released ? "CLAMP RELEASED" : past ? "STANDBY" : "LINK OFFLINE";
        foreach (var sign in new[]{weight,weightBack,linkage,linkageBack}) Set(sign,pState,released);
        Signal(pSignal,released ? Green : Amber);
        foreach (var cable in counterweightCables) Signal(cable,released ? Green : Amber);
        Set(freightHeader,level.FreightAccessGranted ? "OPEN" : "LOCKED",level.FreightAccessGranted);
        Set(maintenanceHeader,level.CargoDelivered ? "OPEN" : "LOCKED",level.CargoDelivered);
        Set(r1, level.CargoDelivered || (!past && receivingOne.IsPressed) ? "RECEIVED" : past && level.C1Positioned ? "POSITIONED" : "AWAITING CARGO", level.CargoDelivered || (!past && receivingOne.IsPressed));
        Set(r2, level.CargoDelivered || (!past && receivingTwo.IsPressed) ? "RECEIVED" : past && level.C2Positioned ? "POSITIONED" : "AWAITING CARGO", level.CargoDelivered || (!past && receivingTwo.IsPressed));
        if (past && !level.CargoDelivered)
            foreach (var receiver in new[]{r1,r2})
                if (receiver.State.text == "POSITIONED") receiver.State.color=receiver.Accent.color=receiver.Lamp.color=new Color(.6f,.87f,.95f);
        var activeRack = manifests[past ? 2 : 3];
        Set(clamp, level.RackReleased ? "EMPTY" : activeRack.isKinematic ? "LOCKED" : "RELEASED", level.RackReleased || !activeRack.isKinematic);
        bool occupied = !past && containment.Contains(manifests[1].transform.position);
        Set(cage, occupied ? "SEALED" : "CLEAR", !occupied);
        foreach (var sign in contextualStates)
            sign.State.enabled = focus.CanInspect(sign.State.transform.parent, 4.5f);
        foreach (var plate in manifestPlates)
        {
            bool received = level.CargoDelivered || (!past && (plate.Manifest < 2 ? receivingOne.IsPressed : receivingTwo.IsPressed));
            var body = manifests[plate.Manifest];
            bool secured = body.isKinematic || (plate.Manifest == 1 && occupied);
            bool held = interaction != null && interaction.HeldObject == body.GetComponent<MovableObject>();
            Set(plate, received ? "RECEIVED" : held ? "IN TRANSIT" : secured ? "SECURED" : "AVAILABLE", received);
            plate.State.enabled = false;
            if (!received && !secured) plate.State.color = Ink;
            if (!received) plate.Accent.color = plate.Manifest < 2 ? Amber : new Color32(125, 197, 220, 255);
        }
    }
    void Signal(Renderer renderer, Color colour)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(signalProperties); signalProperties.SetColor("_Color",colour); renderer.SetPropertyBlock(signalProperties);
    }
    void ResetGuidance()
    {
        resetVersion = level.GuidanceResetVersion;
        c1Examined = c1Carried = gatePassed = c2Examined = weightExamined = false;
        inspectedSurface = null; inspectionAge = linkageLookAge = 0;
        previousPlayerPosition = interaction.transform.position;
        var delta = freightEntrance.center - interaction.transform.position;
        Camera.main.GetComponent<Look>().Rotate(new Vector2(Mathf.Atan2(delta.z,delta.x)*Mathf.Rad2Deg,0));
    }
    void ObserveProgress(bool past)
    {
        if (Parallax.Settings.GameSettingsMenu.GameplayInputBlocked) return;
        var position = interaction.transform.position;
        if (level.FreightAccessGranted && previousPlayerPosition.z <= freightEntrance.center.z && position.z > freightEntrance.center.z)
        {
            float t = (freightEntrance.center.z-previousPlayerPosition.z)/(position.z-previousPlayerPosition.z);
            float x = Mathf.Lerp(previousPlayerPosition.x,position.x,t);
            if (Mathf.Abs(x-freightEntrance.center.x) < freightEntrance.extents.x+.15f) gatePassed=true;
        }
        previousPlayerPosition = position;
        var held = interaction.HeldObject;
        c1Carried |= held != null && (held == manifests[0].GetComponent<MovableObject>() || held == manifests[1].GetComponent<MovableObject>());
        c1Examined |= c1Carried || level.FreightCargoExamined;
        c2Examined |= level.SecuredCargoExamined || (held != null && (held == manifests[2].GetComponent<MovableObject>() || held == manifests[3].GetComponent<MovableObject>()));
        bool readingLinkage=focus.CanInspect(linkage.State.transform.parent,interaction.reachDistance) || focus.CanInspect(linkageBack.State.transform.parent,interaction.reachDistance);
        linkageLookAge=readingLinkage ? linkageLookAge+Time.deltaTime : 0;
        weightExamined |= linkageLookAge>.45f;
        var cast = interaction.fireInteractRay(false);
        var surface = cast.Item1 ? cast.Item2.collider : null;
        if (surface != inspectedSurface) { inspectionAge=0; inspectedSurface=surface; }
        if (surface == null) return;
        inspectionAge += Time.deltaTime;
        if (inspectionAge < .45f) return;
        var body = surface.attachedRigidbody;
        bool cageHit = surface.GetComponentsInParent<Transform>().Any(t=>t.name.StartsWith("Sealed freight cage"));
        c1Examined |= body == manifests[past ? 0 : 1] || (cageHit && !past && containment.Contains(manifests[1].position));
        c2Examined |= body == manifests[past ? 2 : 3];
        weightExamined |= surface.GetComponentInParent<PressureButton>() == counterweight;
    }
    Vector3 CargoAnchor(Rigidbody body, bool caged)
    {
        var bounds = body.GetComponent<Collider>().bounds;
        return new Vector3(bounds.center.x,bounds.max.y+.48f,caged ? containment.min.z-.8f : bounds.min.z-.8f);
    }
    void UpdateFocus(bool past)
    {
        int section = 0;
        string target = null; Vector3 point = Vector3.zero;
        var c1 = manifests[past ? 0 : 1]; var c2 = manifests[past ? 2 : 3];
        if (level.Completed) section=-1;
        else if (level.CargoDelivered) { section=3; target="Maintenance"; point=new Vector3(10.5f,.9f,12.1f); }
        else if (!level.FreightAccessGranted)
        {
            if (c1Carried && !level.StagingOccupied) { target="A"; point=new Vector3(-10.5f,.35f,-4.5f); }
            else if (!c1Examined) { target="C1"; point=CargoAnchor(c1,!past && containment.Contains(c1.position)); }
        }
        else if (!gatePassed) { target="Freight gate"; point=new Vector3(freightEntrance.min.x+.3f,.9f,freightEntrance.min.z-.7f); }
        else if (level.RackReleased || !c2.isKinematic)
        {
            section=2;
            bool waitingC1 = (level.ReceivingStatus & 1)==0;
            target=waitingC1 ? "Receiving C1" : "Receiving C2";
            point=waitingC1 ? new Vector3(13.5f,.65f,9.1f) : past ? new Vector3(-14.4f,.65f,8.5f) : new Vector3(-11.4f,1.25f,9.3f);
        }
        else
        {
            section=1;
            if (!c2Examined) { target="C2 clamp"; point=new Vector3(c2.position.x+2.1f,c2.GetComponent<Collider>().bounds.max.y+.4f,c2.position.z); }
            else if (!weightExamined)
            {
                target="P";
                point=interaction.transform.position.y>1.5f ? new Vector3(-13.55f,2.4f,2.4f) : new Vector3(-10.8f,.35f,1.05f);
            }
        }
        GuidanceTarget=target ?? "";
        focus.SetFocus(section,target=="P" && interaction.transform.position.y>1.5f ? "P upper linkage" : target,point);
    }
    static void Set(Plate plate, string text, bool live)
    {
        if (plate.State.text != text) plate.State.text = text;
        plate.State.color = plate.Accent.color = plate.Lamp.color = live ? Green : Amber;
    }
    Plate Make(string name, Transform parent, Vector3 position, float yaw, Vector2 size, string heading, string detail, bool header = false)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(Image)); roots.Add(root);
        root.transform.SetParent(parent, false);
        int group = name.StartsWith("A freight") || name.StartsWith("C1 containment") || name.StartsWith("Freight gate") || name.StartsWith("Cargo hall") ? 0 :
            name.StartsWith("P counterweight") || name.StartsWith("C2 rack") || name.StartsWith("Archive rail") ? 1 :
            name.StartsWith("R1") || name.StartsWith("R2") ? 2 : name.StartsWith("Maintenance") ? 3 : -1;
        focus.Register(root, group, name.StartsWith("A freight") || name.StartsWith("P counterweight"));
        root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var scale = parent.lossyScale; root.transform.localScale = new Vector3(.01f / scale.x, .01f / scale.y, .01f / scale.z);
        root.GetComponent<RectTransform>().sizeDelta = size * 100;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var background = root.GetComponent<Image>(); background.color = new Color32(17, 24, 29, 246); background.raycastTarget = false;
        var strip = new GameObject("Equipment accent", typeof(RectTransform), typeof(Image)); strip.transform.SetParent(root.transform, false);
        var rect = strip.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(3, 0); rect.sizeDelta = new Vector2(1, -8);
        var accent = strip.GetComponent<Image>(); accent.color = Muted; accent.raycastTarget = false;
        bool small = size.x < .8f;
        var title = Label(root.transform, "Equipment title", titleFont, small ? 8.5f : header ? 10 : 8.5f, header ? -7 : -5, header ? 18 : 13); title.text = heading;
        var body = Label(root.transform, "Equipment detail", bodyFont, small ? 4.6f : 6.5f, small ? -15 : -20, 10); body.text = detail; body.color = Muted;
        var state = Label(root.transform, "Equipment state", bodyFont, small ? 4.8f : 7, -size.y * 100 + (small ? 8 : 14), small ? 8 : 12);
        if (small || string.IsNullOrEmpty(detail)) body.gameObject.SetActive(false);
        if (header) { body.gameObject.SetActive(false); state.gameObject.SetActive(false); }
        var lamp = new GameObject("Status light",typeof(RectTransform),typeof(Image)); lamp.transform.SetParent(root.transform,false);
        var lampRect = lamp.GetComponent<RectTransform>(); lampRect.anchorMin = lampRect.anchorMax = Vector2.one;
        lampRect.anchoredPosition = new Vector2(-5,-size.y*100+5); lampRect.sizeDelta = new Vector2(3,3);
        var light = lamp.GetComponent<Image>(); light.raycastTarget=false; light.color=Amber;
        if (header && !name.StartsWith("Freight gate") && !name.StartsWith("Maintenance gate")) lamp.SetActive(false);
        int pair = name.StartsWith("A freight") || name.StartsWith("R1") || name.StartsWith("C1 containment") ? 1 :
            name.StartsWith("R2") ? 2 : name.StartsWith("P counterweight") || name.StartsWith("C2 rack") ? 3 :
            name.StartsWith("Cargo unit") ? heading == "C1" ? 1 : 2 : 0;
        for (int i = 0; i < pair; i++)
        {
            var mark = new GameObject("Equipment pairing stripe", typeof(RectTransform), typeof(Image));
            mark.transform.SetParent(root.transform, false);
            var mr = mark.GetComponent<RectTransform>(); mr.anchorMin = mr.anchorMax = new Vector2(1, .5f);
            mr.sizeDelta = new Vector2(2, pair == 3 && i == 1 ? 5 : 11);
            mr.anchoredPosition = new Vector2(-7-i*4, 0);
            var mi = mark.GetComponent<Image>(); mi.color = Muted; mi.raycastTarget = false;
        }
        var result = new Plate { State = state, Accent = accent, Lamp = light };
        if (!header) contextualStates.Add(result);
        return result;
    }
    static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, float top, float height)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var label = root.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
        label.enableAutoSizing = true; label.fontSizeMin = size * .8f; label.fontSizeMax = size;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.alignment = TextAlignmentOptions.TopLeft;
        label.color = Ink; label.raycastTarget = false;
        var rect = label.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, top); rect.sizeDelta = new Vector2(-14, height); return label;
    }
    void OnDestroy()
    {
        for (int i = 0; i < originalTextEnabled.Count; i++) if (legacyTexts[i] != null) legacyTexts[i].enabled = originalTextEnabled[i];
        for (int i = 0; i < originalVisibility.Count; i++) if (legacyLabels[i] != null) legacyLabels[i].enabled = originalVisibility[i];
        foreach (var root in roots) if (root != null) Destroy(root);
    }
}
