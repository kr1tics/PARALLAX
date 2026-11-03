using UnityEngine;
using UnityEngine.SceneManagement;

public static class MemoryRecoveryFlow
{
    public const string Puzzle3Scene = "Level3_ColdStorage";
    static bool cyanQueued;
    public static string ResolveDestination(string source, string configured) =>
        source == "Level2_CargoBay" && configured == "Level3_MaintenanceShaft" ? Puzzle3Scene : configured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        cyanQueued = false;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool play = cyanQueued && mode == LoadSceneMode.Single && scene.name == "Level1_PowerRestoration";
        cyanQueued = false;
        if (play) MemoryRecoveryCinematic.Play(MemoryRecoveryStage.Signal);
    }

    public static bool Continue(string source, string destination)
    {
        if (source == "StartScene" && destination == "Level1_PowerRestoration")
        {
            cyanQueued = true;
            SceneManager.LoadScene(destination);
            return true;
        }
        if (source == "Level1_PowerRestoration" && destination == "Level2_CargoBay")
        {
            MemoryRecoveryCinematic.Play(MemoryRecoveryStage.Trace, destination);
            return true;
        }
        if (source == "Level2_CargoBay" && destination == Puzzle3Scene)
        {
            MemoryRecoveryCinematic.Play(MemoryRecoveryStage.Fragment, destination);
            return true;
        }
        // Cooling restored: the final level's Continue plays the closing memory, which ends at Main Menu.
        if (source == Puzzle3Scene && destination == "MainMenu")
        {
            PlayFinal();
            return true;
        }
        return false;
    }

    public static void PlayFinal() => MemoryRecoveryCinematic.Play(MemoryRecoveryStage.Restored, "MainMenu");
}
