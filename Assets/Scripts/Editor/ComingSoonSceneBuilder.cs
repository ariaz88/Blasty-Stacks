using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Assets/Scenes/ComingSoonScene.unity (one camera, one EventSystem, one
/// ComingSoonScreen with the game's art wired in), adds it to the build list,
/// and unticks every stage scene past <see cref="LastShippedStage"/>.
///
/// The scene is created ADDITIVELY and closed again, so whatever scene is open
/// in the editor - including unsaved changes - is never touched.
/// Re-run it any time; it overwrites the scene it made.
/// </summary>
public static class ComingSoonSceneBuilder
{
    /// <summary>First release ships stages 1-10 (Arash, 2026-10-04).</summary>
    private const int LastShippedStage = 10;

    private const string ScenePath = "Assets/Scenes/ComingSoonScene.unity";

    private const string HomeUI = "Assets/Arts/UI/Battle (Home) UI/";
    private const string LvlUI = "Assets/Arts/UI/Level Complete/";
    private const string StageVfx = "Assets/Arts/GamePlay Assets/GameplayARTS/GameplayArts/Stage VFX/";

    [MenuItem("Tools/Blasty/Coming Soon/Build Scene + Ship Stages 1-10")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.09f, 0.2f);
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        SceneManager.MoveGameObjectToScene(camGo, scene);

        var esGo = new GameObject("EventSystem", typeof(EventSystem));
        AddInputModule(esGo);
        SceneManager.MoveGameObjectToScene(esGo, scene);

        var screenGo = new GameObject("ComingSoon", typeof(RectTransform));
        var screen = screenGo.AddComponent<ComingSoonScreen>();
        SceneManager.MoveGameObjectToScene(screenGo, scene);

        var so = new SerializedObject(screen);
        Set(so, "skyBackground", Sprite(HomeUI + "UI_Top-Sky_BG_H3P.png"));
        Set(so, "patternOverlay", Sprite(HomeUI + "UI_Skull-Pattern_BG-Overlay.png"));
        Set(so, "darkOverlay", Sprite(LvlUI + "UI_LVL-Complete_BG-Overlay_H3P.png"));
        Set(so, "ribbon", Sprite(LvlUI + "UI_LVL-Complete_Top-Ribbon.png"));
        Set(so, "heroCell", Sprite(LvlUI + "UI_LVL-Complete_Reward-Cell.png"));
        Set(so, "button", Sprite(HomeUI + "UI_Start-Button.png"));
        Set(so, "sparkle", Sprite(StageVfx + "Gameplay_Stage_Star.png"));
        Set(so, "sparkleTail", Sprite(StageVfx + "Gameplay_Stage_Star-tail.png"));
        Set(so, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/FONTS/LILITAONE-REGULAR SDF.asset"));
        Set(so, "fontOutlineMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/FONTS/LILITAONE-REGULAR Atlas Material-OutLine.mat"));

        var starsProp = so.FindProperty("stars");
        starsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
            starsProp.GetArrayElementAtIndex(i).objectReferenceValue = Sprite(HomeUI + $"UI_Star-Aactive0{i + 1}.png");

        // Fallback heroes (scene played on its own, no roster): the four deployed-player portraits.
        var portraits = AssetDatabase.FindAssets("t:UnitDefinitionSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d && d.portrait).OrderBy(d => d.unitId).Take(4).Select(d => d.portrait).ToArray();
        var fb = so.FindProperty("fallbackPortraits");
        fb.arraySize = portraits.Length;
        for (int i = 0; i < portraits.Length; i++) fb.GetArrayElementAtIndex(i).objectReferenceValue = portraits[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);

        UpdateBuildList();
        Debug.Log($"[ComingSoon] Built {ScenePath}; stages after {LastShippedStage} unticked in the build list.");
    }

    private static void UpdateBuildList()
    {
        var list = EditorBuildSettings.scenes.ToList();
        var stagePattern = new System.Text.RegularExpressions.Regex(@"Level_(\d+)_Stage_(\d+)\.unity$");

        foreach (var s in list)
        {
            var m = stagePattern.Match(s.path);
            if (!m.Success) continue;
            int level = int.Parse(m.Groups[1].Value), stage = int.Parse(m.Groups[2].Value);
            s.enabled = level == 1 && stage <= LastShippedStage;
        }

        if (!list.Any(s => s.path == ScenePath))
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
        else
            foreach (var s in list) if (s.path == ScenePath) s.enabled = true;

        EditorBuildSettings.scenes = list.ToArray();
    }

    private static void AddInputModule(GameObject es)
    {
        // The project ships activeInputHandler = Both; the stage scenes use the
        // Input System UI module, so this scene does too when it is available.
        var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null) es.AddComponent(t);
        else es.AddComponent<StandaloneInputModule>();
    }

    private static Sprite Sprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!s) Debug.LogWarning("[ComingSoon] Sprite not found (or not imported as Sprite): " + path);
        return s;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[ComingSoon] Missing field " + field);
    }
}
