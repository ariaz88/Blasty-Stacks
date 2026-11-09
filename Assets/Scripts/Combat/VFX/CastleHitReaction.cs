using UnityEngine;

/// <summary>Damped squash/stretch and sideways shake on castle graphics only.</summary>
[DisallowMultipleComponent]
public sealed class CastleHitReaction : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.3f;
    [SerializeField, Range(0f, 0.12f)] private float squash = 0.045f;
    [SerializeField, Range(0f, 0.03f)] private float shakeWidthFraction = 0.004f;
    private SpriteHitFlash flash;
    private Vector3 pivot;
    private float elapsed, width;
    private bool playing;

    public static CastleHitReaction GetOrAdd(GameObject owner)
    {
        return owner.GetComponent<CastleHitReaction>() ?? owner.AddComponent<CastleHitReaction>();
    }

    private void Awake() => flash = SpriteHitFlash.GetOrAdd(gameObject);

    public void Play()
    {
        if (!isActiveAndEnabled) return;
        flash.Play();
        if (!flash.TryGetVisualBounds(out var bounds)) return;
        pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        width = bounds.size.x;
        elapsed = 0f;
        playing = true;
        // One reaction per castle: repeated hits restart it without accumulating distortion.
        flash.SetDeformation(pivot, Vector2.one, Vector2.zero);
    }

    private void Update()
    {
        if (!playing) return;
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        if (t >= 1f)
        {
            playing = false;
            flash.ClearDeformation();
            return;
        }
        float envelope = Mathf.Exp(-4f * t) * (1f - t);
        float stretch = Mathf.Sin(t * Mathf.PI * 3f) * squash * envelope;
        float shake = Mathf.Sin(t * Mathf.PI * 6f) * width * shakeWidthFraction * envelope;
        flash.SetDeformation(pivot, new Vector2(1f + stretch, 1f - stretch), new Vector2(shake, 0f));
    }

    private void OnDisable()
    {
        playing = false;
        if (flash) flash.ClearDeformation();
    }
}
