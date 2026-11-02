using System.Collections;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared tutorial, Power Restoration and Cargo Bay completion presentation; never solves puzzles.</summary>
[DefaultExecutionOrder(400)]
public sealed class TutorialCompletionScreen : MonoBehaviour
{
    [SerializeField] Level1Controller level;
    [SerializeField] PowerReactor reactor;
    [SerializeField] CargoBayController cargoLevel;
    [SerializeField] TMP_FontAsset primaryFont, secondaryFont;
    [Tooltip("Next scene name or path from Build Settings. Empty/unavailable destinations return to MainMenu.")]
    [SerializeField] string nextScene;
    [SerializeField] string completionTitle = "TUTORIAL COMPLETE";
    [SerializeField] string completionDescription = "You've learned the basics of time manipulation and gravity control.";
    [SerializeField] string finalMessage = "The investigation begins now.";
    [SerializeField] string replayLabel = "REPLAY TUTORIAL";
    [SerializeField, Min(0)] float completionDelay = 1f;
    [SerializeField, Min(.01f)] float fadeDuration = .8f;
    [Header("Tutorial shortcut")]
    [Tooltip("Editor/development builds only: Shift + K previews completion without solving puzzles.")]
    [SerializeField] bool allowCompletionShortcut = true;
    CanvasGroup overlay, copy, actions;
    GameObject screen;
    Button replayButton;
    bool triggered, ownsLock, navigating;
    float previousScale;
    bool previousCursorVisible;
    CursorLockMode previousCursorLock;
    public bool IsOpen => ownsLock;
    public bool IsReady => actions != null && actions.interactable;

    void LateUpdate() => TryShow();

    public void TryShow()
    {
        if (!isActiveAndEnabled || triggered || GameSettingsMenu.GameplayInputBlocked) return;
        bool shortcut = Debug.isDebugBuild && reactor == null && cargoLevel == null && allowCompletionShortcut && Keyboard.current != null && Keyboard.current.shiftKey.isPressed && Keyboard.current.kKey.wasPressedThisFrame;
        bool completed = cargoLevel != null ? cargoLevel.Completed : reactor != null ? reactor.IsOnline : level != null && level.Completed;
        if (!completed && !shortcut) return;
        ShowCompletion();
    }
    // Called by a level once its own completion condition is met; TryShow remains the polling path.
    public void ShowCompletion()
    {
        if (!isActiveAndEnabled || triggered || GameSettingsMenu.GameplayInputBlocked) return;
        triggered = true;
        previousScale = Time.timeScale;
        previousCursorVisible = Cursor.visible; previousCursorLock = Cursor.lockState;
        ownsLock = true;
        GameSettingsMenu.StartupInputBlocked = true;
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        StartCoroutine(Reveal());
    }
    IEnumerator Reveal()
    {
        yield return new WaitForSecondsRealtime(completionDelay);
        Build();
        float elapsed = 0;
        while (elapsed < fadeDuration + .65f)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / fadeDuration));
            copy.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .2f) / fadeDuration));
            actions.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .65f) / fadeDuration));
            yield return null;
        }
        overlay.alpha = copy.alpha = actions.alpha = 1;
        actions.interactable = true;
        EventSystem.current?.SetSelectedGameObject(replayButton.gameObject);
    }
    void Build()
    {
        screen = new GameObject(reactor != null ? "Power Restoration Completion Screen" : "Tutorial Completion Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        screen.transform.SetParent(transform, false);
        var canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = screen.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        overlay = screen.GetComponent<CanvasGroup>(); overlay.alpha = 0;
        var shade = Rect(screen.transform, "Dark overlay", Vector2.zero, Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
        shade.gameObject.AddComponent<Image>().color = new Color(.015f, .02f, .025f, .84f);
        var content = Rect(screen.transform, "Completion content", new Vector2(0, 10), new Vector2(760, 350));
        copy = content.gameObject.AddComponent<CanvasGroup>(); copy.alpha = 0;
        // Power Restoration keeps its own copy (its scene does not serialize the text fields); other scenes configure theirs.
        bool destinationMissing = string.IsNullOrWhiteSpace(nextScene) || !Application.CanStreamedLevelBeLoaded(nextScene);
        Text(content, "Completion title", reactor != null ? "POWER RESTORED" : completionTitle, primaryFont, 44, 115, 72, true);
        Text(content, "Completion description", reactor != null ? "Both modules repaired. Reactor successfully activated." : completionDescription, secondaryFont, 22, 32, 88, false);
        Text(content, "Completion final message", destinationMissing ? "The next level is not available in this build." : reactor != null ? "Level 1 complete." : finalMessage, secondaryFont, 23, -66, 50, false);
        var row = Rect(screen.transform, "Completion actions", new Vector2(0, -155), new Vector2(520, 48));
        actions = row.gameObject.AddComponent<CanvasGroup>(); actions.alpha = 0; actions.interactable = false;
        replayButton = MakeButton(row, reactor != null ? "RESTART LEVEL" : replayLabel, -138, RestartLevel);
        MakeButton(row, destinationMissing ? "MAIN MENU" : "CONTINUE", 138, Continue);
    }
    static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    void Text(Transform parent, string name, string value, TMP_FontAsset font, float size, float y, float height, bool primary)
    {
        var rect = Rect(parent, name, new Vector2(0, y), new Vector2(740, height));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.font = font;
        text.fontSize = size; text.fontStyle = FontStyles.Normal; text.fontWeight = FontWeight.Regular;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.color = primary ? new Color32(220, 228, 232, 255) : new Color32(139, 146, 152, 255);
    }
    Button MakeButton(Transform parent, string label, float x, UnityEngine.Events.UnityAction callback)
    {
        var rect = Rect(parent, label, new Vector2(x, 0), new Vector2(244, 48));
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .14f, .15f, .85f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.8f, .8f, .8f); button.colors = colors;
        Text(rect, label + " label", label, primaryFont, 18, 0, 42, true);
        button.onClick.AddListener(callback); return button;
    }
    public void ReplayTutorial() => RestartLevel();
    public void RestartLevel()
    {
        if (!IsReady || navigating) return;
        Navigate(gameObject.scene.name);
    }
    public void Continue()
    {
        if (!IsReady || navigating) return;
        // A forthcoming level need not exist in this build; keep the completed level navigable.
        string destination = !string.IsNullOrWhiteSpace(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene) ? nextScene : "MainMenu";
        Navigate(destination);
    }
    void Navigate(string destination)
    {
        if (!Application.CanStreamedLevelBeLoaded(destination)) return;
        navigating = true; actions.interactable = false;
        ContextIntroFlow.Cancel();
        Release();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(destination);
    }
    void Release()
    {
        if (!ownsLock) return;
        ownsLock = false; GameSettingsMenu.StartupInputBlocked = false;
        Time.timeScale = previousScale;
        Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
    }
    void OnDisable()
    {
        StopAllCoroutines(); Release();
        if (screen != null) Destroy(screen);
    }
}
