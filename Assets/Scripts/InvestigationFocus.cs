using System.Collections.Generic;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(900)]
public sealed class InvestigationFocus : MonoBehaviour
{
    sealed class Sign
    {
        public CanvasGroup Group;
        public int Section;
        public float Alpha;
        public bool Functional;
    }
    sealed class Projection
    {
        public Renderer Renderer;
        public int Section;
        public float Opacity;
        public bool Equipment;
        public System.Func<bool> Available;
    }
    readonly List<Sign> signs = new();
    readonly List<Projection> projections = new();
    readonly RaycastHit[] hits = new RaycastHit[64];
    MaterialPropertyBlock properties;
    Camera view;
    Mesh mesh;
    Material material;
    Transform marker, requestedTarget, shownTarget;
    Vector3 requestedPosition, shownPosition;
    string requestedKey, shownKey;
    float opacity;
    int section;
    bool framed;
    public int PrimarySection => section;
    public int VisibleMarkerCount => marker != null && marker.gameObject.activeSelf ? 1 : 0;
    public string MarkerKey => VisibleMarkerCount == 0 ? "" : shownKey;

    public void Register(GameObject root, int group, bool functional = false)
    {
        var canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false;
        signs.Add(new Sign { Group = canvasGroup, Section = group, Functional = functional });
    }
    public void RegisterProjection(Renderer renderer, int group, float alpha = .18f, System.Func<bool> available = null, bool equipment = false)
    {
        if (renderer != null) projections.Add(new Projection { Renderer = renderer, Section = group, Opacity = alpha, Available = available, Equipment = equipment });
    }
    public void SetFocus(int group, string key, Vector3 position, Transform target = null)
    {
        section = group; requestedKey = key; requestedPosition = position; requestedTarget = target;
    }
    void Awake()
    {
        properties = new MaterialPropertyBlock();
        view = Camera.main;
        var source = Resources.Load<Material>("UI/InvestigationMarker");
        if (source == null) return;
        material = new Material(source);
        var root = new GameObject("Primary investigation marker", typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(transform, false); marker = root.transform;
        mesh = new Mesh { name = "Investigation exclamation" };
        mesh.vertices = new[] { new Vector3(-.05f,.12f,0), new Vector3(-.05f,.39f,0), new Vector3(.05f,.39f,0), new Vector3(.05f,.12f,0),
            new Vector3(-.05f,-.075f,0), new Vector3(-.05f,.025f,0), new Vector3(.05f,.025f,0), new Vector3(.05f,-.075f,0) };
        mesh.uv = new[] { new Vector2(0,.3f), Vector2.up, Vector2.one, new Vector2(1,.3f), Vector2.zero, new Vector2(0,.25f), new Vector2(1,.25f), Vector2.right };
        mesh.triangles = new[] {0,1,2,0,2,3,4,5,6,4,6,7,0,2,1,0,3,2,4,6,5,4,7,6}; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        root.SetActive(false);
    }
    public void UseFramedMarker()
    {
        if (mesh == null || material == null) return;
        framed = true;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        void Quad(float left, float bottom, float right, float top)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(left,bottom,0)); vertices.Add(new Vector3(left,top,0));
            vertices.Add(new Vector3(right,top,0)); vertices.Add(new Vector3(right,bottom,0));
            for (int i=0;i<4;i++) uv.Add(new Vector2(vertices[start+i].x + .5f, .125f));
            foreach(int index in new[]{0,1,2,0,2,3,0,2,1,0,3,2}) triangles.Add(start+index);
        }
        Quad(-.055f,-.025f,.055f,.22f); Quad(-.055f,-.20f,.055f,-.09f);
        Quad(-.34f,-.34f,-.32f,.34f); Quad(.32f,-.34f,.34f,.34f);
        Quad(-.32f,.32f,.32f,.34f); Quad(-.32f,-.34f,.32f,-.32f);
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
        material.SetColor("_Color",new Color(1f,.82f,.16f)); material.SetFloat("_ScanDensity",2);
        material.SetFloat("_ScanSpeed",0); material.SetFloat("_FlickerStrength",0);
    }
    bool Visible(Vector3 point, Transform target)
    {
        if (view == null) return false;
        var viewport = view.WorldToViewportPoint(point);
        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
        var delta = point - view.transform.position;
        int count = Physics.RaycastNonAlloc(view.transform.position, delta.normalized, hits, Mathf.Max(0, delta.magnitude - .08f), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i].collider.transform;
            if (hit.GetComponentInParent<PlayerController>() != null) continue;
            if (target != null && (hit == target || hit.IsChildOf(target))) continue;
            return false;
        }
        return true;
    }
    bool FrontFacing(Transform target)
    {
        return !framed || (view != null && Vector3.Dot(target.forward,view.transform.position-target.position)<-.01f);
    }
    public bool CanInspect(Transform target, float range)
    {
        if (view == null || target == null || !FrontFacing(target)) return false;
        var delta=target.position-view.transform.position;
        return delta.sqrMagnitude<range*range && Vector3.Dot(view.transform.forward,delta.normalized)>.97f && Visible(target.position,null);
    }
    bool Inspecting(Transform sign)
    {
        if (!FrontFacing(sign)) return false;
        var delta = sign.position - view.transform.position;
        return delta.sqrMagnitude < 4f * 4f && Vector3.Dot(view.transform.forward, delta.normalized) > .95f && Visible(sign.position, null);
    }
    void LateUpdate()
    {
        if (view == null) view = Camera.main;
        bool blocked = GameSettingsMenu.GameplayInputBlocked;
        int inspected = section;
        float closest = float.PositiveInfinity;
        if (view != null && !blocked)
            foreach (var sign in signs)
                if (!sign.Functional && sign.Group != null && sign.Group.gameObject.activeInHierarchy && Inspecting(sign.Group.transform))
                {
                    float distance = (sign.Group.transform.position - view.transform.position).sqrMagnitude;
                    if (distance < closest) { closest = distance; inspected = sign.Section; }
                }
        if (view != null && !blocked)
            foreach (var item in projections)
                if (item.Opacity < 0 && item.Section >= 0 && item.Renderer != null && item.Renderer.gameObject.activeInHierarchy && Inspecting(item.Renderer.transform))
                {
                    float distance = (item.Renderer.transform.position - view.transform.position).sqrMagnitude;
                    if (distance < closest) { closest = distance; inspected = item.Section; }
                }
        bool retiring = signs.Exists(sign => !sign.Functional && sign.Group != null && sign.Section != inspected && sign.Alpha > .001f);
        foreach (var sign in signs)
        {
            if (sign.Group == null) continue;
            bool local = sign.Functional && view != null && Vector3.Distance(view.transform.position,sign.Group.transform.position) < 8 && Visible(sign.Group.transform.position,null);
            bool show = !blocked && FrontFacing(sign.Group.transform) && (local || (!sign.Functional && sign.Section == inspected && !retiring && (sign.Section >= 0 || (view != null && Inspecting(sign.Group.transform)))));
            sign.Alpha = Mathf.MoveTowards(sign.Alpha, show ? 1 : 0, Time.deltaTime / .25f);
            sign.Group.alpha = blocked ? 0 : sign.Alpha;
        }
        foreach (var item in projections)
        {
            if (item.Renderer == null) continue;
            bool relevant = !blocked && (item.Section == -3 || item.Section == inspected) && (item.Available == null || item.Available());
            item.Renderer.forceRenderingOff = !item.Equipment && !relevant;
            if (item.Opacity >= 0) { item.Renderer.GetPropertyBlock(properties); properties.SetFloat("_Alpha", item.Equipment && !relevant ? .045f : item.Opacity); item.Renderer.SetPropertyBlock(properties); }
        }
        if (marker == null) return;
        bool changed = shownKey != requestedKey || shownTarget != requestedTarget;
        bool showMarker = !blocked && section >= 0 && !string.IsNullOrEmpty(requestedKey);
        opacity = Mathf.MoveTowards(opacity, changed || !showMarker ? 0 : .8f, Time.deltaTime * 3.2f);
        if (opacity <= .001f && changed)
        {
            shownKey = requestedKey; shownTarget = requestedTarget; shownPosition = requestedPosition;
        }
        if (!changed) shownPosition = requestedPosition;
        marker.position = shownPosition;
        if (view != null)
        {
            marker.rotation = view.transform.rotation;
            if (framed) marker.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(view.transform.position, shownPosition) * .10f, .12f, 1.5f);
        }
        marker.gameObject.SetActive(!blocked && opacity > .001f && Visible(shownPosition, shownTarget));
        material.SetFloat("_Alpha", opacity);
    }
    void OnDestroy()
    {
        foreach (var item in projections) if (item.Renderer != null) item.Renderer.forceRenderingOff = false;
        if (marker != null) Destroy(marker.gameObject);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
