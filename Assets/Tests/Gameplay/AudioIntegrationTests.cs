using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace Parallax.Gameplay.Tests
{
    public class AudioIntegrationTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            GameSettingsMenu.StartupInputBlocked = false; Time.timeScale = 1; AudioListener.pause = false;
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            Time.timeScale = 1; AudioListener.pause = false; yield return null;
        }
        [UnityTest] public IEnumerator ClassifiedClipsAndCollisionObserversArePresent()
        {
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            var palette = Resources.Load<ScriptableObject>("Audio/ParallaxAudioPalette"); Assert.That(palette, Is.Not.Null);
            foreach (string field in new[] { "menu", "past", "future", "deviceHum", "click", "timeSwitch", "gravityOff", "gravityOn", "pickup", "drop", "complete" })
            {
                var clip = (AudioClip)palette.GetType().GetField(field).GetValue(palette);
                Assert.That(clip, Is.Not.Null, field); Assert.That(clip.length, Is.GreaterThan(0), field);
            }
            var pickup = (AudioClip)palette.GetType().GetField("pickup").GetValue(palette);
            Assert.That(pickup.length, Is.LessThan(.6f), "Do not play the 19-second event collection.");
            var time = (AudioClip)palette.GetType().GetField("timeSwitch").GetValue(palette);
            Assert.That(time.length, Is.LessThan(.4f), "Use only one brief TV event.");
            var components = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int cubes = components.Count(x => x.GetType().Name == "MovableObject");
            Assert.That(cubes, Is.GreaterThanOrEqualTo(4));
            Assert.That(components.Count(x => x.GetType().Name == "ParallaxDropAudio"), Is.EqualTo(cubes), "Practice cubes also need impact audio.");
            Assert.That(GameObject.Find("Access relay device hum").GetComponent<AudioSource>().spatialBlend, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator TimelineAmbienceCrossfadesAndPausingRemainsFunctional()
        {
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return new WaitForSecondsRealtime(1.7f);
            var past = GameObject.Find("Primary ambience").GetComponent<AudioSource>();
            var future = GameObject.Find("Future ambience").GetComponent<AudioSource>();
            Assert.That(future.volume, Is.EqualTo(.18f).Within(.01f)); Assert.That(past.volume, Is.Zero);
            var observer = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == "ParallaxSceneAudio");
            var type = observer.GetType().Assembly.GetType("GlobalState"); var state = type.GetMethod("get").Invoke(null, null);
            type.GetMethod("SwapTime").Invoke(state, null); yield return new WaitForSecondsRealtime(.85f);
            Assert.That(past.volume, Is.GreaterThan(0).And.LessThan(.18f)); Assert.That(future.volume, Is.GreaterThan(0).And.LessThan(.18f));
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(past.volume, Is.EqualTo(.18f).Within(.01f)); Assert.That(future.volume, Is.Zero);
            GameSettingsMenu.Instance.Open(); yield return null;
            Assert.That(AudioListener.pause, Is.True); Assert.That(Time.timeScale, Is.Zero);
            Assert.That(past.ignoreListenerPause, Is.True); Assert.That(future.ignoreListenerPause, Is.True);
            GameSettingsMenu.Instance.Close(); yield return null; Assert.That(AudioListener.pause, Is.False);
        }
        [UnityTest] public IEnumerator MenuUsesItsOwnLowVolumeLoopAndUiSurvivesSettingsPause()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return new WaitForSecondsRealtime(1.7f);
            var source = GameObject.Find("Primary ambience").GetComponent<AudioSource>();
            Assert.That(source.clip.name, Is.EqualTo("Menu")); Assert.That(source.loop, Is.True); Assert.That(source.volume, Is.EqualTo(.12f).Within(.01f));
            Assert.That(source.ignoreListenerPause, Is.True);
            Assert.That(GameObject.Find("UI audio").GetComponent<AudioSource>().ignoreListenerPause, Is.True);
            GameSettingsMenu.Instance.Open(); yield return null; Assert.That(AudioListener.pause, Is.True);
            GameSettingsMenu.Instance.Close(); yield return null;
        }
    }
}
