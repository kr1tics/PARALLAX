using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class Scene01GuidanceTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MonoBehaviour guidance, level;
        Rigidbody body;
        object gravity;
        Keyboard keyboard;
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
        string Hint => (string)guidance.GetType().GetProperty("RecoveryHint").GetValue(guidance);
        [UnitySetUp] public IEnumerator Setup()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            var root = GameObject.Find("Level 1 - Engineering Access");
            guidance = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Scene01Guidance");
            level = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Level1Controller");
            body = (Rigidbody)guidance.GetType().GetField("playerBody", Flags).GetValue(guidance);
            gravity = guidance.GetType().GetField("playerGravity", Flags).GetValue(guidance);
            // Isolate observer conditions; no puzzle methods or gravity mechanics are replaced.
            foreach (var component in body.GetComponents<MonoBehaviour>()) if (component.GetType().Name == "PlayerController") component.enabled = false;
            body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None;
            Set(gravity, "effective", false);
            body.position = new Vector3(3, 3, 9); body.transform.position = body.position; Physics.SyncTransforms();
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); } InputSystem.RemoveDevice(keyboard); Time.timeScale = 1; yield return null; }
        [UnityTest] public IEnumerator FlightHelpAppearsImmediatelyInObjectiveTwoAndHidesAfterCompletion()
        {
            yield return new WaitForSeconds(.2f); Assert.That(Hint, Is.Empty, "Objective 01 must not show this hint.");
            Set(level, "<IntroSolved>k__BackingField", true);
            yield return null; Assert.That(Hint, Does.Contain("Hold CTRL to descend while floating."));
            var help = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Single(x => x.name == "Hover recovery hint");
            Assert.That(help.rectTransform.anchorMax.y, Is.EqualTo(1));
            Assert.That(help.rectTransform.parent.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(28, 90)));
            help.ForceMeshUpdate(); Assert.That(help.isTextOverflowing, Is.False);
            Scene01PresentationTests.Capture(Camera.main, "objective02-flight-help-small", 960, 540);
            Scene01PresentationTests.Capture(Camera.main, "objective02-flight-help", 1280, 720);
            Scene01PresentationTests.Capture(Camera.main, "objective02-flight-help-wide", 1920, 1080);
            GameSettingsMenu.Instance.Open(); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(GameObject.Find("Scene 01 Objective UI").GetComponent<CanvasGroup>().alpha, Is.Zero);
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            InputSystem.Update(); yield return null; Assert.That(Hint, Is.Not.Empty, "Keep help visible while practicing descent.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            yield return new WaitForSeconds(.2f); Assert.That(Hint, Is.Not.Empty);
            Set(level, "<MainSolved>k__BackingField", true); yield return null; Assert.That(Hint, Is.Empty);
        }
        [UnityTest] public IEnumerator PauseIntroductionWaitsForGameplayAndDismissesAfterSettings()
        {
            var tip = guidance.GetType().GetField("pauseTip", Flags).GetValue(guidance) as TMP_Text;
            Set(guidance, "pauseTipElapsed", 0f);
            GameSettingsMenu.StartupInputBlocked = true;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That((float)guidance.GetType().GetField("pauseTipElapsed", Flags).GetValue(guidance), Is.Zero);
            GameSettingsMenu.StartupInputBlocked = false;
            yield return null;
            Assert.That(tip.gameObject.activeSelf, Is.True);
            tip.ForceMeshUpdate(); Assert.That(tip.isTextOverflowing, Is.False);
            Set(guidance, "pauseTipElapsed", 8f); yield return null;
            Assert.That(tip.gameObject.activeSelf, Is.False);
            Set(guidance, "pauseTipElapsed", 0f); yield return null;
            GameSettingsMenu.Instance.Open(); yield return null;
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            Assert.That(tip.gameObject.activeSelf, Is.False, "Do not repeat the introduction after Settings has been used.");
        }

        [UnityTest] public IEnumerator SettingsLauncherMovesToTopLeftAndRestoresItsOriginalPlacementOutsideTheScene()
        {
            var menu = GameSettingsMenu.Instance; var root = menu.GetComponent<UIDocument>().rootVisualElement;
            yield return new WaitForSecondsRealtime(.35f);
            var launcher = root.Q<Button>("open-settings");
            Assert.That(launcher.text, Is.EqualTo("Settings · Esc / P"));
            Assert.That(launcher.style.top.value.value, Is.EqualTo(24));
            Assert.That(launcher.style.left.value.value, Is.EqualTo(24));
            Assert.That(launcher.style.right.keyword, Is.EqualTo(StyleKeyword.Auto));
            Assert.That(launcher.style.bottom.keyword, Is.EqualTo(StyleKeyword.Auto));
            Assert.That(launcher.worldBound.yMin, Is.LessThan(root.layout.height * .5f));
            var text = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Single(x => x.name == "Contextual control hints");
            Assert.That(text.text, Does.Not.Contain("Settings").And.Not.Contain("Resume"));
            Assert.That(text.font.name, Is.EqualTo("SpaceMono-Regular SDF"));
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Assert.That(launcher.style.top.keyword, Is.EqualTo(StyleKeyword.Null));
            Assert.That(launcher.style.bottom.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
