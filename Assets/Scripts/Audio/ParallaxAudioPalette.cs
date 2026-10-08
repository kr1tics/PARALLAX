using UnityEngine;
[CreateAssetMenu(menuName = "PARALLAX/Audio Palette")]
public sealed class ParallaxAudioPalette : ScriptableObject
{
    [Header("Processed ambience")]
    public AudioClip menu, past, future, deviceHum;
    [Range(0,1)] public float menuVolume = .12f, pastVolume = .18f, futureVolume = .18f, humVolume = .10f;
    [Min(.1f)] public float crossfadeSeconds = 1.5f;
    [Header("Processed events — pickup/completion still candidates")]
    public AudioClip click, timeSwitch, gravityOff, pickup, drop, complete;
    [Range(0,1)] public float uiVolume = .45f, systemVolume = .4f, interactionVolume = .4f;
}
