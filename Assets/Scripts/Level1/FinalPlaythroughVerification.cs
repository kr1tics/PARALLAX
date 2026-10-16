using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

/// <summary>Opt-in player verification (--parallax-verify). Uses input, never sets puzzle flags or teleports objects.</summary>
public sealed class FinalPlaythroughVerification : MonoBehaviour
{
    Keyboard keyboard;
    Mouse mouse;
    Rigidbody player;
    Interact interaction;
    Look look;
    string output;
    bool finished;
    float timeout;
    static T Read<T>(object owner, string field, Type type = null) =>
        (T)(type ?? owner.GetType()).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        bool run = Environment.GetCommandLineArgs().Contains("--parallax-verify");
#if UNITY_EDITOR
        run |= UnityEditor.SessionState.GetBool("Parallax.FinalVerification", false);
        UnityEditor.SessionState.SetBool("Parallax.FinalVerification", false);
#endif
        if (run) new GameObject("Opt-in playthrough verification").AddComponent<FinalPlaythroughVerification>();
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        output = Application.isEditor ? ".utmp/stage5/playthrough" : Path.Combine(Application.dataPath, "../Verification");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "steps.txt"), "");
        File.WriteAllText(Path.Combine(output, "result.txt"), "RUNNING");
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
        timeout = Time.realtimeSinceStartup + 900;
        Application.logMessageReceived += OnLog;
        StartCoroutine(Run());
    }
    void Update() { if (!finished && Time.realtimeSinceStartup > timeout) Finish(false, "Overall playthrough timeout"); }
    void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    void Finish(bool success, string message)
    {
        if (finished) return;
        finished = true;
        Application.logMessageReceived -= OnLog;
        if (!success && player != null)
        {
            message += "\nPlayer " + player.position + "; timeline " + GlobalState.get().now + "; station gravity " + GlobalState.get().gravity;
            var tutorial = FindFirstObjectByType<Level1Controller>();
            if (tutorial != null)
                foreach (var body in Read<Rigidbody[]>(tutorial, "mainCubes"))
                    message += "\n" + body.name + " at " + body.position + "; velocity " + body.linearVelocity;
        }
        File.WriteAllText(Path.Combine(output, "result.txt"), (success ? "PASS: " : "FAIL: ") + message);
        Debug.Log("FINAL PLAYTHROUGH " + (success ? "PASSED: " : "FAILED: ") + message);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(success ? 0 : 1);
#else
        Application.Quit(success ? 0 : 1);
#endif
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    void Mark(string message) { Debug.Log("PLAYTHROUGH: " + message); File.AppendAllText(Path.Combine(output, "steps.txt"), message + "\n"); }
    void BindPlayer()
    {
        player = GameObject.Find("Player").GetComponent<Rigidbody>();
        interaction = player.GetComponent<Interact>(); look = Camera.main.GetComponent<Look>();
    }
    void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    IEnumerator Tap(Key key) { Keys(key); yield return null; yield return null; Keys(); yield return null; yield return null; }
    IEnumerator Until(Func<bool> condition, string message, float seconds = 8)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Require(condition(), message);
    }
    IEnumerator Swap()
    {
        var previous = GlobalState.get().now;
        yield return Tap(Key.F); yield return new WaitForSeconds(1.1f);
        Require(GlobalState.get().now != previous, "F must swap timelines");
    }
    void Face(Vector3 target)
    {
        var direction = target - Camera.main.transform.parent.position;
        float horizontal = new Vector2(direction.x, direction.z).magnitude;
        look.Rotate(new Vector2(Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg,
            Mathf.Clamp(direction.y / Mathf.Max(horizontal, .01f) * .25f, -.5f, .5f)));
    }
    IEnumerator Move(Vector3 target, Func<bool> stop = null)
    {
        Mark("Move " + player.position + " -> " + target);
        look.Rotate(new Vector2(90, 0));
        float deadline = Time.realtimeSinceStartup + 30;
        while ((Vector3.Distance(player.position, target) > .12f || player.linearVelocity.magnitude > .18f) && !(stop?.Invoke() ?? false))
        {
            Require(Time.realtimeSinceStartup < deadline, "Movement blocked: " + player.position + " -> " + target);
            var desired = Vector3.ClampMagnitude((target - player.position) * 1.8f, 2f);
            var thrust = (desired - player.linearVelocity) * 4;
            var keys = new System.Collections.Generic.List<Key>(3);
            if (Mathf.Abs(thrust.x) > .3f) keys.Add(thrust.x > 0 ? Key.D : Key.A);
            if (Mathf.Abs(thrust.z) > .3f) keys.Add(thrust.z > 0 ? Key.W : Key.S);
            if (Mathf.Abs(thrust.y) > .3f) keys.Add(thrust.y > 0 ? Key.Space : Key.LeftCtrl);
            Keys(keys.ToArray()); yield return null;
        }
        Keys(); yield return new WaitForSecondsRealtime(.25f);
    }
    IEnumerator Grab(MovableObject item)
    {
        Face(item.transform.position); yield return null; yield return Tap(Key.E);
        Require(interaction.HeldObject == item, "Could not grab " + item.name + " from " + player.position);
    }
    IEnumerator CarryTo(MovableObject item, Vector3 destination)
    {
        var grabPoint = Read<Vector3>(item, "localGrabPoint", typeof(MovableObject));
        float distance = Read<float>(item, "grabDistance", typeof(MovableObject));
        Vector3 carrier = destination + item.transform.TransformVector(grabPoint) - Vector3.up * .5f - Vector3.forward * (distance + .25f);
        yield return Move(carrier); yield return new WaitForSeconds(.5f);
        Require(interaction.HeldObject == item, "Carry was lost en route");
    }
    IEnumerator Click(string label)
    {
        var button = GameObject.Find(label).GetComponent<UnityEngine.UI.Button>();
        Require(button.IsInteractable(), label + " must be interactable");
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
        yield return MouseClick(point);
    }
    IEnumerator MouseClick(Vector2 point)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
    }
    IEnumerator MenuPlay()
    {
        var root = GameObject.Find("Main Menu").GetComponent<UIDocument>().rootVisualElement;
        var point = root.Q<Button>("play").worldBound.center;
        yield return MouseClick(new Vector2(point.x * Screen.width / root.worldBound.width, Screen.height - point.y * Screen.height / root.worldBound.height));
        yield return Until(() => SceneManager.GetActiveScene().name == "StartScene", "Main Menu Play failed");
        yield return new WaitForSecondsRealtime(1.5f); yield return Tap(Key.Escape);
        yield return Until(() => !GameSettingsMenu.GameplayInputBlocked, "Intro failed to release input", 20);
        BindPlayer();
    }
    IEnumerator Snapshot(string name)
    {
        // Player screenshots include the actual screen-space UI. Editor snapshots are covered by UI tests.
        if (Application.isEditor) yield break;
        foreach (int width in new[] { 1280, 1920 })
        {
            Screen.SetResolution(width, width * 9 / 16, false); yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + "-" + width + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.7f);
        Require(SceneManager.GetActiveScene().name == "MainMenu", "Build must start at Main Menu");
        yield return Snapshot("main-menu"); yield return MenuPlay(); Mark("Main Menu -> tutorial via Play and intro skip");
        var tutorial = FindFirstObjectByType<Level1Controller>();
        if (!Debug.isDebugBuild)
        {
            Keys(Key.LeftShift, Key.K); yield return null; yield return null; Keys(); yield return null;
            Require(!tutorial.GetComponent<TutorialCompletionScreen>().IsOpen, "Release player must ignore the debug completion shortcut");
        }
        yield return Tap(Key.Q); yield return Swap();
        var introCube = Read<Rigidbody[]>(tutorial, "introCubes")[0].GetComponent<MovableObject>();
        yield return Move(new Vector3(2, 1, 14.4f)); yield return Move(new Vector3(2, .5f, 14.4f));
        yield return Grab(introCube); yield return Move(new Vector3(2, 1.2f, 14.4f));
        yield return CarryTo(introCube, new Vector3(2.5f, .8f, 13.45f)); yield return Tap(Key.E);
        yield return Swap(); yield return Until(() => tutorial.IntroSolved, "Access relay did not solve"); Mark("Tutorial access relay solved through physical carry and causality");
        yield return Move(new Vector3(2.5f, 1, 13.45f)); yield return Move(new Vector3(-2, 1, 13.45f));
        yield return Move(new Vector3(-.95f, .8f, 13.45f)); yield return Move(new Vector3(-.95f, .8f, 8.8f));
        yield return Move(new Vector3(-2, .05f, 8.8f)); yield return Swap();
        Face(new Vector3(-2, .35f, 10.5f)); yield return Tap(Key.E);
        Require(tutorial.StorageReleased, "Gravity console must release storage");
        yield return Move(new Vector3(-6, .6f, 10.3f)); yield return Move(new Vector3(-6, .6f, 13.2f));
        var mainCube = Read<Rigidbody[]>(tutorial, "mainCubes")[0].GetComponent<MovableObject>();
        yield return Grab(mainCube); yield return Move(new Vector3(-6, 1.2f, 13.2f));
        yield return Move(new Vector3(-6, 1.2f, 10)); yield return CarryTo(mainCube, new Vector3(-5.5f, .8f, 5));
        yield return Until(() => mainCube.GetComponent<Rigidbody>().linearVelocity.magnitude < .02f, "Wait for the weightless cube to settle before releasing it");
        Mark("Engineering cube released at " + mainCube.transform.position); yield return Tap(Key.E);
        yield return Move(new Vector3(-2, .8f, 2.75f)); // Leave sideways so the player does not push the weightless cube off its plate.
        yield return Move(new Vector3(-2, .05f, 8.8f)); Face(new Vector3(-2, .35f, 10.5f)); yield return Tap(Key.E);
        Require(GlobalState.get().gravity > 4.5f, "Console must restore station gravity");
        yield return new WaitForSeconds(1);
        yield return Swap(); yield return Until(() => tutorial.MainSolved, "Engineering relay did not solve");
        yield return Move(new Vector3(-7.5f, .3f, 4.45f)); yield return Move(new Vector3(-13.3f, .05f, 4.45f), () => tutorial.Completed);
        yield return Until(() => tutorial.GetComponent<TutorialCompletionScreen>().IsReady, "Tutorial completion missing");
        Mark("Both tutorial puzzles and exit completed"); yield return Snapshot("tutorial-complete"); yield return Click("CONTINUE");
        yield return Until(() => SceneManager.GetActiveScene().name == "Level1_PowerRestoration", "Continue destination wrong");
        BindPlayer(); Mark("Continue -> Power Restoration"); yield return RestorePower();
        yield return Click("RESTART LEVEL"); yield return null; yield return null; BindPlayer();
        Require(!FindFirstObjectByType<PowerModuleA>().IsRepaired && !FindFirstObjectByType<PowerModuleB>().IsRepaired && !FindFirstObjectByType<PowerReactor>().IsOnline, "Restart must clear all progress");
        Mark("Restart resets module/reactor state"); yield return RestorePower();
        yield return Click("MAIN MENU"); yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu", "Main Menu navigation failed");
        Require(Cursor.visible && !GameSettingsMenu.StartupInputBlocked && Time.timeScale == 1, "Main Menu input state wrong");
        yield return MenuPlay(); Require(!FindFirstObjectByType<Level1Controller>().Completed, "New tutorial must start fresh");
        Finish(true, "Menu, both tutorial puzzles, Continue, both modules, reactor, completion, restart, Main Menu and new game via normal input.");
    }
    IEnumerator RestorePower()
    {
        Require(GlobalState.get().now == TimeState.Future, "New Level 1 starts in Future");
        yield return Snapshot("level1-entry"); yield return Tap(Key.Q); yield return Swap();
        var a = FindFirstObjectByType<PowerModuleA>(); var b = FindFirstObjectByType<PowerModuleB>();
        yield return Move(new Vector3(-5.8f, 1.05f, -2.6f)); yield return Grab(a);
        yield return Tap(Key.Escape); Require(GameSettingsMenu.Instance.IsOpen, "Pause while carrying failed");
        Require(interaction.HeldObject == a, "Pause lost held module"); yield return Snapshot("pause-holding"); yield return Tap(Key.Escape);
        yield return Tap(Key.Q); yield return Tap(Key.Q);
        Require(!player.GetComponent<ArtificialGravity>().effective && interaction.HeldObject == a, "Rapid Q toggles lost flight or held module");
        yield return Move(new Vector3(-5.8f, 3, -1.5f)); yield return CarryTo(a, Read<Vector3>(a, "alignmentPosition"));
        yield return Until(() => a.CanInstall, "Module A did not align at " + a.transform.position + " player " + player.position, 3);
        yield return Tap(Key.E); Require(a.IsRepaired, "Module A installation failed"); Mark("Module A carried, aligned in Zero-G and installed");
        yield return Move(new Vector3(-6, 3, -3)); yield return Move(new Vector3(6.5f, 3, -3));
        yield return Move(new Vector3(6.5f, 11.3f, 1.4f)); Face(b.transform.position); yield return Tap(Key.E);
        Require(b.IsRepaired, "Module B repair failed"); Mark("Module B reached with Zero-G and repaired");
        yield return Swap(); Require(a.IsRepaired && b.IsRepaired, "Temporal repairs lost");
        yield return Move(new Vector3(6.5f, 1.1f, 1.2f)); yield return Move(new Vector3(3.5f, 1.05f, 1.2f));
        yield return Tap(Key.Q); Face(FindFirstObjectByType<PowerReactor>().transform.position);
        yield return Snapshot("reactor-ready"); yield return Tap(Key.E);
        yield return Until(() => FindFirstObjectByType<TutorialCompletionScreen>().IsReady, "Level completion missing");
        Require(FindFirstObjectByType<PowerReactor>().IsOnline && Cursor.visible && GameSettingsMenu.GameplayInputBlocked, "Completion state wrong");
        Mark("Future reactor activated; Level Complete locks gameplay"); yield return Snapshot("level1-complete");
    }
}
