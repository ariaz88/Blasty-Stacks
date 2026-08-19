using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PieceSimple))]
public class PiecePainterMaterialsOnly : MonoBehaviour
{
    [Header("One material per colorId (0..N-1)")]
    [SerializeField] private Material[] colorMaterials;

    [Header("When to apply")]
    [SerializeField] private bool applyOnAwake = true;   // runs in play mode
    [SerializeField] private bool applyOnValidate = true; // updates in editor when fields change

    private PieceSimple piece;
    private readonly List<Renderer> renderers = new();

    private void Awake()
    {
        piece = GetComponent<PieceSimple>();
        GetComponentsInChildren(true, renderers);
        if (applyOnAwake) Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!applyOnValidate) return;
        // Try to update live in editor
        if (!piece) piece = GetComponent<PieceSimple>();
        if (piece == null) return;
        GetComponentsInChildren(true, renderers);
        Apply();
    }
#endif

    public void Apply()
    {
        if (colorMaterials == null || colorMaterials.Length == 0) return;

        int id = Mathf.Clamp(piece.ColorId, 0, colorMaterials.Length - 1);
        var mat = colorMaterials[id];
        if (!mat) return;

        // Use sharedMaterial so many pieces can share the same material instance (better batching)
        foreach (var r in renderers)
        {
            if (!r) continue;
            r.sharedMaterial = mat;
        }
    }

    // Optional helper if you want to change at runtime:
    public void SetColorId(int newId, bool applyNow = true)
    {
        // set private field via reflection (since ColorId is read-only)
        var f = typeof(PieceSimple).GetField("colorId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(piece, newId);
        if (applyNow) Apply();
    }
}
