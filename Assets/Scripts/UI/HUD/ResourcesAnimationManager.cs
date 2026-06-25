using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ResourcesAnimationManager : MonoBehaviour
{
    public static ResourcesAnimationManager instance;

    [Header("UI References")]
    [SerializeField] private GameObject canvas;
    [SerializeField] private GameObject animatedCoinPrefab;
    [SerializeField] private GameObject animatedGemPrefab;
    [SerializeField] private GameObject animatedXPPrefab;
    [SerializeField] RectTransform coinTargetUI;
    [SerializeField] RectTransform gemTargetUI;
    [SerializeField] RectTransform XPTargetUI;
    //[SerializeField] RectTransform coinStartPos;
    //[SerializeField] RectTransform gemStartPos;
    //[SerializeField] RectTransform XPStartPos;

    [Header("Available Items")]
    [SerializeField] int MaxCoin;
    Queue<GameObject> coinsQueue = new Queue<GameObject>();
    Queue<GameObject> gemsQueue = new Queue<GameObject>();
    Queue<GameObject> XPQueue = new Queue<GameObject>();

    [SerializeField] Ease easeType;
    [SerializeField] float spread = 0.15f;
    float minAnimDuration = 0.6f;
    float maxAnimDuration = 0.9f;

    private void Awake()
    {
        instance = this;

        PrepareItems(animatedCoinPrefab, coinsQueue, MaxCoin);
        PrepareItems(animatedGemPrefab, gemsQueue, MaxCoin); // Initialize gems queue with MaxCoin items
        PrepareItems(animatedXPPrefab, XPQueue, MaxCoin); // Initialize gems queue with MaxCoin items
    }

    private void PrepareItems(GameObject prefab, Queue<GameObject> queue, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject item = Instantiate(prefab, transform);
            item.SetActive(false);
            queue.Enqueue(item);
        }
    }

    private void Animate1(Vector3 collectedItemPosition, RectTransform targetUI, Queue<GameObject> itemQueue, int amount)
    {
        //GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        for (int i = 0; i < amount; i++)
        {
            if (itemQueue.Count > 0)
            {
                GameObject item = itemQueue.Dequeue();
                item.SetActive(true);

                // Get the local position in the canvas RectTransform
                Vector2 uiPosition = WorldToRectTransformPosition(collectedItemPosition, canvasRect);

                // Set the item's parent and position
                item.transform.SetParent(canvas.transform, false);
                item.GetComponent<RectTransform>().anchoredPosition = uiPosition;

                item.transform.localPosition += new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0);

                float duration = Random.Range(minAnimDuration, maxAnimDuration);

                // Tween Animation
                item.transform.DOMove(targetUI.position, duration)
                .SetEase(easeType)
                .OnComplete(() =>
                {
                    item.SetActive(false);
                    itemQueue.Enqueue(item);
                });
            }
        }
    }
    private void AnimateFromUI(RectTransform source,RectTransform targetUI,Queue<GameObject> itemQueue,int amount)
    {
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        for (int i = 0; i < amount; i++)
        {
            if (itemQueue.Count == 0)
                break;

            GameObject item = itemQueue.Dequeue();
            item.SetActive(true);

            // Parent under the canvas
            item.transform.SetParent(canvasRect, worldPositionStays: false);
            RectTransform rt = item.GetComponent<RectTransform>();

            // ***IMPORTANT***: use world position directly
            // CoinSpawnPos is already under the same canvas, so this is valid
            Vector3 startWorld = source.position;
            startWorld.x += Random.Range(-spread, spread);
            startWorld.y += Random.Range(-spread, spread);

            item.transform.position = startWorld;

            float duration = Random.Range(minAnimDuration, maxAnimDuration);

            // Move to HUD target (also world position)
            item.transform.DOMove(targetUI.position, duration)
                .SetEase(easeType)
                .OnComplete(() =>
                {
                    item.SetActive(false);
                    itemQueue.Enqueue(item);
                });
        }
    }


    public static Vector2 WorldToRectTransformPosition(Vector3 worldPosition, RectTransform canvasRectTransform, Camera camera = null)
    {
        if (camera == null)
            camera = Camera.main;

        // Convert world position to screen point
        Vector2 screenPosition = camera.WorldToScreenPoint(worldPosition);

        // Convert screen point to local position in RectTransform
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            screenPosition,
            camera,
            out Vector2 localPosition
        );

        return localPosition;
    }

    public void AddCoinsFromUI(RectTransform source, int amount)
    {
        AnimateFromUI(source, coinTargetUI, coinsQueue, amount);
    }

    public void AddGemsFromUI(RectTransform source, int amount)
    {
        AnimateFromUI(source, gemTargetUI, gemsQueue, amount);
    }

    public void AddHeroXPFromUI(RectTransform source, int amount)
    {
        AnimateFromUI(source, XPTargetUI, XPQueue, amount);
    }


}


