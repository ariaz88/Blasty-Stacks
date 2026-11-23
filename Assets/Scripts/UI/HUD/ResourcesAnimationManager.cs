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

    [Header("Available Items")]
    [SerializeField] int MaxCoin;
    Queue<GameObject> coinsQueue = new Queue<GameObject>();
    Queue<GameObject> gemsQueue = new Queue<GameObject>();
    Queue<GameObject> XPQueue = new Queue<GameObject>();
    private CanvasSortSnapshot flightCanvas;
    private CanvasSortSnapshot coinHudCanvas;
    private CanvasSortSnapshot gemHudCanvas;
    private CanvasSortSnapshot xpHudCanvas;
    private bool claimPresentationActive;
    private int activeFlights;

    public bool HasActiveFlights => activeFlights > 0;

    private struct CanvasSortSnapshot
    {
        public GameObject root;
        public bool wasActive;
        public Canvas canvas;
        public bool created;
        public bool enabled;
        public bool overrideSorting;
        public string sortingLayer;
        public int sortingOrder;

        public static CanvasSortSnapshot Raise(GameObject root, int order)
        {
            if (!root) return default;
            var existing = root.GetComponent<Canvas>();
            var snapshot = new CanvasSortSnapshot
            {
                root = root,
                wasActive = root.activeSelf,
                canvas = existing ? existing : root.AddComponent<Canvas>(),
                created = !existing,
                enabled = existing && existing.enabled,
                overrideSorting = existing && existing.overrideSorting,
                sortingLayer = existing ? existing.sortingLayerName : "Default",
                sortingOrder = existing ? existing.sortingOrder : 0
            };
            root.SetActive(true);
            snapshot.canvas.enabled = true;
            snapshot.canvas.overrideSorting = true;
            snapshot.canvas.sortingLayerName = GameplayModalOrder.ModalSortingLayer;
            snapshot.canvas.sortingOrder = order;
            return snapshot;
        }

        public void Restore()
        {
            if (!canvas) return;
            canvas.overrideSorting = overrideSorting;
            canvas.sortingLayerName = sortingLayer;
            canvas.sortingOrder = sortingOrder;
            if (created) Object.Destroy(canvas);
            else canvas.enabled = enabled;
            if (root) root.SetActive(wasActive);
        }
    }

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

    public void BeginClaimPresentation(bool coins, bool gems, bool heroXp)
    {
        if (claimPresentationActive || (!coins && !gems && !heroXp)) return;
        claimPresentationActive = true;
        flightCanvas = CanvasSortSnapshot.Raise(gameObject, GameplayModalOrder.RewardFlightOrder);
        // Both currency holders stay readable above the Win panel during Claim,
        // including a replay that awards coins but no gems.
        if (coins || gems)
            coinHudCanvas = CanvasSortSnapshot.Raise(coinTargetUI ? coinTargetUI.gameObject : null,
                                                     GameplayModalOrder.RewardHudOrder);
        if (coins || gems)
            gemHudCanvas = CanvasSortSnapshot.Raise(gemTargetUI ? gemTargetUI.gameObject : null,
                                                    GameplayModalOrder.RewardHudOrder);
        if (heroXp)
            xpHudCanvas = CanvasSortSnapshot.Raise(XPTargetUI ? XPTargetUI.gameObject : null,
                                                   GameplayModalOrder.RewardHudOrder);
    }

    public void EndClaimPresentation()
    {
        if (!claimPresentationActive) return;
        claimPresentationActive = false;
        flightCanvas.Restore();
        coinHudCanvas.Restore();
        gemHudCanvas.Restore();
        xpHudCanvas.Restore();
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
        if (canvas == null || !source || !targetUI) return;

        for (int i = 0; i < amount; i++)
        {
            if (itemQueue.Count == 0)
                break;

            GameObject item = itemQueue.Dequeue();
            item.SetActive(true);

            // Parent under the temporary canvas above the Win panel.
            item.transform.SetParent(transform, worldPositionStays: false);

            // ***IMPORTANT***: use world position directly
            // CoinSpawnPos is already under the same canvas, so this is valid
            Vector3 startWorld = source.position;
            startWorld.x += Random.Range(-spread, spread);
            startWorld.y += Random.Range(-spread, spread);

            item.transform.position = startWorld;

            float duration = Random.Range(minAnimDuration, maxAnimDuration);
            activeFlights++;

            // Move to HUD target (also world position)
            item.transform.DOMove(targetUI.position, duration)
                .SetEase(easeType)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    item.SetActive(false);
                    itemQueue.Enqueue(item);
                    activeFlights--;
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
        // Land on the visible gem icon, while the whole holder is raised above Win.
        var icon = gemTargetUI ? gemTargetUI.Find("GemImage") as RectTransform : null;
        AnimateFromUI(source, icon ? icon : gemTargetUI, gemsQueue, amount);
    }

    public void AddHeroXPFromUI(RectTransform source, int amount)
    {
        AnimateFromUI(source, XPTargetUI, XPQueue, amount);
    }


}
