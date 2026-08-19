using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class LoseGame : MonoBehaviour
{
    // ---------- EVENTS (other systems can subscribe) ----------
    public static event Action OnPlayAgainClicked;
    public static event Action OnMainMenuClicked;

    [Header("UI References")]
    [SerializeField] private GameObject losePanel;   // root Lose panel object
    [SerializeField] private Image BGImage;          // dark background overlay behind Lose panel

    [Header("Fade (WinPanel-style)")]
    [SerializeField] private CanvasGroup loseCanvasGroup;  // CanvasGroup on losePanel root
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Buttons")]
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Scenes")]
    [SerializeField] private string menuSceneName = "MenuScene";

    private void Awake()
    {
        // Make sure the Lose panel starts enabled & visible when this script is active.
        // (RevivePanel.ShowLosePanel() should activate it before we do anything.)
        if (losePanel != null && !losePanel.activeSelf)
            losePanel.SetActive(true);

        if (loseCanvasGroup != null)
        {
            // Start fully visible when the Lose panel pops up (Revive handles fade-in).
            loseCanvasGroup.alpha = 1f;
            loseCanvasGroup.blocksRaycasts = true;
            loseCanvasGroup.interactable = true;
        }
    }

    private void Start()
    {
        // Hook up button events
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgainButton);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuButton);
    }

    // --------------------------------------------------------------------
    // Button handlers (called by Unity UI Button.onClick)
    // --------------------------------------------------------------------

    private void OnPlayAgainButton()
    {
        // Broadcast event so other systems can react (save, analytics, etc.)
        OnPlayAgainClicked?.Invoke();

        // Fade out and reload the current stage
        string currentSceneName = SceneManager.GetActiveScene().name;
        StartCoroutine(FadeOutLoseAndLoadScene(currentSceneName));
    }

    private void OnMainMenuButton()
    {
        // Broadcast event so other systems can react
        OnMainMenuClicked?.Invoke();

        // Fade out and go to main menu
        StartCoroutine(FadeOutLoseAndLoadScene(menuSceneName));
    }

    // --------------------------------------------------------------------
    // Fade-out animation (WinPanel-style) then load scene
    // --------------------------------------------------------------------

    private IEnumerator FadeOutLoseAndLoadScene(string sceneName)
    {
        float t = 0f;

        float startPanelAlpha = loseCanvasGroup != null ? loseCanvasGroup.alpha : 1f;
        float startBgAlpha = BGImage != null ? BGImage.color.a : 1f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;          // unscaled, so it works while paused
            float k = Mathf.Clamp01(t / fadeDuration);

            // Panel fade
            if (loseCanvasGroup != null)
                loseCanvasGroup.alpha = Mathf.Lerp(startPanelAlpha, 0f, k);

            // BG fade
            if (BGImage != null)
            {
                Color c = BGImage.color;
                c.a = Mathf.Lerp(startBgAlpha, 0f, k);
                BGImage.color = c;
            }

            yield return null;
        }

        // Final snap + disable interaction
        if (loseCanvasGroup != null)
        {
            loseCanvasGroup.alpha = 0f;
            loseCanvasGroup.blocksRaycasts = false;
            loseCanvasGroup.interactable = false;
        }

        if (losePanel != null)
            losePanel.SetActive(false);

        // Ensure gameplay is unpaused for next scene
        GameplayPause.SetPaused(false);

        // Finally load the requested scene
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}



public class LoseGame1 : MonoBehaviour
{
    [Header("Fade (WinPanel-style)")]
    [SerializeField] private CanvasGroup loseCanvasGroup;
    [SerializeField] private float fadeDuration = 0.35f;


    // Start is called before the first frame update
    [SerializeField] GameObject revivePanel;
    [SerializeField] Button playAgainButton;
    [SerializeField] Button mainMenuButton;
    [SerializeField] GameObject losePanel;
    [SerializeField] Image BGImage;

  
    int levelNumer = 2;
    int desieredLevel = 20;
    private void Start()
    {
        revivePanel.SetActive(false);     
    }



    public void PlayAgain1()
    {
        losePanel.transform.DOScale(Vector3.zero, 0.8f)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               losePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
               }
               //SceneManager.LoadScene("Level1");
               var currentScene = SceneManager.GetActiveScene();

               // Reload it by build index
               SceneManager.LoadScene(currentScene.buildIndex);

           }
           );

    }

    public void MainMenu1()
    {
        losePanel.transform.DOScale(Vector3.zero, 0.8f)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               losePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
               }
               SceneManager.LoadScene("MenuScene");

           }
           );

    }

    public void PlayAgain()
    {
        // Fade out lose panel, then reload current scene
        var currentScene = SceneManager.GetActiveScene();
        StartCoroutine(FadeOutLoseAndLoadScene(currentScene.name));
    }

    public void MainMenu()
    {
        // Fade out lose panel, then go to menu
        StartCoroutine(FadeOutLoseAndLoadScene("MenuScene"));
    }


    private IEnumerator FadeOutLoseAndLoadScene(string sceneName)
    {
        float t = 0f;

        float startPanelAlpha = loseCanvasGroup != null ? loseCanvasGroup.alpha : 1f;
        float startBgAlpha = BGImage != null ? BGImage.color.a : 1f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);

            // Panel fade
            if (loseCanvasGroup != null)
                loseCanvasGroup.alpha = Mathf.Lerp(startPanelAlpha, 0f, k);

            // BG fade
            if (BGImage != null)
            {
                Color c = BGImage.color;
                c.a = Mathf.Lerp(startBgAlpha, 0f, k);
                BGImage.color = c;
            }

            yield return null;
        }

        // Final snap + disable interaction
        if (loseCanvasGroup != null)
        {
            loseCanvasGroup.alpha = 0f;
            loseCanvasGroup.blocksRaycasts = false;
            loseCanvasGroup.interactable = false;
        }

        if (losePanel != null)
            losePanel.SetActive(false);

        // Finally load the requested scene
        SceneManager.LoadScene(sceneName);
    }



}
