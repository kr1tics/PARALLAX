using UnityEditor;
using UnityEditor.SceneManagement;

public static class IntroIntegrationSetup
{
    [MenuItem("Tools/Context Intro/Open Team Main Menu")]
    public static void OpenMainMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/MainMenu.unity");
    }
}
