// SavePersistenceDebugPanel.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// On-screen switch for the <see cref="SavePersistence"/> debug gate.
///
/// Two ways to use it:
///  1. Nothing to do - it auto-spawns itself on play (editor / development builds
///     only) and draws a small collapsible IMGUI panel in the top-right corner.
///  2. Wire the public methods to a real uGUI Button's OnClick if you'd rather have
///     an in-canvas control: <see cref="ToggleSaving"/>, <see cref="EnableSaving"/>,
///     <see cref="DisableSaving"/>, <see cref="ResetProgressNow"/>.
///
/// Stripped entirely from release builds, together with the gate it controls.
/// </summary>
public class SavePersistenceDebugPanel : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    [Tooltip("Start with the panel collapsed to a single small button.")]
    [SerializeField] private bool startCollapsed = true;

    [Tooltip("Scenes the panel is allowed to draw in. Menu only - it must never " +
             "cover gameplay or show up in a screenshot of a battle.")]
    [SerializeField] private string[] allowedScenes = { "MenuScene" };

    private bool _collapsed;
    private bool _visible;
    private Rect _area;

    /// <summary>
    /// Spawns the panel before the first scene's Start, so it exists no matter which
    /// scene you hit Play on and survives scene loads like the other managers.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        var go = new GameObject("[Debug] SavePersistencePanel");
        go.AddComponent<SavePersistenceDebugPanel>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        _collapsed = startCollapsed;
        RefreshVisibility(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        // Also on sceneLoaded: this object is created BeforeSceneLoad, so the scene
        // the Awake check saw may not have been the real first scene yet.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnActiveSceneChanged(Scene from, Scene to) => RefreshVisibility(to);

    // Always judge by the ACTIVE scene, so an additive load can't make the panel
    // think it is in the menu while gameplay is on screen.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        => RefreshVisibility(SceneManager.GetActiveScene());

    /// <summary>
    /// Cached because OnGUI runs several times per frame and comparing scene names
    /// there would allocate a string on every pass.
    /// </summary>
    private void RefreshVisibility(Scene scene)
    {
        _visible = false;
        if (allowedScenes == null) return;

        foreach (var name in allowedScenes)
        {
            if (string.Equals(name, scene.name, StringComparison.OrdinalIgnoreCase))
            {
                _visible = true;
                return;
            }
        }
    }

    // ---- Hook these to a uGUI Button if you want an in-canvas control ----

    public void ToggleSaving() => SavePersistence.Toggle();

    public void EnableSaving() => SavePersistence.Enabled = true;

    public void DisableSaving() => SavePersistence.Enabled = false;

    /// <summary>Wipe progress right now (stage back to 1, resources cleared).</summary>
    public void ResetProgressNow() => SaveSystem.ResetAllIfRequested(true);

    // ---- IMGUI fallback panel ----

    private void OnGUI()
    {
        // Menu-only: never draw over gameplay.
        if (!_visible) return;

        // Scale with resolution so the panel stays readable on a phone-sized game view.
        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUIUtility.ScaleAroundPivot(Vector2.one * scale, Vector2.zero);

        float w = _collapsed ? 84f : 200f;
        float h = _collapsed ? 26f : 116f;
        _area = new Rect((Screen.width / scale) - w - 8f, 8f, w, h);

        GUILayout.BeginArea(_area, GUI.skin.box);

        if (_collapsed)
        {
            bool on = SavePersistence.Enabled;
            if (GUILayout.Button(on ? "SAVE: ON" : "SAVE: OFF")) _collapsed = false;
        }
        else
        {
            bool on = SavePersistence.Enabled;
            GUILayout.Label(on ? "Save/Load: ON" : "Save/Load: OFF (fresh run)");

            if (GUILayout.Button(on ? "Disable save/load" : "Enable save/load"))
                SavePersistence.Toggle();

            if (GUILayout.Button("Reset progress now"))
                ResetProgressNow();

            GUILayout.Label($"Stage {LevelManager.CurrentStage}");

            if (GUILayout.Button("Hide")) _collapsed = true;
        }

        GUILayout.EndArea();
    }

#endif
}
