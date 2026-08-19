# Ads Commands — copy-paste reference

Portable command/snippet reference for adding and debugging Google AdMob in a
Unity project. Everything here is copy-paste ready; replace `<PLACEHOLDERS>`.

Companion to `Unity-AdMob-Integration-Guide.md` (the *why*); this file is the
*what to type*.

> **Reusable as a Claude Code slash command:** drop this file into
> `.claude/commands/ads.md` in any project and invoke it with `/ads`.

---

## 1. Install

**`Packages/manifest.json`** — add to `dependencies`:

```json
"com.google.ads.mobile": "https://github.com/googleads/googleads-mobile-unity.git?path=packages/com.google.ads.mobile#v11.2.0",
"com.google.external-dependency-manager": "1.2.187"
```

Then in Unity: **Assets > External Dependency Manager > Android Resolver > Resolve**

---

## 2. Test IDs (Google official — safe for development)

```
App ID (Android)       ca-app-pub-3940256099942544~3347511713
Banner (Android)       ca-app-pub-3940256099942544/6300978111
Interstitial (Android) ca-app-pub-3940256099942544/1033173712
Rewarded (Android)     ca-app-pub-3940256099942544/5224354917

Banner (iOS)           ca-app-pub-3940256099942544/2934735716
Interstitial (iOS)     ca-app-pub-3940256099942544/4411468910
Rewarded (iOS)         ca-app-pub-3940256099942544/1712485313
```

App ID uses `~`. Ad unit IDs use `/`.

---

## 3. Gradle — required dependency block

**`Assets/Plugins/Android/mainTemplate.gradle`**, inside `dependencies { }`.
Needed whenever EDM4U cannot resolve (see §8). Keep the `**DEPS**` token.

```gradle
dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])
**DEPS**
    implementation 'com.google.android.gms:play-services-ads:25.3.0'
    implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'androidx.lifecycle:lifecycle-process:2.6.2'
}
```

Kotlin alignment (prevents duplicate-class failures), same file, top level:

```gradle
rootProject.allprojects {
    configurations.configureEach {
        resolutionStrategy {
            force 'org.jetbrains.kotlin:kotlin-stdlib:1.8.22'
            force 'org.jetbrains.kotlin:kotlin-stdlib-jdk7:1.8.22'
            force 'org.jetbrains.kotlin:kotlin-stdlib-jdk8:1.8.22'
        }
    }
}
```

Proguard rule (required if release minification is ON):

```proguard
-dontwarn com.google.android.libraries.ads.mobile.sdk.**
```

---

## 4. Code snippets

**Show a rewarded ad and grant only on success:**

```csharp
AdManager.instance.ShowRewardedAd(
    onRewardEarned:  () => GrantReward(3),
    onAdUnavailable: () => { SetButtonsInteractable(true); });
```

**Guard before showing:**

```csharp
if (AdManager.instance == null || !AdManager.instance.IsRewardedAdReady)
{
    SetButtonsInteractable(true);
    return;
}
```

**Never do this** (grants without watching):

```csharp
GrantReward(3);                       // WRONG — before the callback
AdManager.instance.ShowRewardedAd(null);
```

---

## 5. Build settings (C#, Editor script)

```csharp
using UnityEditor;
using UnityEditor.Build;

PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
PlayerSettings.Android.optimizedFramePacing = false;   // Swappy off
PlayerSettings.defaultInterfaceOrientation  = UIOrientation.Portrait;
PlayerSettings.Android.minifyRelease        = false;   // see §3 proguard rule
EditorUserBuildSettings.buildAppBundle      = false;   // APK, not AAB
PlayerSettings.Android.useCustomKeystore    = false;   // debug signing
```

**Build from script:**

```csharp
var scenes = System.Array.ConvertAll(
    System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);

var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
    scenes           = scenes,
    locationPathName = "Builds/Android/Game.apk",
    target           = BuildTarget.Android,
    targetGroup      = BuildTargetGroup.Android,
    options          = BuildOptions.None
});
Debug.Log(report.summary.result + " " + report.summary.totalTime);
```

---

## 6. Device commands

```bash
# Devices attached
adb devices

# Install / reinstall
adb install -r Builds/Android/Game.apk

# Unity + ads logs only
adb logcat -s Unity:V Ads:V

# Everything from your app
adb logcat --pid=$(adb shell pidof -s <YOUR.PACKAGE.NAME>)

# Ad-related lines only
adb logcat | grep -i "admob\|mobileads\|rewarded\|interstitial"

# Clear log, then reproduce
adb logcat -c

# Uninstall
adb uninstall <YOUR.PACKAGE.NAME>
```

`adb` lives at:
`<UNITY>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`

---

## 7. Verify the APK actually contains the SDK

```powershell
# PowerShell — dex files + entry count
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [System.IO.Compression.ZipFile]::OpenRead("Builds/Android/Game.apk")
"entries: $($z.Entries.Count)"
$z.Entries.FullName | Where-Object { $_ -like '*.dex' }
$z.Entries.FullName | Where-Object { $_ -like '*arm64-v8a*' }
$z.Dispose()
```

Confirm the App ID reached the manifest:

```bash
grep -n "APPLICATION_ID" \
  Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml
```

---

## 8. Fix EDM4U resolution (`JAVA_HOME`)

Symptom: `Resolution Failed` / `Gradle failed to fetch dependencies` /
`JAVA_HOME is set to an invalid directory`.

```powershell
# Inspect
$env:JAVA_HOME

# Correct value — the JDK of the Unity version you actually build with
"C:\Program Files\Unity\Hub\Editor\<YOUR_VERSION>\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK"

# Set permanently (new shells only; restart Unity afterwards)
[Environment]::SetEnvironmentVariable("JAVA_HOME",
  "C:\Program Files\Unity\Hub\Editor\<YOUR_VERSION>\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK",
  "User")
```

---

## 9. Build-log triage

```bash
# Windows Unity editor log
tail -c 3000 "$LOCALAPPDATA/Unity/Editor/Editor.log"

# Jump to the failure
grep -n "What went wrong" -A 25 "$LOCALAPPDATA/Unity/Editor/Editor.log" | tail -60

# Success / failure markers
grep -n "BUILD FAILED\|BUILD SUCCESSFUL" "$LOCALAPPDATA/Unity/Editor/Editor.log"
```

---

## 10. Error → fix lookup

| Error text | Fix |
|---|---|
| `AAPT: error: attribute layout_constraintTop_toTopOf not found` | Add the §3 dependency block |
| `Missing class com.google.android.libraries.ads.mobile.sdk.*` | Minify off, or add the §3 `-dontwarn` rule |
| `JAVA_HOME is set to an invalid directory` | §8 |
| `Resolution Failed` in Android Resolver | §8, then re-resolve |
| `checkDebugDuplicateClasses` failed | Kotlin `force` block in §3 |
| Ads fine in Editor, nothing on device | **Check the device's internet first** |
| No ad, no logcat output | `AdManager` not in scene / stripped — verify `link.xml` |
| Editor shows a grey "test ad" panel | Expected. Real creatives require a device |

---

## 11. Ship checklist

```
[ ] Production App ID + ad unit IDs
[ ] App registered in AdMob console, package name matches
[ ] minifyRelease ON + -dontwarn rule present
[ ] Real keystore (not debug signing)
[ ] AAB for Google Play (buildAppBundle = true)
[ ] ARMv7 added if supporting older devices
[ ] GDPR consent flow (User Messaging Platform) for EU
[ ] JAVA_HOME correct so EDM4U resolves without manual Gradle lines
```
