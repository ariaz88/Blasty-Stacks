// VFX Kit - builds a prefab of Particle Systems from a <name>.vfx.json spec.
//
// Every spec under Assets/VFXKit/Effects/ is built:
//   - automatically, when its .vfx.json is imported (drop the folder in);
//   - from the menu, Tools > VFX Kit > Build All Effects;
//   - from the command line, for the skill's `unity build`:
//       Unity -batchmode -projectPath <p> -executeMethod VFXKit.EditorTools.Builder.BuildAllFromCommandLine
//
// Conventions shared with the Python preview (see spec.py):
//   - each layer object is rotated -90 on X, so the shape's +Z is world up;
//   - velocity-over-lifetime is in WORLD space, y up;
//   - angles in the spec are degrees and are converted to radians here;
//   - trail lifetime in the spec is seconds and becomes Unity's ratio here.
// Written for C# 7.3 (Unity 2019.4+) - no switch expressions on purpose.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VFXKit.EditorTools
{
    public static class Builder
    {
        public const string Root = "Assets/VFXKit";
        public const string EffectsDir = Root + "/Effects";

        [MenuItem("Tools/VFX Kit/Build All Effects")]
        public static void BuildAllMenu()
        {
            int n = BuildAll();
            Debug.Log("[VFXKit] built " + n + " effect(s) under " + EffectsDir);
        }

        public static int BuildAll()
        {
            AssetDatabase.Refresh();
            if (!Directory.Exists(EffectsDir)) return 0;
            var files = Directory.GetFiles(EffectsDir, "*.vfx.json", SearchOption.AllDirectories);
            foreach (var f in files) Build(f.Replace('\\', '/'));
            return files.Length;
        }

        // Batchmode entry. Always exits, with 0 only if every spec built.
        public static void BuildAllFromCommandLine()
        {
            try
            {
                int n = BuildAll();
                Debug.Log("[VFXKit] OK built " + n);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[VFXKit] FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        public static GameObject Build(string jsonPath)
        {
            string dir = Path.GetDirectoryName(jsonPath).Replace('\\', '/');
            var spec = JsonUtility.FromJson<EffectSpec>(File.ReadAllText(jsonPath));
            if (spec == null || spec.layers == null || spec.layers.Length == 0)
                throw new Exception(jsonPath + " has no layers");

            var tex = new Dictionary<string, Texture2D>();
            foreach (var ts in spec.textures ?? new TextureSpec[0])
            {
                string p = dir + "/textures/" + ts.file;
                bool data = ts.wrap == "repeat";          // tileable noise is data, not colour
                ConfigureTexture(p, data, data);
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t == null) throw new Exception("texture not found: " + p);
                tex[ts.id] = t;
            }

            string matDir = dir + "/Materials";
            if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(dir, "Materials");

            // The root carries a Particle System with no emission and no renderer,
            // so that Play()/Stop() on the prefab's root drives every layer - the
            // way Unity's own effect prefabs are usually organised.
            var root = new GameObject(spec.name);
            var rootPs = root.AddComponent<ParticleSystem>();
            rootPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var rm = rootPs.main;
            rm.duration = Mathf.Max(0.05f, spec.duration);
            rm.loop = spec.loop;
            rm.playOnAwake = true;
            var re = rootPs.emission; re.enabled = false;
            var rs = rootPs.shape; rs.enabled = false;
            root.GetComponent<ParticleSystemRenderer>().enabled = false;

            foreach (var L in spec.layers.OrderBy(l => l.sort))
            {
                if (!L.enabled) continue;
                var go = new GameObject(L.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Configure(ps, go.GetComponent<ParticleSystemRenderer>(), L, spec, tex, matDir);
            }

            string prefabPath = dir + "/" + spec.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        static void Configure(ParticleSystem ps, ParticleSystemRenderer r, LayerSpec L, EffectSpec S,
                              Dictionary<string, Texture2D> tex, string matDir)
        {
            var main = ps.main;
            main.duration = Mathf.Max(0.05f, S.duration);
            main.loop = S.loop;
            main.prewarm = S.loop && S.prewarm;
            main.startDelay = L.delay;
            main.startLifetime = MinMax(L.lifetime, 1f);
            main.startSpeed = MinMax(L.speed, 1f);
            main.startSize = MinMax(L.size, 1f);
            main.startRotation = MinMax(L.rotation, Mathf.Deg2Rad);
            main.startColor = Color.white;
            main.gravityModifier = L.gravity;
            main.maxParticles = Mathf.Max(1, L.max);
            main.simulationSpace = L.space == "world" ? ParticleSystemSimulationSpace.World
                                                      : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = true;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = L.rate;
            var bursts = (L.bursts ?? new BurstSpec[0])
                .Select(b => new ParticleSystem.Burst(b.time, (short)Mathf.Clamp(b.count, 0, 32767),
                                                      (short)Mathf.Clamp(b.count, 0, 32767),
                                                      Mathf.Max(1, b.cycles), Mathf.Max(0.0001f, b.interval)))
                .ToArray();
            em.SetBursts(bursts);

            var sh = ps.shape;
            var s = L.shape;
            sh.enabled = true;
            switch (s.type)
            {
                case "sphere": sh.shapeType = ParticleSystemShapeType.Sphere; break;
                case "hemisphere": sh.shapeType = ParticleSystemShapeType.Hemisphere; break;
                case "cone": sh.shapeType = ParticleSystemShapeType.Cone; break;
                case "circle": sh.shapeType = ParticleSystemShapeType.Circle; break;
                case "box": sh.shapeType = ParticleSystemShapeType.Box; break;
                default: sh.shapeType = ParticleSystemShapeType.Cone; break;   // "point"
            }
            if (s.type == "point")
            {
                sh.angle = 0f;
                sh.radius = 0.0001f;
            }
            else
            {
                sh.radius = Mathf.Max(0.0001f, s.radius);
                sh.angle = s.angle;
                sh.arc = s.arc;
                sh.radiusThickness = s.thickness;
                if (s.type == "box") sh.scale = V3(s.box);
            }
            sh.rotation = V3(s.rotation);
            sh.position = V3(s.position);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(L.color.Select(c => new GradientColorKey(Hex(c.c), c.t)).ToArray(),
                      L.color.Select(c => new GradientAlphaKey(c.a, c.t)).ToArray());
            col.color = new ParticleSystem.MinMaxGradient(g);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            float peak = Mathf.Max(0.0001f, L.size_curve.Max(k => k.v));
            sol.size = new ParticleSystem.MinMaxCurve(peak, Curve(L.size_curve, 1f / peak));

            var v = L.velocity;
            bool anyVel = v.x != 0 || v.y != 0 || v.z != 0 || v.radial != 0 || v.orbital != 0;
            var vol = ps.velocityOverLifetime;
            vol.enabled = anyVel;
            if (anyVel)
            {
                vol.space = ParticleSystemSimulationSpace.World;
                vol.x = v.x; vol.y = v.y; vol.z = v.z;
                vol.radial = v.radial;
                vol.orbitalZ = v.orbital;
            }

            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = L.drag > 0;
            if (L.drag > 0)
            {
                // Only drag: the speed limit is out of reach and dampening off.
                lv.limit = 10000f;
                lv.dampen = 0f;
                lv.drag = L.drag;
                lv.multiplyDragByParticleSize = false;
                lv.multiplyDragByParticleVelocity = false;
            }

            var rol = ps.rotationOverLifetime;
            rol.enabled = L.spin[0] != 0 || L.spin[1] != 0;
            if (rol.enabled) rol.z = MinMax(L.spin, Mathf.Deg2Rad);

            var nz = ps.noise;
            nz.enabled = L.noise.strength > 0;
            if (nz.enabled)
            {
                nz.strength = L.noise.strength;
                nz.frequency = L.noise.frequency;
                nz.scrollSpeed = L.noise.scroll;
                nz.damping = true;
                nz.quality = ParticleSystemNoiseQuality.Medium;
            }

            var tsa = ps.textureSheetAnimation;
            int frames = L.tiles[0] * L.tiles[1];
            tsa.enabled = frames > 1;
            if (tsa.enabled)
            {
                tsa.mode = ParticleSystemAnimationMode.Grid;
                tsa.numTilesX = L.tiles[0];
                tsa.numTilesY = L.tiles[1];
                tsa.animation = ParticleSystemAnimationType.WholeSheet;
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                tsa.cycleCount = Mathf.Max(1, L.flipbook_cycles);
            }

            var tr = ps.trails;
            tr.enabled = L.trail.enabled;
            if (tr.enabled)
            {
                float avgLife = Mathf.Max(0.01f, (L.lifetime[0] + L.lifetime[1]) * 0.5f);
                tr.mode = ParticleSystemTrailMode.PerParticle;
                tr.lifetime = Mathf.Clamp01(L.trail.lifetime / avgLife);
                tr.minVertexDistance = Mathf.Max(0.001f, L.trail.min_vertex);
                float wpk = Mathf.Max(0.0001f, L.trail.width.Max(k => k.v));
                tr.widthOverTrail = new ParticleSystem.MinMaxCurve(wpk, Curve(L.trail.width, 1f / wpk));
                tr.inheritParticleColor = true;
                tr.dieWithParticles = true;
                tr.sizeAffectsWidth = true;
                tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
            }

            switch (L.render)
            {
                case "stretched": r.renderMode = ParticleSystemRenderMode.Stretch; break;
                case "horizontal": r.renderMode = ParticleSystemRenderMode.HorizontalBillboard; break;
                case "vertical": r.renderMode = ParticleSystemRenderMode.VerticalBillboard; break;
                default: r.renderMode = ParticleSystemRenderMode.Billboard; break;
            }
            if (L.render == "stretched")
            {
                r.lengthScale = L.length_scale;
                r.velocityScale = L.velocity_scale;
                r.cameraVelocityScale = 0f;
            }
            // The default max size is half the screen, which silently clamps big
            // flashes and ground rings when the camera is close.
            r.maxParticleSize = 5f;
            r.sortingOrder = L.sort;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            Texture2D mainTex;
            if (!tex.TryGetValue(L.texture, out mainTex))
                throw new Exception("layer " + L.name + ": texture '" + L.texture + "' is not in the spec's texture list");
            r.sharedMaterial = MakeMaterial(L, mainTex, tex, matDir, "");
            if (L.trail.enabled)
            {
                Texture2D trailTex;
                tex.TryGetValue("trail", out trailTex);
                r.trailMaterial = MakeMaterial(L, trailTex != null ? trailTex : mainTex, tex, matDir, "_trail");
            }
        }

        static Material MakeMaterial(LayerSpec L, Texture2D mainTex, Dictionary<string, Texture2D> tex,
                                     string matDir, string suffix)
        {
            string shaderName;
            switch (L.shader)
            {
                case "alpha": shaderName = "VFXKit/AlphaBlend"; break;
                case "premultiplied": shaderName = "VFXKit/Premultiplied"; break;
                case "erode": shaderName = "VFXKit/Erode"; break;
                case "scroll": shaderName = "VFXKit/Scroll"; break;
                default: shaderName = "VFXKit/Additive"; break;
            }
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new Exception("Shader " + shaderName + " not found - is " + Root + "/Shaders in the project?");
            string path = matDir + "/" + L.name + suffix + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            mat.SetTexture("_MainTex", mainTex);
            mat.SetFloat("_Intensity", L.intensity);
            if (mat.HasProperty("_Softness")) mat.SetFloat("_Softness", L.softness);
            if (L.shader == "scroll")
            {
                Texture2D noise;
                if (tex.TryGetValue(L.scroll.noise, out noise)) mat.SetTexture("_NoiseTex", noise);
                mat.SetFloat("_NoiseScale", L.scroll.scale);
                mat.SetVector("_SpeedA", new Vector4(L.scroll.speed_a[0], L.scroll.speed_a[1], 0, 0));
                mat.SetVector("_SpeedB", new Vector4(L.scroll.speed_b[0], L.scroll.speed_b[1], 0, 0));
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ConfigureTexture(string path, bool repeat, bool linearData)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                imp = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (imp == null) throw new Exception("not an importable texture: " + path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            // Dilates colour into transparent pixels so mips and bilinear filtering
            // do not pull a dark fringe in from the (colourless) transparent area.
            imp.alphaIsTransparency = !linearData;
            imp.sRGBTexture = !linearData;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------ helpers

        static ParticleSystem.MinMaxCurve MinMax(float[] p, float scale)
        {
            if (p == null || p.Length == 0) return new ParticleSystem.MinMaxCurve(0f);
            if (p.Length == 1 || Mathf.Approximately(p[0], p[1])) return new ParticleSystem.MinMaxCurve(p[0] * scale);
            return new ParticleSystem.MinMaxCurve(p[0] * scale, p[1] * scale);
        }

        static AnimationCurve Curve(CurveKey[] keys, float scale)
        {
            var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v * scale)).ToArray());
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            }
            return c;
        }

        static Vector3 V3(float[] a)
        {
            if (a == null || a.Length < 3) return Vector3.zero;
            return new Vector3(a[0], a[1], a[2]);
        }

        static Color Hex(string h)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(h.StartsWith("#") ? h : "#" + h, out c)) c = Color.magenta;
            return c;
        }
    }

    // Rebuild a prefab whenever its spec is (re)imported - copy an effect folder
    // into Assets/VFXKit/Effects and the prefab appears next to it.
    class SpecPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Application.isBatchMode) return;   // the command line builds explicitly
            var specs = imported.Where(p => p.EndsWith(".vfx.json") && p.StartsWith(Builder.EffectsDir)).ToArray();
            if (specs.Length == 0) return;
            // Never create assets from inside an import callback: defer a tick.
            EditorApplication.delayCall += () =>
            {
                foreach (var p in specs)
                {
                    try
                    {
                        var prefab = Builder.Build(p);
                        Debug.Log("[VFXKit] built " + AssetDatabase.GetAssetPath(prefab), prefab);
                    }
                    catch (Exception e) { Debug.LogError("[VFXKit] could not build " + p + ": " + e.Message); }
                }
            };
        }
    }
}
