# Android build for this Unity project

When the user asks for an Android build (including a short request in Persian), run
`Build Android.ps1` from the project root. It uses the project's exact Unity
version and `AndroidBuildAutomation.BuildFromCommandLine`, writes the APK to
`Builds/Android/StackyWarriors.apk`, and records a fresh build log. When this
project's Unity Editor is open, it sends the build request to the Editor instead.

The Editor also has `Tools > Build > Build Android APK (Automatic)` for one click.
Both paths use the same IL2CPP, ARM64, APK, debug-signing, and enabled-scene
settings. The automation retries transient failures up to three times. If it
reports a source or configuration error, inspect the log, fix the specific cause,
and rerun until the fresh `build-result.txt` starts with `Succeeded`.

Do not report success from an old APK or an old result file. Verify the fresh APK
timestamp and `adMobAppId=OK` in the result, and prefer checking the APK manifest
with Android SDK `aapt` if the AdMob check is disputed. Preserve existing project
changes while fixing build errors.
