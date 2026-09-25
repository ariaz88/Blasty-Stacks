// VFX Kit - renders an effect prefab to PNG frames, so the skill can put what
// Unity ACTUALLY draws beside the reference, instead of trusting the preview.
//
// Same framing and times as the Python preview: an orthographic camera looking
// down +Z at the spec's view (centre and size), frames at t = 1/fps, 2/fps, ...
// Each frame re-simulates from zero with a fixed seed (ParticleSystem.Simulate
// with restart), which is deterministic - stepping a playing system is not.
//
// Command line (needs graphics - do NOT pass -nographics):
//   Unity -batchmode -projectPath <p> -executeMethod VFXKit.EditorTools.Capture.CaptureFromCommandLine
//         -vfxName <name> [-vfxFps 30] [-vfxSize 320]
// Frames land in <project>/VFXKitCaptures/<name>/ (outside Assets, never imported).
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VFXKit.EditorTools
{
    public static class Capture
    {
        [MenuItem("Tools/VFX Kit/Capture Selected Effect")]
        static void CaptureSelected()
        {
            var go = Selection.activeObject as GameObject;
            string path = go != null ? AssetDatabase.GetAssetPath(go) : null;
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Builder.EffectsDir))
            {
                Debug.LogWarning("[VFXKit] select an effect prefab under " + Builder.EffectsDir);
                return;
            }
            string outDir = CaptureByName(Path.GetFileNameWithoutExtension(path), 30, 320);
            Debug.Log("[VFXKit] captured to " + outDir);
            EditorUtility.RevealInFinder(outDir);
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                string name = Arg("-vfxName");
                if (string.IsNullOrEmpty(name)) throw new Exception("missing -vfxName");
                int fps = int.Parse(Arg("-vfxFps") ?? "30");
                int size = int.Parse(Arg("-vfxSize") ?? "320");
                string outDir = CaptureByName(name, fps, size);
                Debug.Log("[VFXKit] OK capture " + outDir);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[VFXKit] FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        public static string CaptureByName(string name, int fps, int size)
        {
            string dir = Builder.EffectsDir + "/" + name;
            string json = dir + "/" + name + ".vfx.json";
            var spec = JsonUtility.FromJson<EffectSpec>(File.ReadAllText(json));
            var prefab = Builder.Build(json);   // always capture what the spec says now

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = Vector3.zero;
            var camGo = new GameObject("VFXKitCaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            float viewSize = spec.view != null && spec.view.size > 0 ? spec.view.size : 3f;
            float cx = spec.view != null && spec.view.center != null && spec.view.center.Length > 1 ? spec.view.center[0] : 0f;
            float cy = spec.view != null && spec.view.center != null && spec.view.center.Length > 1 ? spec.view.center[1] : 0f;
            cam.orthographicSize = viewSize * 0.5f;
            cam.transform.position = new Vector3(cx, cy, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.18f, 0.21f, 1f);   // the preview's background
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);

            var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = (uint)(1234 + i * 101);
            }

            float maxLife = spec.layers.Max(l => l.delay + l.lifetime[1]);
            float span, offset = 0f;
            if (spec.loop)
            {
                // Steady state, phase-aligned to whole cycles like the preview.
                span = spec.duration;
                offset = Mathf.Ceil(maxLife / spec.duration + 1f) * spec.duration;
            }
            else
            {
                float lastEmit = spec.layers.Max(l => l.delay
                    + (l.rate > 0 ? spec.duration : 0f)
                    + (l.bursts == null || l.bursts.Length == 0 ? 0f
                       : l.bursts.Max(b => b.time + (b.cycles - 1) * b.interval)));
                span = lastEmit + maxLife + 0.05f;
            }

            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "VFXKitCaptures", name);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            int frames = Mathf.CeilToInt(span * fps);
            var times = new System.Text.StringBuilder();
            try
            {
                for (int f = 0; f < frames; f++)
                {
                    float t = (f + 1) / (float)fps;
                    foreach (var ps in systems) ps.Simulate(t + offset, false, true, true);
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(outDir, f.ToString("0000") + ".png"), tex.EncodeToPNG());
                    times.AppendLine(t.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture));
                }
                File.WriteAllText(Path.Combine(outDir, "times.txt"), times.ToString());
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(inst);
            }
            return outDir;
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
