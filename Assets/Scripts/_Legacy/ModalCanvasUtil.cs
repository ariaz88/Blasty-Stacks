using UnityEngine;
using UnityEngine.UI;

public static class ModalCanvasUtil
{
    public static void PromoteToOverlayCanvas(GameObject panel, int sortOrder = 999)
    {
        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas == null)
            canvas = panel.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = sortOrder;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (panel.GetComponent<GraphicRaycaster>() == null)
            panel.AddComponent<GraphicRaycaster>();
    }

    public static void RemoveOverlayCanvas(GameObject panel)
    {
        GraphicRaycaster raycaster = panel.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            Object.Destroy(raycaster);

        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas != null)
            Object.Destroy(canvas);

       
    }
}
