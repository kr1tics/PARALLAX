using Parallax.Settings;
using UnityEngine;

/// <summary>Final interaction on the existing console. Repair state remains owned by the modules.</summary>
public sealed class PowerReactor : Interactable
{
    [SerializeField] Interact player;
    [SerializeField] PowerRestorationPresentation presentation;
    [SerializeField] TutorialCompletionScreen completion;
    [SerializeField] TextMesh reactorLabel, consoleLabel;
    [SerializeField] Light poweredLight;
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
        "Power modules incomplete" : GlobalState.get().now != TimeState.Future ?
        "F — Return to the Future" : "E — Activate reactor";

    public override void onInteract(Interact owner)
    {
        if (owner != player || !CanActivate) return;
        IsOnline = true;
        RefreshFeedback();
        var palette = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        if (palette != null && palette.complete != null)
        {
            var audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.ignoreListenerPause = true;
            audio.PlayOneShot(palette.complete, palette.systemVolume);
        }
        // Acquire the shared modal lock immediately, before another input can open Settings.
        completion.TryShow();
    }

    public void RefreshFeedback()
    {
        bool ready = presentation.ReactorReady && GlobalState.get().now == TimeState.Future;
        reactorLabel.text = IsOnline ? "REACTOR / ONLINE" : "REACTOR / OFFLINE";
        consoleLabel.text = IsOnline ? "POWER / RESTORED" : ready ? "RESTART / READY" : "RESTART / LOCKED";
        reactorLabel.color = IsOnline ? new Color(.35f, 1, .65f) : Color.white;
        consoleLabel.color = IsOnline || ready ? new Color(.35f, 1, .65f) : Color.white;
        poweredLight.enabled = IsOnline;
        poweredLight.intensity = IsOnline ? 2.5f : 0;
    }
}
