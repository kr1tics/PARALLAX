using TMPro;
using UnityEngine;

/// <summary>Small unlit projections; text keeps its own font shader and never flickers.</summary>
public sealed class LevelHolograms : MonoBehaviour
{
    [SerializeField] private Level1Controller level;
    [SerializeField] private StorageInterlock interlock;
    [SerializeField] private Renderer storagePanel, consolePanel;
    [SerializeField] private TMP_Text storageText, consoleText, objectiveText;
    [SerializeField] private Transform objectiveMarker;
    [SerializeField] private Material guidance, locked, ready;
    private string previousStorage, previousConsole;
    private int previousStage = -1;
    private Camera view;

    void Start() => view = Camera.main;

    void LateUpdate()
    {
        if (view != null && (objectiveText.transform.position - view.transform.position).sqrMagnitude > 0.001f)
            objectiveText.transform.rotation = Quaternion.LookRotation(objectiveText.transform.position - view.transform.position);
        string storage = interlock.ClosingDeferred ? "STORAGE / SAFETY HOLD\nClear the hatch to lock\nExit remains open while inside" :
            interlock.IsOpen ? "STORAGE / DISENGAGED\nTake cube OUT before gravity ON" :
            "STORAGE / LOCKED\nGravity OFF at the PAST console";
        if (storage != previousStorage)
        {
            storageText.text = previousStorage = storage;
            storagePanel.sharedMaterial = interlock.IsOpen && !interlock.ClosingDeferred ? ready : locked;
        }
        bool past = GlobalState.get().now == TimeState.Past;
        bool gravityOn = GlobalState.get().gravity > 0.1f;
        string console = (past ? "GRAVITY CONSOLE [E]" : "CONSOLE OFFLINE / USE PAST") +
            "\nSTATION GRAVITY " + (gravityOn ? "ON" : "OFF") + "\nOFF opens storage / ON locks it";
        if (console != previousConsole)
        {
            consoleText.text = previousConsole = console;
            consolePanel.sharedMaterial = past ? guidance : locked;
        }
        int stage = level.Completed ? 4 : level.MainSolved ? 3 : level.StorageAccessGranted ? 2 : level.IntroSolved ? 1 : 0;
        if (stage == previousStage) return;
        previousStage = stage;
        objectiveMarker.gameObject.SetActive(stage != 4);
        if (stage == 4) return;
        Vector3[] targets = { new Vector3(2.5f, 1.25f, 13.45f), new Vector3(-2, 1.65f, 10.5f),
            new Vector3(-5.5f, 1.25f, 5), new Vector3(-13.3f, 1.25f, 4.45f) };
        objectiveMarker.position = targets[stage];
        objectiveText.text = new[] { "01", "GRAVITY", "02", "EXIT" }[stage];
    }
}
