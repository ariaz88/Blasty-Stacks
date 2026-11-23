using UnityEngine;

/// <summary>Keeps an open gameplay menu and its background above the stage UI.</summary>
public static class GameplayModalOrder
{
    // Stats is the highest sorting layer in this project. Health bars use order 500.
    public const string ModalSortingLayer = "Stats";
    public const int ModalOrder = 30000;
    public const int RewardHudOrder = ModalOrder + 1;
    public const int RewardFlightOrder = ModalOrder + 2;

    public static void BringToFront(GameObject panel, int order = ModalOrder)
    {
        if (!panel) return;

        panel.transform.SetAsLastSibling();

        var canvas = panel.GetComponent<Canvas>();
        if (!canvas) canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = ModalSortingLayer;
        canvas.sortingOrder = order;

        // A nested Canvas needs its own raycaster for the menu buttons.
        if (!panel.GetComponent<UnityEngine.UI.GraphicRaycaster>())
            panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    public static void BringBackgroundToFront(GameObject background)
    {
        BringToFront(background, ModalOrder - 1);
    }
}
