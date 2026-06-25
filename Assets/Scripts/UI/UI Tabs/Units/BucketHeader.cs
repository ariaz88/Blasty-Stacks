using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BucketHeader : MonoBehaviour
{
    [SerializeField] private float headerLeftPadding = 24f; // tune to taste

    public enum BucketType { Deployed, Undeployed, Unachieved }

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button menuButton;
    [SerializeField] private BucketType bucket;

    public BucketType Type => bucket;

    public string Title
    {
        get => titleText ? titleText.text : "";
        set
        {
            if (!titleText) return;
            titleText.text = value;
            ApplyTitlePadding();                   // <-- ensure padding after any external set
        }
    }

    // OPTIONAL: expose padding as a property you can change at runtime
    public float LeftPadding
    {
        get => headerLeftPadding;
        set { headerLeftPadding = value; ApplyTitlePadding(); }
    }

    public void Wire(System.Action<BucketType> onMenuClick)
    {
        if (!menuButton) return;
        menuButton.onClick.RemoveAllListeners();
        menuButton.onClick.AddListener(() => onMenuClick?.Invoke(bucket));
    }

    // --- NEW: keep the text left margin in sync ---
    private void ApplyTitlePadding()
    {
        if (!titleText) return;
        var m = titleText.margin;   // Vector4: (left, top, right, bottom)
        m.x = headerLeftPadding;    // left margin
        titleText.margin = m;
    }

    private void Awake() => ApplyTitlePadding();
    private void OnEnable() => ApplyTitlePadding();

#if UNITY_EDITOR
    private void OnValidate() => ApplyTitlePadding(); // reflect changes in Inspector
#endif
}
