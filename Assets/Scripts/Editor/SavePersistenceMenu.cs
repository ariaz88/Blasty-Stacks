// SavePersistenceMenu.cs
using UnityEditor;
using UnityEngine;

/// <summary>
/// Testing tool: start the game over as a brand-new player.
///
/// Saving itself is UNCONDITIONAL (2026-09-22) - there is no on/off switch any
/// more, in the editor menu or on screen, and every state change writes through
/// SaveSystem straight away. What is still needed for testing is the opposite
/// operation: erasing what was written, so the next Play boots a first run -
/// stage 1, starting currency, and the first-run tutorial detour in MenuLoader.
/// </summary>
public static class SavePersistenceMenu
{
    private const string WipeItem = "Tools/Save System/Wipe Saved Progress Now";

    /// <summary>
    /// Deletes the two PlayerPrefs keys all persistence funnels through, which is
    /// the whole save: units, currency, stars, completed tutorials and the stage.
    /// </summary>
    [MenuItem(WipeItem, priority = 0)]
    private static void WipeSavedProgress()
    {
        if (!EditorUtility.DisplayDialog(
                "Wipe saved progress?",
                "Deletes GAME_SAVE_V1 (units, currency, stars, completed tutorials) and " +
                "LM.CurrentStage.\n\nThe next Play boots as a new player, tutorial included." +
                "\n\nThis cannot be undone.",
                "Wipe", "Cancel"))
            return;

        WipeNow();
    }

    /// <summary>The wipe without the confirmation dialog, for scripted/automated runs.</summary>
    public static void WipeNow()
    {
        // One implementation, shared with GameStartManager's "Reset Progress On
        // Play" tickbox - so the menu and the checkbox can never wipe differently.
        SaveSystem.WipeAllProgress();

        Debug.Log("[SaveSystem] Saved progress wiped: next run is a first run (stage 1, tutorial plays).");
    }
}
