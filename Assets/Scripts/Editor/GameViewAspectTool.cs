// GameViewAspectTool.cs  (Editor only)
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Switches the Game view to a device-shaped resolution and saves Play-mode
/// screenshots, so the UI can be checked at the screen shapes real phones and
/// tablets have - not only the Editor's tall 1242x2688 default.
///
/// WHY: the HUD canvases scale by HEIGHT (CanvasScaler match = 1, reference
/// 1125x2436). On a wider screen the canvas gets wider, and anything anchored
/// wrongly drifts. That is the whole class of "looks right in the Editor,
/// shifted on device" bugs; this tool reproduces them without a build.
///
/// Unity has no public API for the Game view size, so this uses reflection on
/// UnityEditor.GameViewSizes / GameView (stable since 2017, still present in 6).
/// </summary>
public static class GameViewAspectTool
{
    private const string Menu = "Tools/Blasty/Layout/";

    [MenuItem(Menu + "Game View 1242x2688 (Editor default, 9:19.5)")] private static void Tall() => SetSize(1242, 2688);
    [MenuItem(Menu + "Game View 1080x1920 (9:16)")] private static void Phone916() => SetSize(1080, 1920);
    [MenuItem(Menu + "Game View 1080x1886 (recorded device)")] private static void Recorded() => SetSize(1080, 1886);
    [MenuItem(Menu + "Game View 1080x2400 (9:20)")] private static void Phone920() => SetSize(1080, 2400);
    [MenuItem(Menu + "Game View 1536x2048 (tablet 3:4)")] private static void Tablet() => SetSize(1536, 2048);

    [MenuItem(Menu + "Capture Screenshot (Play mode)")]
    private static void CaptureMenu() => Capture(null);

    /// <summary>Adds (if missing) and selects a fixed resolution in the current Game view group.</summary>
    public static void SetSize(int width, int height)
    {
        var asm = typeof(EditorWindow).Assembly;
        var sizesType = asm.GetType("UnityEditor.GameViewSizes");
        var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleType.GetProperty("instance").GetValue(null);
        var groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });

        var getTotal = group.GetType().GetMethod("GetTotalCount");
        var getSize = group.GetType().GetMethod("GetGameViewSize");
        var sizeType = asm.GetType("UnityEditor.GameViewSize");

        int index = -1;
        int total = (int)getTotal.Invoke(group, null);
        for (int i = 0; i < total; i++)
        {
            var s = getSize.Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width").GetValue(s) == width &&
                (int)sizeType.GetProperty("height").GetValue(s) == height)
            { index = i; break; }
        }

        if (index < 0)
        {
            var kindType = asm.GetType("UnityEditor.GameViewSizeType");
            var fixedRes = Enum.Parse(kindType, "FixedResolution");
            var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
            var newSize = ctor.Invoke(new[] { fixedRes, width, height, $"Blasty {width}x{height}" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
            index = (int)getTotal.Invoke(group, null) - 1;
        }

        var gvType = asm.GetType("UnityEditor.GameView");
        var gv = EditorWindow.GetWindow(gvType, false, null, false);
        var sel = gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        sel.SetValue(gv, index);
        gv.Repaint();
        Debug.Log($"[GameViewAspectTool] Game view set to {width}x{height} (index {index}).");
    }

    /// <summary>Saves a Game view screenshot (Play mode only). Returns the file path.</summary>
    public static string Capture(string fileName)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[GameViewAspectTool] Screenshots need Play mode.");
            return null;
        }
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "LayoutCaptures");
        Directory.CreateDirectory(dir);
        if (string.IsNullOrEmpty(fileName))
            fileName = $"capture_{Screen.width}x{Screen.height}_{DateTime.Now:HHmmss}.png";
        string path = Path.Combine(dir, fileName);
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[GameViewAspectTool] Screenshot -> {path}");
        return path;
    }
}
