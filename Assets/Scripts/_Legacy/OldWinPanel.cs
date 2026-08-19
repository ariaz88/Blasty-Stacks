using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class OldWinPanel : MonoBehaviour
{
    [SerializeField] Button threeXReward;
    [SerializeField] Button noThanksButton;
    [SerializeField] GameObject winPanel;
    [SerializeField] Image BGImage;
    [SerializeField] Image progressBar;
    [SerializeField] TextMeshProUGUI barText;
    [SerializeField] TextMeshProUGUI LevelCompleteText;
    //int coinCount = 250;
    //int gemCount = 5;
    //int hammerCount = 1;
    //int muliplier = 1;
    int levelNumer=1;
    int desieredLevel=20;
    private void Start()
    {
        //levelNumer = GameDataManager.instance.CurrentLevelNumber - 2;
        progressBar.fillAmount = levelNumer-1 / desieredLevel;


        //levelNumer = GameDataManager.instance.CurrentLevelNumber - 1;
        LevelCompleteText.text = "LVL." + levelNumer + " COMPLETED";

        StartCoroutine(ShowProgress());
    }
  
    IEnumerator ShowProgress()
    {
        yield return new WaitForSeconds(1f);
        //levelNumer = GameDataManager.instance.CurrentLevelNumber-1 ;

        progressBar.fillAmount = (float)levelNumer / desieredLevel;
        barText.text = levelNumer + "/" + desieredLevel;
    }

    private void AddRewards(int muliplier)
    {
        //GameDataManager.instance.AddGems(5* muliplier);
        //HammerManager.instance.SetHammerCount(1* muliplier);
        //GameDataManager.instance.AddCoins(250* muliplier);
        //GameDataManager.instance.AddToTotalCoins();
    }
    IEnumerator ShowAnimation(int multiplier)
    {
        yield return new WaitForSeconds(0.5f);
        //CoinManager.instance.AddGems(threeXReward.transform.position, 5* multiplier);
        //CoinManager.instance.AddCoins(threeXReward.transform.position, 40* multiplier);
        //UIManager.instance.UpdateUI();
    }

    public void ClaimThreeX()
    {
        AddRewards(3);
        winPanel.transform.DOScale(Vector3.zero, 0.7f)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               winPanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f; // Set alpha to 0
                    BGImage.color = color;
               }
               StartCoroutine(ShowAnimation(3));
               //GameManager.instance.gameEnded = true;
               //BoxSpawner.instance.stopSpawn = true;
               GoToNextLevel();

           }
           );
    }

    public void NoThanksClick()
    {
        AddRewards(1);


        winPanel.transform.DOScale(Vector3.zero, 0.7f)
            .SetEase(Ease.OutBack).OnComplete(()=>
            {
                winPanel.gameObject.SetActive(false);
                if (BGImage != null)
                {
                    Color color = BGImage.color;
                    color.a = 0f; // Set alpha to 0
                    BGImage.color = color;
                }
                StartCoroutine(ShowAnimation(1));
                //GameManager.instance.gameEnded = true;
                //BoxSpawner.instance.stopSpawn = true;
                GoToNextLevel();
            }
            );
        
    }

    private void GoToNextLevel()
    {
        //int nextLevelIndex = GameDataManager.instance.IncrementLevel();
        //int nextLevelIndex = GameDataManager.instance.CurrentLevelNumber;
        int nextLevelIndex = 5;

        // Get the total number of scenes
        int totalScenes = SceneManager.sceneCountInBuildSettings;

        if (nextLevelIndex < totalScenes)
        {
            SceneManager.LoadScene(nextLevelIndex+1);
        }
        else
        {
            Debug.LogWarning("Invalid scene index. No more levels available.");
            // Optionally, handle what to do if there are no more levels.
        }
    }

}
