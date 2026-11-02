using System.Linq;
using UnityEngine;

/// <summary>Power Restoration's power path: installed module A feeds B's high junction through one of two wall routes.
/// Both routes work in 2026; by 2176 route 1 lies under a collapsed truss. The reactor's control rods only seat under
/// station gravity. Scene-local presentation of state the modules and selector own; never changes repairs.</summary>
public sealed class PowerCircuit : MonoBehaviour
{
    [SerializeField] PowerModuleA moduleA;
    [SerializeField] PowerModuleB moduleB;
    [SerializeField] Renderer[] feedLamps = { }, routeOneLamps = { }, routeTwoLamps = { };
    [Tooltip("Route 1 lamps from this index onward lie beyond the 2176 collapse.")]
    [SerializeField] int routeOneBreak = 3;
    [Tooltip("Reactor console lamps: A feed, B feed, control rods.")]
    [SerializeField] Renderer[] consoleLamps = new Renderer[3];
    [SerializeField] Transform[] controlRods = { };
    // Floating rods must be impossible to miss: amber finish, unlit sockets, a label and a pulsing light call it out.
    [SerializeField] Material floatingRodMaterial;
    [SerializeField] Renderer[] rodSockets = { };
    [SerializeField] TextMesh rodLabel;
    [SerializeField] Light rodWarning;
    [SerializeField] GameObject[] pastOnly = { }, futureOnly = { };
    [SerializeField] TextMesh selectorLabel, gravityLabel;
    [SerializeField] Material liveMaterial, deadMaterial;
    Vector3[] rodSeats;
    Quaternion[] rodRotations;
    Material seatedRodMaterial;

    public int Route { get; private set; } = 1;
    public bool AFeed => moduleA.IsRepaired;
    // In 2026 both wall routes are intact; in 2176 only route 2 still reaches B.
    public bool BFeed => AFeed && moduleB.IsRepaired && (Past || Route == 2);
    public bool RodsSeated => GlobalState.get().gravity > 4.5f;
    // The 2176 start conditions; the reactor itself checks that it is being used in 2176.
    public bool ReactorReady => AFeed && moduleB.IsRepaired && Route == 2 && RodsSeated;
    static bool Past => GlobalState.get().now == TimeState.Past;

    public void SelectRoute(int route) => Route = route;

    void Awake()
    {
        rodSeats = controlRods.Select(r => r.localPosition).ToArray();
        rodRotations = controlRods.Select(r => r.localRotation).ToArray();
        seatedRodMaterial = controlRods[0].GetComponent<Renderer>().sharedMaterial;
        rodLabel.text = ""; // Force the first LateUpdate to write both text and colour.
    }

    void LateUpdate()
    {
        bool past = Past;
        foreach (var item in pastOnly) if (item.activeSelf != past) item.SetActive(past);
        foreach (var item in futureOnly) if (item.activeSelf == past) item.SetActive(!past);
        foreach (var lamp in feedLamps) Lamp(lamp, AFeed);
        for (int i = 0; i < routeOneLamps.Length; i++) Lamp(routeOneLamps[i], AFeed && Route == 1 && (past || i < routeOneBreak));
        foreach (var lamp in routeTwoLamps) Lamp(lamp, AFeed && Route == 2);
        Lamp(consoleLamps[0], AFeed); Lamp(consoleLamps[1], BFeed); Lamp(consoleLamps[2], RodsSeated);
        // Without station gravity the rods drift out of their seats; restoring it drops them back.
        bool seated = RodsSeated;
        for (int i = 0; i < controlRods.Length; i++)
        {
            // Floating: well clear of the sockets, tilted, and slowly bobbing so the motion draws the eye.
            float bob = .15f * Mathf.Sin(Time.time * 1.4f + i * 1.7f);
            controlRods[i].localPosition = rodSeats[i] + (seated ? Vector3.zero : new Vector3(0, 1.5f + .35f * i + bob, 0));
            controlRods[i].localRotation = rodRotations[i] * (seated ? Quaternion.identity : Quaternion.Euler(14 + 6 * i, 45 * i, 8));
            var rod = controlRods[i].GetComponent<Renderer>();
            var finish = seated ? seatedRodMaterial : floatingRodMaterial;
            if (rod.sharedMaterial != finish) rod.sharedMaterial = finish;
        }
        foreach (var socket in rodSockets) Lamp(socket, seated);
        string rods = "CONTROL RODS\n" + (seated ? "SEATED" : "FLOATING");
        if (rodLabel.text != rods) { rodLabel.text = rods; rodLabel.color = seated ? new Color(.35f, 1, .65f) : new Color(1, .7f, .2f); }
        rodWarning.enabled = !seated;
        if (!seated) rodWarning.intensity = 1 + .8f * Mathf.Sin(Time.time * 4);
        // TextMesh rebuilds its mesh on every assignment, so only write changes.
        string selector = "B FEED / ROUTE " + Route, gravity = "STATION GRAVITY\n" + (RodsSeated ? "ON" : "OFF");
        if (selectorLabel.text != selector) selectorLabel.text = selector;
        if (gravityLabel.text != gravity) gravityLabel.text = gravity;
    }

    void Lamp(Renderer lamp, bool live)
    {
        var material = live ? liveMaterial : deadMaterial;
        if (lamp.sharedMaterial != material) lamp.sharedMaterial = material;
    }
}
