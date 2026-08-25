using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-loads the target scene from a small Preload scene.
/// - Waits one frame (so managers in Preload can initialize)
/// - Optionally waits a short delay (for logos, etc.)
/// - Loads Menu (Single by default), or Additive + unloads Preload
/// 
/// Attach to any GameObject in your Preload scene.
/// </summary>

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MenuLoader : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string targetSceneName = "MenuScene";   // exact name in Build Settings

    [Header("Timing")]
    [Tooltip("Extra delay before loading (seconds). Set 0 for immediate.")]
    [SerializeField] private float delaySeconds = 0.0f;

    [Tooltip("Minimum time (in seconds) the loading bar should take to reach 100%.")]
    [SerializeField] private float minLoadTime = 8.0f;

    [Header("Mode")]
    [Tooltip("If true, loads additively then unloads the Preload scene.")]
    [SerializeField] private bool loadAdditiveThenUnloadPreload = false;

    [Header("Loading UI")]
    [SerializeField] private Image loadingFillImage;   // Image type = Filled
    [SerializeField] private TMP_Text loadingText;     // e.g. "45%"

    [Header("First-run tutorial")]
    [Tooltip("The tutorial that must be seen before the menu. Leave empty to always go " +
             "straight to targetSceneName.")]
    [SerializeField] private TutorialSequenceSO firstRunTutorial;

    [Tooltip("Scene that plays that tutorial. Its TutorialTrigger is what sends the " +
             "player on to the menu afterwards.")]
    [SerializeField] private string firstRunTutorialScene = "Tutorial_Board_01";

    private IEnumerator Start()
    {
        // 1) give managers a frame to run Awake/Start (GameStartManager, CurrencyManager, etc.)
        yield return null;

        // 1b) First launch only: detour through the tutorial scene instead of the
        // menu. The flag lives in SaveSystem, so this happens exactly once per
        // save - clear it with Tools/Tutorial/Reset Tutorial Progress to test again.
        if (firstRunTutorial && !string.IsNullOrEmpty(firstRunTutorialScene)
            && !TutorialManager.IsTutorialDone(firstRunTutorial.TutorialId))
        {
            Debug.Log($"[MenuLoader] First run: routing to tutorial scene '{firstRunTutorialScene}'.");
            targetSceneName = firstRunTutorialScene;
        }

        // 2) optional delay (e.g. splash logo)
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        // 3) start async load so we can show progress
        AsyncOperation loadOp;

        if (!loadAdditiveThenUnloadPreload)
        {
            // Single-mode: replaces Preload completely after loading
            loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        }
        else
        {
            // Additive: keep Preload while loading Menu, then unload Preload
            loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        }

        // We control when the new scene is activated so we can finish our 8s bar
        loadOp.allowSceneActivation = false;

        float timer = 0f;

        while (!loadOp.isDone)
        {
            timer += Time.deltaTime;

            // Scene loading progress goes from 0 -> 0.9 while allowSceneActivation = false
            float sceneProgress = Mathf.Clamp01(loadOp.progress / 0.9f);

            // Time-based progress to force at least minLoadTime seconds
            float timeProgress = Mathf.Clamp01(timer / minLoadTime);

            // Displayed progress: we only show the smaller of the two,
            // so the bar never shows 100% before both loading and time are done.
            float displayProgress = Mathf.Min(sceneProgress, timeProgress);

            // Update UI
            if (loadingFillImage != null)
                loadingFillImage.fillAmount = displayProgress;

            if (loadingText != null)
                loadingText.text = Mathf.RoundToInt(displayProgress * 100f) + "%";

            // When both scene is loaded and min time passed, go to the next scene
            if (displayProgress >= 1f)
            {
                loadOp.allowSceneActivation = true;
            }

            yield return null;
        }

        // If you are using additive mode, unload the Preload scene after Menu is active
        if (loadAdditiveThenUnloadPreload)
        {
            var menuScene = SceneManager.GetSceneByName(targetSceneName);
            if (menuScene.IsValid())
                SceneManager.SetActiveScene(menuScene);

            var preloadScene = gameObject.scene;
            yield return SceneManager.UnloadSceneAsync(preloadScene);
        }
    }
}
