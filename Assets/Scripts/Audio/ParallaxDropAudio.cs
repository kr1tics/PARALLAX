using Parallax.Settings;
using UnityEngine;
/// <summary>Sound only after a released tutorial cube physically hits a surface.</summary>
public sealed class ParallaxDropAudio : MonoBehaviour
{
    ParallaxAudioPalette palette;
    Interact interaction;
    AudioSource source;
    bool armed;
    float releasedAt;
    public void Configure(ParallaxAudioPalette value, Interact owner)
    {
        palette = value; interaction = owner; source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 1; source.minDistance = 1.5f; source.maxDistance = 12;
        source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
    }
    public void Arm() { armed = true; releasedAt = Time.time; }
    void OnCollisionEnter(Collision collision)
    {
        if (!armed || GameSettingsMenu.GameplayInputBlocked || interaction.HeldObject == GetComponent<MovableObject>()) return;
        if (Time.time - releasedAt > 30) { armed = false; return; }
        if (collision.relativeVelocity.magnitude < .45f || collision.collider.GetComponentInParent<PlayerController>() != null) return;
        armed = false;
        if (palette.drop != null) source.PlayOneShot(palette.drop, palette.interactionVolume);
    }
}
