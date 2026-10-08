using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Queue the story only for an explicit Play request, without editing the tutorial scene.</summary>
public static class ContextIntroFlow
{
    private static string pendingScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        pendingScene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void Request(string gameplayScene) => pendingScene = gameplayScene;
    public static void Cancel() => pendingScene = null;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingScene == null) return;
        string expected = pendingScene;
        pendingScene = null;
        if (mode != LoadSceneMode.Single || scene.name != expected) return;
        var prefab = Resources.Load<GameObject>("UI/ContextIntro");
        if (prefab == null)
        {
            Debug.LogError("Story intro prefab UI/ContextIntro is missing.");
            return;
        }
        var instance = Object.Instantiate(prefab);
        instance.name = "Context Intro";
        SceneManager.MoveGameObjectToScene(instance, scene);
        instance.GetComponent<ContextIntro>().StartGame();
    }
}
