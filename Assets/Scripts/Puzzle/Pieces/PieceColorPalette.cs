using UnityEngine;

[DefaultExecutionOrder(-100)]   // init early
[DisallowMultipleComponent]
public class PieceColorPalette : MonoBehaviour
{
    // Color IDs you use: blue=0, green=1, orange=2, pink=3, purple=4, red=5, yellow=6
    public enum ColorId { Blue = 0, Green = 1, Orange = 2, Pink = 3, Purple = 4, Red = 5, Yellow = 6 }

    [Tooltip("Index by ColorID (0..6). Leave null entries if a color isn’t used yet.")]
    [SerializeField] private Material[] materials = new Material[7];

    [Tooltip("Fallback if requested ColorID is out of range or missing.")]
    [SerializeField] private Material fallback;

    public static PieceColorPalette Instance { get; private set; }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public Material Get(int colorId)
    {
        if (materials != null && colorId >= 0 && colorId < materials.Length && materials[colorId])
            return materials[colorId];
        return fallback;
    }
}
