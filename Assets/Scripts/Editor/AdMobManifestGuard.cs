// AdMobManifestGuard.cs  (Editor only)
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Guarantees the AdMob App ID is in every Android build's manifest.
///
/// WHY: the Google Mobile Ads plugin writes
/// &lt;meta-data android:name="com.google.android.gms.ads.APPLICATION_ID"&gt; in its
/// ManifestProcessor, but that class is wrapped in #if UNITY_ANDROID - it only
/// EXISTS when the editor's active target is already Android when the build
/// starts. The 2026-10-05 and 2026-10-07 APKs were started from a Windows-target
/// editor, the processor never ran, and on device the app crashed at launch
/// before any game code: "Missing application ID. AdMob publishers should ...
/// add a valid App ID inside the AndroidManifest" (MobileAdsInitProvider).
///
/// This callback is NOT behind #if UNITY_ANDROID, so it always runs. After Unity
/// generates the Gradle project it checks unityLibrary's manifest and, if the
/// App ID is missing, adds it with the value from GoogleMobileAdsSettings.asset.
/// When the plugin already wrote it, this does nothing.
/// </summary>
public class AdMobManifestGuard : IPostGenerateGradleAndroidProject
{
    private const string MetaName = "com.google.android.gms.ads.APPLICATION_ID";
    private const string SettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    // After the plugin's own Gradle processor.
    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        string manifestPath = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogError("[AdMobManifestGuard] No manifest at " + manifestPath);
            return;
        }

        string appId = ReadAndroidAppId();
        if (string.IsNullOrEmpty(appId))
        {
            // Fail loudly: an APK without it crashes on every device at launch.
            throw new System.Exception("[AdMobManifestGuard] adMobAndroidAppId is empty in " + SettingsPath +
                                       " - the APK would crash at launch. Set it before building.");
        }

        var doc = XDocument.Load(manifestPath);
        var app = doc.Root?.Element("application");
        if (app == null)
        {
            Debug.LogError("[AdMobManifestGuard] <application> missing in " + manifestPath);
            return;
        }

        foreach (var meta in app.Elements("meta-data"))
        {
            if ((string)meta.Attribute(Android + "name") == MetaName)
            {
                if ((string)meta.Attribute(Android + "value") != appId)
                    meta.SetAttributeValue(Android + "value", appId);
                doc.Save(manifestPath);
                Debug.Log("[AdMobManifestGuard] AdMob App ID present: " + appId);
                return;
            }
        }

        app.Add(new XElement("meta-data",
            new XAttribute(Android + "name", MetaName),
            new XAttribute(Android + "value", appId)));
        doc.Save(manifestPath);
        Debug.LogWarning("[AdMobManifestGuard] AdMob App ID was MISSING from the manifest - added " + appId +
                         " (the Google Mobile Ads ManifestProcessor did not run; was the editor on Android?)");
    }

    /// <summary>Reads adMobAndroidAppId straight from the settings asset's YAML.</summary>
    private static string ReadAndroidAppId()
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), SettingsPath);
        if (!File.Exists(full)) return null;
        var m = Regex.Match(File.ReadAllText(full), @"adMobAndroidAppId:\s*(\S+)");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
