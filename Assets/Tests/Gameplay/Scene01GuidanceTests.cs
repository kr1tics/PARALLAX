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
            Set(guidance, "hoverDelay", .1f); Set(gravity, "effective", false);
            body.position = new Vector3(3, 3, 9); body.transform.position = body.position; Physics.SyncTransforms();
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); } InputSystem.RemoveDevice(keyboard); Time.timeScale = 1; yield return null; }
        [UnityTest] public IEnumerator RecoveryOnlyAppearsForSustainedHoverInObjectiveTwoAndStopsOnDescent()
        {
            yield return new WaitForSeconds(.2f); Assert.That(Hint, Is.Empty, "Objective 01 must not show this hint.");
            Set(level, "<IntroSolved>k__BackingField", true);
            yield return null; Assert.That(Hint, Is.Empty, "Do not display immediately.");
            yield return new WaitForSeconds(.2f); Assert.That(Hint, Does.Contain("CTRL — Thrust down"));
            GameSettingsMenu.Instance.Open(); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(GameObject.Find("Scene 01 Objective UI").GetComponent<CanvasGroup>().alpha, Is.Zero);
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            InputSystem.Update(); yield return null; Assert.That(Hint, Is.Empty);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            yield return new WaitForSeconds(.2f); Assert.That(Hint, Is.Not.Empty);
            Set(level, "<MainSolved>k__BackingField", true); yield return null; Assert.That(Hint, Is.Empty);
        }
        [UnityTest] public IEnumerator SettingsLauncherMovesBelowStatusAndRestoresItsOriginalPlacementOutsideTheScene()
        {
            var menu = GameSettingsMenu.Instance; var root = menu.GetComponent<UIDocument>().rootVisualElement;
            yield return new WaitForSecondsRealtime(.35f);
            var launcher = root.Q<Button>("open-settings");
            Assert.That(launcher.style.top.keyword, Is.EqualTo(StyleKeyword.Auto));
            Assert.That(launcher.style.bottom.value.value, Is.EqualTo(24));
            Assert.That(launcher.worldBound.yMin, Is.GreaterThan(root.layout.height * .5f));
            var text = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Single(x => x.name == "Contextual control hints");
            Assert.That(text.text, Does.Contain("ESC / P — Settings / Resume"));
            Assert.That(text.font.name, Is.EqualTo("SpaceMono-Regular SDF"));
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Assert.That(launcher.style.top.keyword, Is.EqualTo(StyleKeyword.Null));
            Assert.That(launcher.style.bottom.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
