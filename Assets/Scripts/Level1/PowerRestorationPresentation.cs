using UnityEngine;
using Parallax.Settings;
using TMPro;

// Scene-local presentation only. Movement, input, gravity and time travel stay in the existing systems.
[DefaultExecutionOrder(-100)]
public sealed class PowerRestorationPresentation : MonoBehaviour
{
    [SerializeField] private Look playerLook;
    [SerializeField] private ArtificialGravity playerGravity;
    [SerializeField] private Light[] roomLights;
    [SerializeField] private Interact interaction;
    [SerializeField] private TMP_FontAsset titleFont, supportingFont;
    private TimeState? displayedTime;
    private GameplayHud hud;
    private TMP_Text controls;

    private void Awake()
    {
        GlobalState.ResetForNewGame();
        playerLook.Rotate(new Vector2(90f, 0f));
    }

    private void Start()
    {
        var menu = GameSettingsMenu.Instance;
        menu.SetMainMenuContext(false);
        menu.SetGameplayControls(false, false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        hud = new GameplayHud(transform, "Power Restoration Objective UI", titleFont, supportingFont);
        controls = GameplayHud.CreateControlHint(hud.Instruction.GetComponentInParent<Canvas>().transform, supportingFont);
        SetObjective("OBJECTIVE 01", "Power Restoration", "Reactor offline. Survey the room.\nLocate Module A and Module B.");
    }

    public void SetObjective(string number, string title, string instruction) => hud.SetObjective(number, title, instruction);

    private void LateUpdate()
    {
        var time = GlobalState.get().now;
        hud.SetVisible(!GameSettingsMenu.GameplayInputBlocked);
        hud.Status.text = (time == TimeState.Past ? "2026 / PAST" : "2176 / FUTURE") +
            (playerGravity.IsGravityActive ? "  ·  GRAVITY ON" : "  ·  GRAVITY OFF");
        controls.text = "ESC / P — Settings / Resume" + (!playerGravity.IsGravityActive ? "    ·    Q — Toggle jetpack" : "");
        var cast = interaction.fireInteractRay(true);
        var target = cast.Item1 ? cast.Item2.collider.GetComponentInParent<Interactable>() : null;
        hud.Prompt.text = interaction.HeldObject != null ? "E — Place" :
            target is MovableObject movable && !movable.GetComponent<Rigidbody>().isKinematic ? "E — Pick up" :
            target is Switch ? (time == TimeState.Past ? "E — Toggle gravity" : "F — Switch Timeline") : "";
        if (displayedTime == time) return;
        foreach (var light in roomLights)
            light.intensity = time == TimeState.Future ? 1.4f : 2.6f;
        displayedTime = time;
    }

}
