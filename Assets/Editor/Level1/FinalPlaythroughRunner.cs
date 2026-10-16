using UnityEditor;
using UnityEditor.SceneManagement;

public static class FinalPlaythroughRunner
{
    // Run without -quit; the same input-only verifier runs in the final player with --parallax-verify.
    public static void Run()
    {
        PowerRestorationValidation.Validate();
        EditorSceneManager.OpenScene("Assets/MainMenu.unity", OpenSceneMode.Single);
        SessionState.SetBool("Parallax.FinalVerification", true);
        EditorApplication.isPlaying = true;
    }
}
