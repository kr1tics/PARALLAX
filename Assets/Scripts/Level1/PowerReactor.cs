using System.Collections;
using Parallax.Settings;
using UnityEngine;

/// <summary>Final interaction on the existing console. Repair state remains owned by the modules. Starting the reactor
/// plays a short, visible start-up before the shared completion screen appears.</summary>
public sealed class PowerReactor : Interactable
{
    [SerializeField] Interact player;
    [SerializeField] PowerRestorationPresentation presentation;
    [SerializeField] TutorialCompletionScreen completion;
    [SerializeField] TextMesh reactorLabel, consoleLabel;
    [SerializeField] Light poweredLight;
    [SerializeField] Renderer core;
    [SerializeField] Material coreOnline;
    [SerializeField, Min(0)] float startUpSeconds = 2.2f;
    bool holdingInput;
    public bool IsOnline { get; private set; }

    public bool CanActivate
    {
        get
        {
            if (IsOnline || !presentation.ReactorReady || GlobalState.get().now != TimeState.Future ||
                GameSettingsMenu.GameplayInputBlocked || player.HeldObject != null) return false;
            var cast = player.fireInteractRay(true);
            return cast.Item1 && cast.Item2.collider.GetComponentInParent<PowerReactor>() == this;
        }
    }

    public string InteractionHint => IsOnline ? "Reactor online" : !presentation.ReactorReady ?
        "Power path incomplete" : GlobalState.get().now != TimeState.Future ?
        "Ready · Return to the Future" : "E — Activate reactor";

    public override void onInteract(Interact owner)
    {
        if (owner != player || !CanActivate) return;
        IsOnline = true;
        // Hold the shared input lock now, before another input can open Settings; it passes to the completion screen.
        GameSettingsMenu.StartupInputBlocked = true; holdingInput = true;
        poweredLight.intensity = .3f;
        RefreshFeedback();
        var palette = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        if (palette != null && palette.complete != null)
        {
            var audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.ignoreListenerPause = true;
            audio.PlayOneShot(palette.complete, palette.systemVolume);
        }
        StartCoroutine(StartUp());
    }

    IEnumerator StartUp()
    {
        for (float t = 0; t < startUpSeconds; t += Time.unscaledDeltaTime)
        {
            poweredLight.intensity = Mathf.Lerp(.3f, 2.5f, Mathf.SmoothStep(0, 1, t / (startUpSeconds * .7f)));
            if (t > .4f && core != null && core.sharedMaterial != coreOnline) core.sharedMaterial = coreOnline;
            yield return null;
        }
        poweredLight.intensity = 2.5f;
        // Same frame: release, then the completion screen takes the lock again before any input runs.
        ReleaseInput();
        completion.TryShow();
    }

    void ReleaseInput()
    {
        if (!holdingInput) return;
        holdingInput = false; GameSettingsMenu.StartupInputBlocked = false;
    }

    void OnDisable() => ReleaseInput();

    public void RefreshFeedback()
    {
        bool ready = presentation.ReactorReady && GlobalState.get().now == TimeState.Future;
        reactorLabel.text = IsOnline ? "REACTOR / ONLINE" : "REACTOR / OFFLINE";
        consoleLabel.text = IsOnline ? "POWER / RESTORED" : ready ? "RESTART / READY" : "RESTART / LOCKED";
        reactorLabel.color = IsOnline ? new Color(.35f, 1, .65f) : Color.white;
        consoleLabel.color = IsOnline || ready ? new Color(.35f, 1, .65f) : Color.white;
        poweredLight.enabled = IsOnline;
        // While online the start-up owns the intensity ramp.
        if (!IsOnline) poweredLight.intensity = 0;
    }
}
