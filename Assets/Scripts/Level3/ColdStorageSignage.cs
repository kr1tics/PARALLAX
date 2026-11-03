using System.Collections.Generic;
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
    Plate valve, vault, s1, s2, r, pan, f3, console;

    void Start()
    {
        valve = Make("Plant valve plate", transform, new Vector3(-3.55f, 1.12f, -3.315f), 0, new Vector2(.9f, .5f), "PLANT VALVE", "LOOP / SERVICE");
        vault = Make("Vault plate", transform, new Vector3(-6, 4.95f, 2.825f), 0, new Vector2(1.6f, .46f), "VAULT / ACTIVE CHILL", "PLANT POWER");
        s1 = Make("S1 plate", transform, new Vector3(0, 1.5f, 5.665f), 0, new Vector2(1.3f, .46f), "S1 / CARTRIDGE BAY", "SEALED IN LOOP");
        console = Make("Exchanger console plate", transform, new Vector3(-2.8f, 1.18f, 4.285f), 0, new Vector2(1.1f, .46f), "EXCHANGER", "START-UP");
        pan = Make("Balance pan plate", transform, new Vector3(2.6f, .36f, 2.215f), 0, new Vector2(1.1f, .46f), "P / BALANCE PAN", "LINKED TO R");
        r = Make("R plate", transform, new Vector3(8.085f, 8.75f, 6), 90, new Vector2(1.6f, .46f), "R / RADIATOR SINK", "PASSIVE COLD");
        s2 = Make("S2 plate", transform, new Vector3(5, 7.33f, 5.145f), 0, new Vector2(1f, .46f), "S2 / HOT SOCKET", "OPEN CRADLE");
        f3 = Make("F3 tag", transform, new Vector3(2.9f, 4.12f, 7.995f), 0, new Vector2(.5f, .28f), "F3", "");
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
        Refresh();
    }

    void LateUpdate() { if (valve != null) Refresh(); }

    void Refresh()
    {
        bool past = GlobalState.get().now == TimeState.Past;
        Set(valve, past ? (plant.ServiceMode ? "SERVICE" : "LOOP") : "SEIZED", past && !plant.ServiceMode);
        Set(vault, !past ? "FAILED" : plant.VaultOpen ? "OPEN" : "LOCKED", false);
        Set(s1, past ? (plant.ServiceMode ? "VENTED" : "SEALED") : plant.SocketState(1).ToUpperInvariant(), past ? !plant.ServiceMode : plant.SocketUsable(1));
        Set(s2, plant.SocketState(2).ToUpperInvariant(), !past && plant.SocketUsable(2));
        Set(r, plant.HatchObstructed ? "HELD OPEN" : plant.HatchOpen ? "OPEN" : "SEALED", !plant.HatchOpen);
        Set(pan, plant.PanLoaded ? "HATCH LIFTED" : plant.HatchObstructed ? "HATCH HELD" : "HATCH SEALED", !plant.HatchOpen);
        Set(f3, past ? (plant.FlangeRepaired ? "REPAIRED" : "CRACKED") : plant.FlangeRepaired ? "INTACT" : "BURST", plant.FlangeRepaired);
        Set(console, plant.Completed ? "RUNNING" : past ? "NORMAL" : plant.Ready ? "READY" : "NOT READY", plant.Completed || plant.Ready);
        foreach (var (plate, cell, future) in cellPlates)
        {
            bool usable = !future || plant.CellUsable(cell);
            Set(plate, usable ? "USABLE" : "BOILED OFF", usable);
        }
    }

    static void Set(Plate plate, string text, bool live)
    {
        if (plate.State.text != text) plate.State.text = text;
        plate.State.color = plate.Accent.color = live ? EquipmentPlate.Green : EquipmentPlate.Amber;
    }

    Plate Make(string name, Transform parent, Vector3 position, float yaw, Vector2 size, string heading, string detail, bool header = false)
    {
        roots.Add(EquipmentPlate.Create(name, parent, position, yaw, size, heading, detail, titleFont, bodyFont, header, out var state, out var accent));
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
    }

    void OnDestroy() { foreach (var root in roots) if (root != null) Destroy(root); }
}
