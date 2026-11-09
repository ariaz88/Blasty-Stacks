using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A short silhouette flash shared by all the body sprites of one victim.</summary>
[DisallowMultipleComponent]
public sealed class SpriteHitFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] targets;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField, Min(0.01f)] private float duration = 0.12f;
    [SerializeField, Min(0f)] private float holdDuration = 0.025f;
    [SerializeField, Range(0f, 1f)] private float intensity = 1f;

    private static readonly int AmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int ColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int PivotId = Shader.PropertyToID("_HitPivot");
    private static readonly int ScaleId = Shader.PropertyToID("_HitScale");
    private static readonly int OffsetId = Shader.PropertyToID("_HitOffset");
    private Material material;
    private Material[] originals;
    private float elapsed;
    private bool flashing, deforming, applied;

    public static SpriteHitFlash GetOrAdd(GameObject owner)
    {
        return owner.GetComponent<SpriteHitFlash>() ?? owner.AddComponent<SpriteHitFlash>();
    }

    private void Awake()
    {
        if (targets == null || targets.Length == 0)
        {
            var body = new List<SpriteRenderer>();
            var owner = GetComponent<CharacterStats>();
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (owner && sr.GetComponentInParent<CharacterStats>() != owner) continue;
                string label = sr.name;
                if (label.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("slash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("FX", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                body.Add(sr);
            }
            targets = body.ToArray();
        }
        originals = new Material[targets.Length];
    }

    public void Play()
    {
        if (!isActiveAndEnabled || !ApplyMaterial()) return;
        elapsed = 0f;
        flashing = true;
        material.SetColor(ColorId, flashColor);
        material.SetFloat(AmountId, intensity);
    }

    public bool TryGetVisualBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var sr in targets)
        {
            if (!sr || !sr.enabled || !sr.gameObject.activeInHierarchy || !sr.sprite) continue;
            if (found) bounds.Encapsulate(sr.bounds);
            else { bounds = sr.bounds; found = true; }
        }
        return found;
    }

    public void SetDeformation(Vector3 pivot, Vector2 scale, Vector2 offset)
    {
        if (!isActiveAndEnabled || !ApplyMaterial()) return;
        deforming = true;
        material.SetVector(PivotId, pivot);
        material.SetVector(ScaleId, new Vector4(scale.x, scale.y, 0f, 0f));
        material.SetVector(OffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
    }

    public void ClearDeformation()
    {
        deforming = false;
        if (material)
        {
            material.SetVector(ScaleId, new Vector4(1f, 1f, 0f, 0f));
            material.SetVector(OffsetId, Vector4.zero);
        }
        if (!flashing) RestoreMaterials();
    }

    private bool ApplyMaterial()
    {
        if (applied) return true;
        if (!material)
        {
            // Resources keeps the shader available in Android builds as well as the Editor.
            var shader = Resources.Load<Shader>("VFX/SpriteHitFlash");
            if (!shader || !shader.isSupported) return false;
            material = new Material(shader) { name = name + " Hit Flash", hideFlags = HideFlags.DontSave };
        }
        for (int i = 0; i < targets.Length; i++)
        {
            if (!targets[i]) continue;
            originals[i] = targets[i].sharedMaterial;
            targets[i].sharedMaterial = material;
        }
        applied = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!flashing) return;
        elapsed += Time.deltaTime;
        float hold = Mathf.Min(holdDuration, duration);
        float fade = Mathf.Clamp01((elapsed - hold) / Mathf.Max(0.001f, duration - hold));
        material.SetFloat(AmountId, intensity * (1f - Mathf.SmoothStep(0f, 1f, fade)));
        if (elapsed < duration) return;
        flashing = false;
        material.SetFloat(AmountId, 0f);
        if (!deforming) RestoreMaterials();
    }

    private void RestoreMaterials()
    {
        if (!applied) return;
        for (int i = 0; i < targets.Length; i++)
            if (targets[i] && targets[i].sharedMaterial == material)
                targets[i].sharedMaterial = originals[i];
        applied = false;
    }

    private void OnDisable()
    {
        flashing = false;
        ClearDeformation();
        if (material) material.SetFloat(AmountId, 0f);
    }

    private void OnDestroy()
    {
        RestoreMaterials();
        if (material)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
