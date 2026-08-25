using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helpers for testing tutorials. Without these, re-testing a playOnce
/// tutorial means wiping the whole save.
/// </summary>
public static class TutorialMenu
{
    [MenuItem("Tools/Tutorial/Reset Tutorial Progress", priority = 0)]
    private static void ResetTutorialProgress()
    {
        SaveSystem.ResetTutorials();
        Debug.Log("[Tutorial] Tutorial progress cleared - every tutorial will play again.");
    }

    [MenuItem("Tools/Tutorial/Log Tutorial Progress", priority = 1)]
    private static void LogTutorialProgress()
    {
        var list = SaveSystem.Data.completedTutorials;

        if (list == null || list.Count == 0)
        {
            Debug.Log("[Tutorial] No tutorials marked as seen.");
            return;
        }

        Debug.Log($"[Tutorial] Seen ({list.Count}): {string.Join(", ", list)}");
    }
}
