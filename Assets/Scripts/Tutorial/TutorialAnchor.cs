using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tags any object in a scene with a string id so a tutorial step can point at
/// it without the TutorialSequenceSO holding a scene reference (an asset cannot
/// reference a scene object anyway).
///
/// Works for BOTH kinds of object:
///   - a world object (a Transform)     -> resolved through the world camera
///   - a UI element (a RectTransform)   -> resolved through its own Canvas
///
/// Drop one on a button, a HUD icon, a castle gate, anything a future tutorial
/// needs to highlight, then reference it by id from TutorialTarget.SceneAnchor.
/// </summary>
[DisallowMultipleComponent]
public class TutorialAnchor : MonoBehaviour
{
    [Tooltip("Unique id a tutorial step points at, e.g. \"battle_button\".")]
    [SerializeField] private string anchorId = "";

    public string AnchorId => anchorId;

    // Live anchors, refreshed by OnEnable/OnDisable. A plain list (not a
    // dictionary) because there are only ever a handful and duplicate ids
    // should be reported, not silently overwritten.
    private static readonly List<TutorialAnchor> Live = new List<TutorialAnchor>();

    private void OnEnable()
    {
        if (!Live.Contains(this)) Live.Add(this);
    }

    private void OnDisable()
    {
        Live.Remove(this);
    }

    /// <summary>Finds an enabled anchor by id. Returns null when there is none.</summary>
    public static TutorialAnchor Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < Live.Count; i++)
        {
            var a = Live[i];
            if (a && a.anchorId == id) return a;
        }
        return null;
    }

    /// <summary>
    /// Screen position of this anchor. UI anchors go through their own canvas
    /// (which may be Overlay or Camera mode); world anchors use the camera the
    /// caller supplies.
    /// </summary>
    public bool TryGetScreenPosition(Camera worldCamera, out Vector2 screenPos)
    {
        screenPos = default;

        if (transform is RectTransform rt)
        {
            var canvas = GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = canvas.worldCamera;

            screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, rt.position);
            return true;
        }

        if (!worldCamera) return false;

        screenPos = worldCamera.WorldToScreenPoint(transform.position);
        return true;
    }
}
