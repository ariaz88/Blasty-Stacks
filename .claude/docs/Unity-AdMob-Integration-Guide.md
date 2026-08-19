# Unity + Google AdMob — Integration Guide

A portable, project-agnostic guide to adding Google Mobile Ads (AdMob) to a Unity
game and getting it onto an Android device. Written from a real integration where
the Android build **failed twice before succeeding** — every failure and its fix
is documented here so the next project skips them.

| | |
|---|---|
| Unity | 6000.3.21f1 (works on 2021.3+ with minor menu-path differences) |
| Google Mobile Ads Unity SDK | v11.2.0 |
| External Dependency Manager (EDM4U) | v1.2.187 |
| Scripting backend | IL2CPP + ARM64 |
| Verified on | Android. **iOS paths are included but were not tested.** |

---

## 0. What this costs

Nothing. Installing the SDK and serving ads does **not** require paying Google —
Google pays *you* from ad revenue. Money only flows the other way if you buy ad
campaigns through Google Ads, which is unrelated.

For development you use Google's **official test ad unit IDs**. Never point a
development build at production ad units: repeatedly requesting and clicking your
own live ads is invalid traffic and can get an AdMob account suspended.

---

## 1. Install the SDK

Add both packages to `Packages/manifest.json`. The ads SDK ships from Google's
GitHub, not the Unity registry:

```json
{
  "dependencies": {
    "com.google.ads.mobile": "https://github.com/googleads/googleads-mobile-unity.git?path=packages/com.google.ads.mobile#v11.2.0",
    "com.google.external-dependency-manager": "1.2.187"
  }
}
```

`external-dependency-manager` (EDM4U) is not optional. It is what pulls the
Android libraries the ads SDK needs into your Gradle build. See §7 for what
happens when it silently fails — the single most likely thing to break your build.

---

## 2. Set the App ID

Create the settings asset at
`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`
(menu: **Assets > Google Mobile Ads > Settings**) and set the Android App ID.
Google's public test App ID:

```
ca-app-pub-3940256099942544~3347511713
```

Note the `~` — App IDs use a tilde, ad *unit* IDs use a `/`. Mixing them up is a
common and confusing failure.

**You do not hand-edit AndroidManifest.xml for this.** The plugin injects the App
ID into its own android library at build time. To verify after a build, this file:

```
Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml
```

should contain:

```xml
<meta-data android:name="com.google.android.gms.ads.APPLICATION_ID"
           android:value="ca-app-pub-3940256099942544~3347511713" />
```

If this meta-data is missing, the SDK throws on initialization and **no ad will
ever load**. Do not look for it in the `launcher` or `unityLibrary` manifests —
it is not there, and its absence in those files is normal.

---

## 3. The AdManager script

One persistent singleton owns initialization and ad lifecycle. Key design
decisions, and why:

- **Ads are pre-loaded**, not loaded on demand. A rewarded ad needs a network
  round-trip; requesting one the moment the player clicks means showing nothing.
  Load at init, and reload immediately after each show.
- **The reward is granted in a callback**, never optimistically. The player must
  actually finish the ad.
- **`onAdUnavailable` is part of the contract.** Without it, the UI disables its
  buttons waiting for an ad that never appears, and the player is stuck.

```csharp
using System;
using GoogleMobileAds;
using GoogleMobileAds.Api;
using UnityEngine;

/// <summary>
/// Central Google Mobile Ads manager. Uses Google's official test ad unit IDs,
/// so it is safe for development and device testing. Replace the IDs only when
/// a verified production AdMob setup is ready.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager instance;

    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;
    private bool isInitialized;

#if UNITY_ANDROID || UNITY_EDITOR
    private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    private const string RewardedAdUnitId     = "ca-app-pub-3940256099942544/5224354917";
    private const string BannerAdUnitId       = "ca-app-pub-3940256099942544/6300978111";
#elif UNITY_IOS
    private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    private const string RewardedAdUnitId     = "ca-app-pub-3940256099942544/1712485313";
    private const string BannerAdUnitId       = "ca-app-pub-3940256099942544/2934735716";
#else
    private const string InterstitialAdUnitId = "unused";
    private const string RewardedAdUnitId     = "unused";
    private const string BannerAdUnitId       = "unused";
#endif

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() => Initialize();

    public void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        MobileAds.Initialize(status =>
        {
            if (status == null)
            {
                Debug.LogError("Google Mobile Ads initialization failed.");
                return;
            }

            isInitialized = true;
            Debug.Log("Google Mobile Ads initialized with test ad unit IDs.");

            // Pre-load so an ad exists before the player asks for one.
            LoadInterstitialAd();
            LoadRewardedAd();
        });
    }

    #region Interstitial

    public void LoadInterstitialAd()
    {
        if (!isInitialized) return;

        interstitialAd?.Destroy();
        interstitialAd = null;

        InterstitialAd.Load(InterstitialAdUnitId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError("Interstitial ad failed to load: " + error);
                return;
            }

            interstitialAd = ad;
            RegisterInterstitialEvents(ad);
        });
    }

    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            interstitialAd.Show();
            return;
        }

        Debug.LogWarning("Interstitial ad is not ready yet.");
        LoadInterstitialAd();
    }

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            ad.Destroy();
            if (interstitialAd == ad) interstitialAd = null;
            LoadInterstitialAd();          // always queue the next one
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogError("Interstitial failed to show: " + error);
            ad.Destroy();
            if (interstitialAd == ad) interstitialAd = null;
            LoadInterstitialAd();
        };
    }

    #endregion

    #region Rewarded

    public bool IsRewardedAdReady => rewardedAd != null && rewardedAd.CanShowAd();

    public void LoadRewardedAd()
    {
        if (!isInitialized) return;

        rewardedAd?.Destroy();
        rewardedAd = null;

        RewardedAd.Load(RewardedAdUnitId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError("Rewarded ad failed to load: " + error);
                return;
            }

            rewardedAd = ad;
            RegisterRewardedEvents(ad);
        });
    }

    /// <summary>
    /// Shows a rewarded ad. onRewardEarned fires only after Google confirms the
    /// user earned the reward. onAdUnavailable fires immediately when no ad is
    /// ready, so the caller can re-enable its UI instead of hanging.
    /// </summary>
    public void ShowRewardedAd(Action onRewardEarned, Action onAdUnavailable = null)
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            rewardedAd.Show(_ => onRewardEarned?.Invoke());
            return;
        }

        Debug.LogWarning("Rewarded ad is not ready yet.");
        LoadRewardedAd();
        onAdUnavailable?.Invoke();
    }

    private void RegisterRewardedEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            ad.Destroy();
            if (rewardedAd == ad) rewardedAd = null;
            LoadRewardedAd();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogError("Rewarded ad failed to show: " + error);
            ad.Destroy();
            if (rewardedAd == ad) rewardedAd = null;
            LoadRewardedAd();
        };
    }

    #endregion

    private void OnDestroy()
    {
        interstitialAd?.Destroy();
        rewardedAd?.Destroy();
    }
}
```

Place one `AdManager` in your first-loaded scene. `DontDestroyOnLoad` keeps it
alive across level loads, and the `Awake` guard prevents duplicates when
returning to that scene.

---

## 4. Call it from the UI

The pattern that matters: **disable the buttons, request, grant only in the
success callback.** Re-enable on failure or the player is locked out.

```csharp
public void ClaimTripleReward()
{
    if (claimed || requestInProgress) return;

    requestInProgress = true;
    SetButtonsInteractable(false);

    if (AdManager.instance == null)
    {
        HandleAdUnavailable();
        return;
    }

    AdManager.instance.ShowRewardedAd(
        onRewardEarned:  CompleteTripleClaim,   // reward granted here, only here
        onAdUnavailable: HandleAdUnavailable);
}

private void CompleteTripleClaim()
{
    requestInProgress = false;
    claimed = true;
    GrantReward(multiplier: 3);
}

private void HandleAdUnavailable()
{
    requestInProgress = false;
    Debug.LogWarning("Rewarded ad not ready. Try again shortly.");
    SetButtonsInteractable(true);       // never leave the player stuck
}
```

> **Sequencing trap.** If claiming a reward also triggers a scene change, do not
> call the reward animation and the scene load in the same frame. `LoadScene`
> kills the running coroutine and the player sees no reward at all. Await the
> animation, then load:
>
> ```csharp
> private IEnumerator AdvanceAfterClaim(int multiplier)
> {
>     yield return ShowRewardAnimation(multiplier);
>     yield return new WaitForSecondsRealtime(1f);
>     GoToNextLevel();
> }
> ```

---

## 5. Protect against code stripping

IL2CPP strips unused managed code. The ads SDK is invoked partly via reflection
and native callbacks, so stripping can break it in ways that never appear in the
Editor.

The plugin ships `Assets/GoogleMobileAds/link.xml`. **Confirm it exists.** If ads
work in the Editor but silently fail in a release build, check this first.

---

## 6. Android build settings

The configuration that worked:

| Setting | Value |
|---|---|
| Scripting backend | IL2CPP |
| Target architecture | **ARM64 only** |
| Optimized Frame Pacing (Swappy) | **Off** |
| Kotlin stdlib | forced to 1.8.22 |
| Orientation | Portrait |
| Signing | Debug (test builds) |
| Output | APK, not AAB |
| Minify (release) | **Off** — see §7.2 |

Kotlin version alignment goes in `Assets/Plugins/Android/mainTemplate.gradle`.
Different plugins request different Kotlin versions, causing duplicate-class
failures at `checkDebugDuplicateClasses`:

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

---

## 7. The three build attempts

This is the section worth reading *before* you build.

### 7.1 Attempt 1 — FAILED after 7m52s: AAPT resource linking

```
Execution failed for task ':launcher:processReleaseResources'
Android resource linking failed
ERROR: .../jetified-googlemobileads-unity/res/layout/gnt_medium_template_view.xml:21:
AAPT: error: attribute layout_constraintTop_toTopOf not found.
```

**Cause.** The ads AAR ships native-ad template layouts built on
`androidx.constraintlayout`. That dependency — along with `play-services-ads` and
`lifecycle-process` — is declared in the plugin's own
`GoogleMobileAdsDependencies.xml`, but never reached the Gradle project because
**EDM4U's resolver could not run** (see §7.3).

**Fix.** Declare them by hand in `Assets/Plugins/Android/mainTemplate.gradle`:

```gradle
dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])
**DEPS**
    // Declared by GoogleMobileAdsDependencies.xml. Added explicitly because the
    // googlemobileads-unity AAR ships ConstraintLayout-based native ad layouts;
    // without these, AAPT fails to link layout_constraint* attributes.
    implementation 'com.google.android.gms:play-services-ads:25.3.0'
    implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'androidx.lifecycle:lifecycle-process:2.6.2'
}
```

Keep the `**DEPS**` token — Unity substitutes into it.

### 7.2 Attempt 2 — FAILED after 5m09s: R8 minification

```
Execution failed for task ':launcher:minifyReleaseWithR8'
ERROR: Missing classes detected while running R8
Missing class com.google.android.libraries.ads.mobile.sdk.MobileAds
  (referenced from: com.google.unity.ads.nextgen.MobileAdsWrapper...)
... ~40 more
```

**Cause.** The AAR also bundles "nextgen" wrapper classes referencing an entirely
different ads SDK namespace (`com.google.android.libraries.ads.mobile.sdk.*`)
that the plugin never declares as a dependency. R8 treats unresolved references
as fatal.

**Fix (fast, for test builds).** Disable release minification:
**Player Settings > Publishing Settings > Minify > Release** → off.

**Fix (correct, before shipping).** Re-enable minification and add a custom
Proguard file containing:

```proguard
-dontwarn com.google.android.libraries.ads.mobile.sdk.**
```

Shipping with minification off produces a larger, unobfuscated APK.

### 7.3 The root cause behind attempt 1 — `JAVA_HOME`

EDM4U's "Resolving Android Dependencies" dialog was failing the whole time:

```
ERROR: Gradle failed to fetch dependencies.
ERROR: JAVA_HOME is set to an invalid directory:
C:\Program Files\Unity\Hub\Editor\2021.3.42f1\...\AndroidPlayer\OpenJDK
```

The machine's `JAVA_HOME` pointed at a **Unity version that was not the one
building the project**. EDM4U uses `JAVA_HOME`; Gradle uses Unity's own bundled
JDK. That asymmetry is why the build ran at all and only died deep in AAPT/R8 —
the error looked like an SDK bug when it was a machine configuration problem.

**Fix.** Point `JAVA_HOME` at the JDK of the Unity version you actually build
with, or clear it:

```
C:\Program Files\Unity\Hub\Editor\<YOUR_VERSION>\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK
```

Check this **first** whenever a newly added Android package's dependencies
mysteriously fail to appear.

### 7.4 Attempt 3 — SUCCEEDED in 2m19s

Both fixes in place. Result: ~58 MB APK, ARM64-only, 2 dex files, ads SDK classes
verified present.

---

## 8. Testing

### In the Unity Editor
You get a **mock/placeholder ad** — a grey panel reading "This is an interstitial
test ad from Google AdMob" with a countdown. That is the expected, correct
result. It exercises your callbacks, close behaviour and reward logic.

**You cannot see real Google ad creatives in the Editor.** Do not spend time
trying.

### On an Android device
Test ad units serve real creatives (image, video, interactive) through Google's
network.

```bash
adb install -r path/to/YourGame.apk
adb logcat -s Unity:V Ads:V
```

### If no ad appears — check the network first

Ads require a live internet connection. A device with no data, airplane mode, a
blocked network, or a VPN/proxy that cannot reach Google will silently fail every
load. In this integration, "ads don't show at all" turned out to be **exactly
this** — the device's network was disabled. The integration was correct the whole
time.

Diagnose in this order:

1. **Network reachable?** Open a browser on the device.
2. **`Google Mobile Ads initialized` in logcat?** No → SDK / App-ID problem.
3. **`Rewarded ad failed to load: <reason>`?** → read the reason; usually network
   or an invalid ad unit ID.
4. **Nothing in logcat at all?** → `AdManager` is not in the scene, or was
   stripped.

---

## 9. Verifying the APK

Confirm the SDK actually shipped, rather than assuming:

```powershell
# PowerShell — list dex files inside the APK
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [System.IO.Compression.ZipFile]::OpenRead("YourGame.apk")
$z.Entries.FullName | Where-Object { $_ -like '*.dex' }
$z.Dispose()
```

Extract a dex and search it for `com/google/android/gms/ads`. Presence of those
strings proves the dependency survived into the build.

---

## 10. Before you ship

- [ ] Replace test ad unit IDs **and** the App ID with production values
- [ ] Register the app in the AdMob console; ad units must match the package name
- [ ] Re-enable `minifyRelease` **and** add `-dontwarn com.google.android.libraries.ads.mobile.sdk.**`
- [ ] Switch from debug signing to a real keystore
- [ ] Build an AAB if publishing to Google Play (`buildAppBundle = true`)
- [ ] Add ARMv7 alongside ARM64 if you need to support older devices
- [ ] Implement consent (GDPR via Google's User Messaging Platform) for EU users
- [ ] Fix `JAVA_HOME` so EDM4U resolves normally and the manual Gradle lines can
      eventually be removed
- [ ] Verify AdMob policy compliance — no accidental clicks, no ads over
      interactive UI

---

## 11. Pitfalls, condensed

| Symptom | Real cause |
|---|---|
| AAPT `layout_constraint*` not found | ConstraintLayout missing from Gradle; EDM4U never resolved |
| R8 `Missing class ...ads.mobile.sdk.*` | Undeclared "nextgen" SDK in the AAR; needs `-dontwarn` or minify off |
| EDM4U `Resolution Failed` | `JAVA_HOME` points at the wrong/stale Unity JDK |
| Ads work in Editor, not on device | Device has no working internet |
| No ad ever loads, no logs | `AdManager` missing from scene, or App ID meta-data absent |
| Ads work in dev, fail in release | Code stripping; missing `link.xml` |
| Reward granted without watching | Reward called outside the `onRewardEarned` callback |
| Reward invisible / scene changes instantly | Scene load fired in the same frame as the reward animation |
| Buttons stay disabled forever | `onAdUnavailable` not handled |
| Editor shows only a grey placeholder | Expected — real creatives need a device |
