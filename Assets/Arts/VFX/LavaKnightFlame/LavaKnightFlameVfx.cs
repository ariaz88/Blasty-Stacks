using UnityEngine;

namespace Blasty.Vfx
{
    /// <summary>
    /// Continuously looping flame flipbook for the Lava Knight's helmet crest.
    ///
    /// Unlike <see cref="FireSwordSlashVfx"/> this is not a one-shot: the crest burns
    /// for as long as the unit is alive, so it just cycles frames forever and never
    /// disables itself.
    ///
    /// Feed it either the 16 sliced frames (drag them onto <see cref="frames"/>) or the
    /// 4x4 sheet (<see cref="sheet"/>), which is sliced at runtime so no Editor
    /// sprite-slicing step is needed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LavaKnightFlameVfx : MonoBehaviour
    {
        [Header("Source — use ONE of these")]
        [Tooltip("The 16 individual flame frames, in order. Takes priority over the sheet.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("The 4x4 flipbook sheet, sliced at runtime if no frames are assigned.")]
        [SerializeField] private Texture2D sheet;
        [SerializeField] private int sheetColumns = 4;
        [SerializeField] private int sheetRows = 4;
        [SerializeField] private float pixelsPerUnit = 100f;

        [Header("Playback")]
        [Min(1f)] [SerializeField] private float framesPerSecond = 18f;
        [Tooltip("Randomises the start frame so several knights on screen don't flicker in unison.")]
        [SerializeField] private bool randomizePhase = true;

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 1;
        [SerializeField] private Color tint = Color.white;

        private SpriteRenderer spriteRenderer;
        private Sprite[] clip;
        private float timer;
        private int index;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            clip = BuildClip();

            if (!string.IsNullOrEmpty(sortingLayerName))
                spriteRenderer.sortingLayerName = sortingLayerName;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = tint;

            if (clip != null && clip.Length > 0)
            {
                if (randomizePhase) index = Random.Range(0, clip.Length);
                spriteRenderer.sprite = clip[index];
            }
        }

        private Sprite[] BuildClip()
        {
            if (frames != null && frames.Length > 0) return frames;
            if (sheet == null) return null;

            int cols = Mathf.Max(1, sheetColumns);
            int rows = Mathf.Max(1, sheetRows);
            int cw = sheet.width / cols;
            int ch = sheet.height / rows;

            var built = new Sprite[cols * rows];
            for (int i = 0; i < built.Length; i++)
            {
                int cx = i % cols;
                // Sheet rows are authored top-to-bottom; texture space is bottom-up.
                int cy = rows - 1 - (i / cols);
                built[i] = Sprite.Create(
                    sheet,
                    new Rect(cx * cw, cy * ch, cw, ch),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
            }
            return built;
        }

        private void Update()
        {
            if (clip == null || clip.Length == 0) return;

            timer += Time.deltaTime;
            float step = 1f / framesPerSecond;
            while (timer >= step)
            {
                timer -= step;
                index = (index + 1) % clip.Length;
                spriteRenderer.sprite = clip[index];
            }
        }

        private void OnValidate()
        {
            if (framesPerSecond < 1f) framesPerSecond = 1f;
            if (sheetColumns < 1) sheetColumns = 1;
            if (sheetRows < 1) sheetRows = 1;
        }
    }
}
