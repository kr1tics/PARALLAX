using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class CargoBayPlayModeTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        protected MonoBehaviour player, level;
        protected Rigidbody body;
        protected static object Get(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
        protected static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        protected static MonoBehaviour Component(GameObject obj, string name) => obj.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == name);

        [UnitySetUp] public IEnumerator Load()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1; GameSettingsMenu.StartupInputBlocked = false;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level2_CargoBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Level2_CargoBay");
#endif
            yield return null;
            player = Component(GameObject.Find("Player"), "PlayerController"); body = player.GetComponent<Rigidbody>();
            Call(Component(Camera.main.gameObject, "Look"), "Rotate", new Vector2(90, 0));
            yield return new WaitForSeconds(.5f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); }
            GameSettingsMenu.StartupInputBlocked = false; Time.timeScale = 1; yield return null;
        }

        protected IEnumerator Keys(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        [UnityTest] public IEnumerator LayoutOpensAndNormalMovementStaysOnDeck()
        {
            Assert.That(body.position.y, Is.InRange(-.2f, .3f));
            Vector3 before = body.position;
            yield return Keys(.45f, Key.W);
            Assert.That(body.position.z, Is.GreaterThan(before.z + 1), "W must move the real player. position=" + body.position + " velocity=" + body.linearVelocity + " blocked=" + GameSettingsMenu.GameplayInputBlocked + " intent=" + Get(player, "movementIntent") + " camera=" + Camera.main.transform.forward + " feet=" + ((Transform)Get(player, "feet")).position);
            Assert.That(body.position.y, Is.InRange(-.2f, .3f));
            Assert.That(Physics.Raycast(body.position, Vector3.up, 6), Is.True, "Roof prevents a jetpack bypass.");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(transform.GetComponents<MonoBehaviour>().Any(c => c == null), Is.False, transform.name);
        }
    }
}
