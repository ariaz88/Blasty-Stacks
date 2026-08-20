using TMPro;
using UnityEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BucketStatsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform content;  // ScrollRect/Viewport/Content
    [SerializeField] private GameObject root;        // whole overlay (the object with Canvas)
    [SerializeField] private Button closeButton;

    [SerializeField] private RectTransform skeletonImage;   // drag Skeleton here

    public RectTransform Content => content;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);

        // start hidden
        Hide();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        Hide();
    }

    public void Show(string title)
    {
        if (titleText != null)
            titleText.text = title.ToUpperInvariant();

        if (root != null)
            root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void Clear()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);

            // don�t delete the skeleton background
            if (skeletonImage != null && child == skeletonImage)
                continue;

            Destroy(child.gameObject);
        }
    }
}
