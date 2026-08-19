// SaveOnStop.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SaveOnStop : MonoBehaviour
{
    void OnApplicationQuit()
    {
        SaveSystem.Save(); // final flush for builds & editor
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    static class EditorAutoSaveHook
    {
        static EditorAutoSaveHook()
        {
            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    SaveSystem.Save(); // ensure JSON/PlayerPrefs flushed
                }
            };
        }
    }
#endif
}
