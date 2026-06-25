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
        // if you forgot to assign root in inspector, fall back to this GameObject
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

            // don’t delete the skeleton background
            if (skeletonImage != null && child == skeletonImage)
                continue;

            Destroy(child.gameObject);
        }
    }
}

public class BucketStatsPanel1 : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform content;  // ScrollRect/Viewport/Content
    [SerializeField] private GameObject root;        // whole overlay (canvas group/panel)
    [SerializeField] private UnityEngine.UI.Button closeButton;

    public RectTransform Content => content;

    [SerializeField] private RectTransform skeletonImage;   // drag Skeleton here

    private void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Hide);
        Hide();
    }

    public void Show(string title)
    {
        if (titleText) titleText.text = title.ToUpper();
        if (root) root.SetActive(true);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    public void Clear1()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
    public void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);

            // don’t delete the skeleton background
            if (child == skeletonImage)
                continue;

            Destroy(child.gameObject);
        }
    }
}
