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

public class ContextIntroIntegrationTests
{
    private MonoBehaviour intro;
    private Keyboard keyboard;
    private InputSettings.BackgroundBehavior background;
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
    private object Field(string name) => intro.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(intro);
    private bool Playing => intro != null && (bool)intro.GetType().GetProperty("IsPlaying").GetValue(intro);
    private void Skip() => intro.GetType().GetMethod("Skip").Invoke(intro, null);
    private MonoBehaviour Find(string name) => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(x => x.GetType().Name == name);

    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale = 1;
        background = InputSystem.settings.backgroundBehavior;
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (intro != null) Object.Destroy(intro.gameObject);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = background;
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        yield return null; Time.timeScale = 1;
    }
    private IEnumerator Play()
    {
        Find("MainMenuController").GetType().GetMethod("Play").Invoke(Find("MainMenuController"), null);
        float deadline = Time.realtimeSinceStartup + 20;
        while (SceneManager.GetActiveScene().name != "StartScene" && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        Assert.AreEqual("StartScene", SceneManager.GetActiveScene().name);
        intro = Find("ContextIntro");
        Assert.IsTrue(Playing);
        Assert.IsTrue(GameSettingsMenu.GameplayInputBlocked);
        Assert.AreEqual(0, Time.timeScale);
        Assert.IsNotNull(Find("Level1Controller"));
        Assert.IsNotNull(Find("LevelHolograms"));
        Assert.IsFalse((bool)Field("showStartGameButton"));
        Assert.IsFalse(((GameObject)Field("startButton")).activeSelf, "Do not show a second Start Game entry.");
    }
    private IEnumerator WaitForGameplay()
    {
        float deadline = Time.realtimeSinceStartup + 16;
        while (Playing && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(Playing); Assert.IsFalse(GameSettingsMenu.GameplayInputBlocked);
        Assert.AreEqual(1, Time.timeScale);
        Assert.IsFalse(((GameObject)Field("canvasObject")).activeSelf);
        var global = intro.GetType().Assembly.GetType("GlobalState");
        Assert.AreEqual("Future", global.GetField("now").GetValue(global.GetMethod("get").Invoke(null, null)).ToString());
    }
    [UnityTest] public IEnumerator MenuPlayShowsStoryThenRevealsCurrentTutorial()
    {
        yield return new WaitForSecondsRealtime(.2f);
        Assert.IsFalse(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x.GetType().Name == "ContextIntro"));
        yield return Play();
        float started = Time.realtimeSinceStartup;
        yield return new WaitForSecondsRealtime(1.5f);
        var caption = Field("caption"); var type = caption.GetType();
        type.GetMethod("ForceMeshUpdate").Invoke(caption, new object[] { true, true });
        Assert.AreEqual("Oxanium-Regular SDF", ((Object)Field("font")).name);
        Assert.AreEqual("SpaceMono-Regular SDF", ((Object)Field("metadataFont")).name);
        var info = type.GetProperty("textInfo").GetValue(caption);
        int count = (int)info.GetType().GetField("characterCount").GetValue(info);
        var chars = (Array)info.GetType().GetField("characterInfo").GetValue(info);
        Assert.AreEqual(4, count);
        for (int i = 0; i < count; i++) Assert.AreSame(Field("font"), chars.GetValue(i).GetType().GetField("fontAsset").GetValue(chars.GetValue(i)));
        yield return WaitForGameplay();
        Assert.That(Time.realtimeSinceStartup - started, Is.InRange(12, 15));
    }
    [UnityTest] public IEnumerator EscapeSkipsWithoutOpeningSettings()
    {
        keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        yield return Play();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null; yield return null;
        Assert.IsFalse(GameSettingsMenu.Instance.IsOpen);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return WaitForGameplay();
        Assert.Less((float)Field("introElapsed"), 4);
    }
    [UnityTest] public IEnumerator SkipButtonAndMenuReplayWork()
    {
        yield return Play();
        var button = ((GameObject)Field("skipButton")).GetComponent("Button");
        var click = button.GetType().GetProperty("onClick").GetValue(button);
        click.GetType().GetMethod("Invoke").Invoke(click, null);
        yield return WaitForGameplay();
        Assert.Less((float)Field("introElapsed"), 4);
        yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
        Assert.IsFalse(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x.GetType().Name == "ContextIntro"));
        yield return Play(); Skip(); yield return WaitForGameplay();
    }
}
