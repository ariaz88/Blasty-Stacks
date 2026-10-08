// AndroidBuildAutomation.cs
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the Android test APK, either from the menu or from outside the editor.
///
/// External trigger: write any text to Builds/Android/build-request.txt while the
/// editor is open. The watcher below picks it up on the next editor tick, deletes
/// it, builds, and writes the outcome to Builds/Android/build-result.txt (first
/// line "Succeeded ..." / "Failed ..."). This exists because tools that drive the
/// editor remotely are not allowed to call BuildPipeline directly.
///
/// Settings are the test-build recipe: IL2CPP, ARM64 only, debug signing, APK
/// (not AAB), no R8 minify, no Swappy frame pacing. Scenes = the ticked rows in
/// Build Settings, so the stage 1-10 release cut is respected.
/// </summary>
[InitializeOnLoad]
public static class AndroidBuildAutomation
{
    private const string MenuPath = "Tools/Build/Build Android Test APK";
    private const string PackageId = "com.ariaz88.stackywarriors";

    private static string Root => Directory.GetCurrentDirectory();
    private static string OutDir => Path.Combine(Root, "Builds", "Android");
    private static string RequestFile => Path.Combine(OutDir, "build-request.txt");
    private static string ResultFile => Path.Combine(OutDir, "build-result.txt");
    private static string ApkPath => Path.Combine(OutDir, "StackyWarriors.apk");

    private static double nextPoll;

    static AndroidBuildAutomation()
    {
        EditorApplication.update += PollForRequest;
    }

    private static void PollForRequest()
    {
        // A cheap file check twice a second; never while compiling or playing.
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(RequestFile)) return;

        File.Delete(RequestFile);
        // Leave the update callback before the long, blocking build starts.
        EditorApplication.delayCall += () => Build(false);
    }

    [MenuItem(MenuPath)]
    private static void BuildFromMenu() => Build(false);

    /// <summary>
    /// Entry point for a headless build:
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android
    ///             -executeMethod AndroidBuildAutomation.BuildFromCommandLine
    /// Batch mode never shows modal dialogs, so plugins that prompt mid-build
    /// (EDM4U's "Enable Android Auto-resolution?") cannot deadlock it.
    /// </summary>
    public static void BuildFromCommandLine()
    {
        Build(false);
        bool ok = File.Exists(ResultFile) && File.ReadAllText(ResultFile).StartsWith("Succeeded");
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static void Build(bool development)
    {
        Directory.CreateDirectory(OutDir);
        if (File.Exists(ResultFile)) File.Delete(ResultFile);

        string outText;
        try
        {
            ApplyTestSettings(development);

            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);

            var startedUtc = System.DateTime.UtcNow;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None
            });

            // summary.totalSize is NOT the APK size - measure the file instead.
            var sb = new StringBuilder();
            var sum = report.summary;
            long bytes = File.Exists(ApkPath) ? new FileInfo(ApkPath).Length : 0;

            // 2026-10-07: an APK whose merged manifest lacks the AdMob App ID crashes
            // on EVERY device at launch, yet the build itself reports Succeeded. Check
            // the merged manifest and refuse to call such a build a success.
            bool appIdOk = MergedManifestHasAdMobAppId(startedUtc);
            string verdict = sum.result == BuildResult.Succeeded && !appIdOk ? "Failed" : sum.result.ToString();
            sb.AppendLine($"{verdict} errors={sum.totalErrors} warnings={sum.totalWarnings} " +
                          $"seconds={sum.totalTime.TotalSeconds:F0} apkMB={bytes / 1048576f:F1} scenes={scenes.Count} " +
                          $"adMobAppId={(appIdOk ? "OK" : "MISSING")}");
            if (!appIdOk)
                sb.AppendLine("ERR: AdMob APPLICATION_ID missing from the merged AndroidManifest - the app would crash at launch.");
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        sb.AppendLine("ERR: " + m.content);
            outText = sb.ToString();
        }
        catch (System.Exception e)
        {
            outText = "Failed EXCEPTION " + e;
        }

        File.WriteAllText(ResultFile, outText);
        Debug.Log("[AndroidBuildAutomation] " + outText);
    }

    /// <summary>
    /// True if a merged AndroidManifest written by THIS build (Gradle's launcher
    /// output under Library/Bee/Android) contains the AdMob APPLICATION_ID.
    /// </summary>
    private static bool MergedManifestHasAdMobAppId(System.DateTime buildStartedAtUtc)
    {
        string root = Path.Combine(Root, "Library", "Bee", "Android");
        if (!Directory.Exists(root)) return false;
        foreach (var f in Directory.GetFiles(root, "AndroidManifest.xml", SearchOption.AllDirectories))
        {
            if (!f.Replace('\\', '/').Contains("/launcher/build/intermediates/merged_manifest")) continue;
            if (File.GetLastWriteTimeUtc(f) < buildStartedAtUtc) continue;   // stale, from an older build
            if (File.ReadAllText(f).Contains("com.google.android.gms.ads.APPLICATION_ID")) return true;
        }
        return false;
    }

    private static void ApplyTestSettings(bool development)
    {
        var t = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(t, PackageId);
        PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.optimizedFramePacing = false; // Swappy breaks the IL2CPP native build
        PlayerSettings.Android.minifyRelease = false;        // R8 fails on AdMob nextgen classes
        PlayerSettings.Android.useCustomKeystore = false;    // debug signing for test builds
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.development = development;
        AssetDatabase.SaveAssets();
    }
}
