// SavePersistenceMenu.cs
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-menu access to the <see cref="SavePersistence"/> debug gate, so it can be
/// flipped without entering Play mode. Mirrors SavePersistenceDebugPanel.
/// </summary>
public static class SavePersistenceMenu
{
    private const string ToggleItem = "Tools/Save System/Save && Load Enabled";
    private const string WipeItem = "Tools/Save System/Wipe Saved Progress Now";

    [MenuItem(ToggleItem, priority = 0)]
    private static void ToggleSaving() => SavePersistence.Enabled = !SavePersistence.Enabled;

    [MenuItem(ToggleItem, validate = true)]
    private static bool ToggleSavingValidate()
    {
        Menu.SetChecked(ToggleItem, SavePersistence.Enabled);
        return true;
    }

    /// <summary>
    /// Deletes the two PlayerPrefs keys all persistence funnels through. Needed once
    /// after disabling the gate, because a save written earlier is still on disk -
    /// the gate only stops new writes, it doesn't erase the old blob.
    /// </summary>
    [MenuItem(WipeItem, priority = 20)]
    private static void WipeSavedProgress()
    {
        if (!EditorUtility.DisplayDialog(
                "Wipe saved progress?",
                "Deletes GAME_SAVE_V1 (units, currency, stars) and LM.CurrentStage.\n\nThis cannot be undone.",
                "Wipe", "Cancel"))
            return;

        PlayerPrefs.DeleteKey("GAME_SAVE_V1");
        PlayerPrefs.DeleteKey("LM.CurrentStage");
        PlayerPrefs.Save();
        SaveSystem.ResetAll();   // drops the in-memory cache too

        Debug.Log("[SavePersistence] Saved progress wiped: stage 1, no resources.");
    }
}
