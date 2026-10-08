// GameFrame.cs
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Locks the whole game to ONE screen shape - the shape every screen was designed
/// and tested at in the Editor (1125 x 2436, the CanvasScaler reference) - and
/// fills whatever is left of a differently-shaped device with black bars.
///
/// WHY THIS EXISTS
/// ---------------
/// Every UI canvas scales by HEIGHT (CanvasScaler match = 1). On a screen wider
/// than the reference the canvas therefore gets WIDER than 1125 units, and every
/// element that was anchored to an edge drifts away from every element anchored
/// to the centre. The first device build showed exactly that: the Home stage
/// card sat left of its START button (the carousel viewport is pinned to the left
/// edge and sized 1125 wide), and the battle HUD rows were shifted. The Editor
/// never showed it because its Game view is the reference shape.
///
/// Instead of re-anchoring hundreds of hand-authored rects one by one, every
/// camera that draws to the screen is given a centred viewport of the reference
/// shape. Screen Space - Camera canvases (all the game's main canvases) and the
/// 2D world size themselves from their camera's viewport, so on every device the
/// game is drawn exactly as it is in the Editor - just smaller or larger.
///
///   wider than the reference  -> black bars left and right (pillarbox)
///   taller than the reference -> thin black bars top and bottom (letterbox)
///   within <see cref="Tolerance"/> of it (the Editor's 1242x2688) -> full screen
///
/// Input needs no change: Camera.ScreenToWorldPoint / ScreenPointToRay and the
/// EventSystem's camera raycasts all account for a camera's viewport.
///
/// Screen Space - OVERLAY canvases cannot be confined to a viewport. Code that
/// lays such a canvas out against "the screen" should use <see cref="PixelRect"/>
/// instead of Screen.width / Screen.height (HeroCardRevealDirector, TutorialTarget).
///
/// SELF-BOOTSTRAPPING: nothing to place in a scene.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameFrame : MonoBehaviour
{
    /// <summary>The design resolution every canvas's CanvasScaler uses.</summary>
    public const float ReferenceWidth = 1125f;
    public const float ReferenceHeight = 2436f;

    /// <summary>Width / height of the design shape.</summary>
    public const float ReferenceAspect = ReferenceWidth / ReferenceHeight;

    /// <summary>
    /// Relative aspect difference below which the full screen is used. 1% keeps
    /// the Editor's 1242x2688 (0.05% off) and near-identical phones bar-free.
    /// </summary>
    private const float Tolerance = 0.01f;

    private static readonly Color BarColour = Color.black;

    private static GameFrame instance;

    private Camera barsCamera;
    private int lastW, lastH;
    private Rect currentRect = new Rect(0f, 0f, 1f, 1f);
    private Camera[] cameraBuffer = new Camera[8];   // reused - no per-frame garbage

    /// <summary>The game's area as a normalized viewport rect (0..1).</summary>
    public static Rect NormalizedRect => ComputeNormalizedRect(Screen.width, Screen.height);

    /// <summary>The game's area in screen pixels.</summary>
    public static Rect PixelRect
    {
        get
        {
            var n = NormalizedRect;
            return new Rect(n.x * Screen.width, n.y * Screen.height,
                            n.width * Screen.width, n.height * Screen.height);
        }
    }

    /// <summary>Pure function: the centred reference-shape rect for a screen size.</summary>
    public static Rect ComputeNormalizedRect(int screenW, int screenH)
    {
        if (screenW <= 0 || screenH <= 0) return new Rect(0f, 0f, 1f, 1f);

        float screenAspect = (float)screenW / screenH;
        float ratio = screenAspect / ReferenceAspect;

        if (Mathf.Abs(ratio - 1f) <= Tolerance) return new Rect(0f, 0f, 1f, 1f);

        if (ratio > 1f)
        {
            // Wider than the design: full height, bars at the sides.
            float w = 1f / ratio;
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }

        // Taller than the design: full width, bars top and bottom.
        float h = ratio;
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance) return;
        var go = new GameObject("[GameFrame]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameFrame>();
    }

    private void Awake()
    {
        // The bars camera clears the WHOLE screen to black before the game's
        // cameras draw into their centred viewport. It renders no layers.
        barsCamera = gameObject.AddComponent<Camera>();
        barsCamera.clearFlags = CameraClearFlags.SolidColor;
        barsCamera.backgroundColor = BarColour;
        barsCamera.cullingMask = 0;
        barsCamera.depth = -100f;
        barsCamera.orthographic = true;
        barsCamera.rect = new Rect(0f, 0f, 1f, 1f);
        barsCamera.useOcclusionCulling = false;
        barsCamera.allowHDR = false;
        barsCamera.allowMSAA = false;
        barsCamera.enabled = false;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        Apply(force: true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this) instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(force: true);

    // LateUpdate rather than once per scene: cameras can be created or enabled at
    // any time (and the screen can be resized), and re-checking a handful of
    // cameras is far cheaper than a frame drawn in the wrong shape.
    private void LateUpdate() => Apply(force: false);

    private void Apply(bool force)
    {
        if (force || Screen.width != lastW || Screen.height != lastH)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            currentRect = ComputeNormalizedRect(lastW, lastH);
            barsCamera.enabled = !IsFull(currentRect);
        }

        int count = Camera.allCamerasCount;
        if (count == 0) return;
        if (cameraBuffer.Length < count) cameraBuffer = new Camera[count + 4];
        count = Camera.GetAllCameras(cameraBuffer);
        for (int i = 0; i < count; i++)
        {
            var cam = cameraBuffer[i];
            cameraBuffer[i] = null;
            if (!cam || cam == barsCamera) continue;
            if (cam.targetTexture != null) continue;      // render-texture cameras keep their own rect
            if (cam.rect != currentRect) cam.rect = currentRect;
        }
    }

    private static bool IsFull(Rect r) =>
        Mathf.Approximately(r.x, 0f) && Mathf.Approximately(r.y, 0f) &&
        Mathf.Approximately(r.width, 1f) && Mathf.Approximately(r.height, 1f);
}
