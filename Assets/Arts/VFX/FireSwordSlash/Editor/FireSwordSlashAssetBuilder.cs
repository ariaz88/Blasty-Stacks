using System.IO;
using System.Linq;
using Blasty.Vfx;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Blasty.Editor.Vfx
{
    /// <summary>Creates the imported animation, additive material, controller and reusable prefab.</summary>
    public static class FireSwordSlashAssetBuilder
    {
        private const string Root = "Assets/Arts/VFX/FireSwordSlash";
        private const string TexturePath = Root + "/Sprite Sheets/FireSwordSlash_CleanAlpha_HQ_2048_4x4_V4.png";
        private const string ShaderPath = Root + "/FireSwordSlashURPAdditive.shader";
        private const string MaterialPath = Root + "/FireSwordSlash_Additive.mat";
        private const string AnimationPath = Root + "/FireSwordSlash_16Frames.anim";
        private const string ControllerPath = Root + "/FireSwordSlash.controller";
        public const string PrefabPath = Root + "/FireSwordSlashVfx.prefab";

        [MenuItem("Tools/Blasty/VFX/Create Fire Sword Slash Assets")]
        public static void CreateAssets()
        {
            ConfigureTexture();
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            if (sprites.Length != 16)
            {
                Debug.LogError("Fire Sword Slash needs exactly 16 sliced sprites. Import setup did not complete.");
                return;
            }

            Material material = CreateMaterial();
            AnimationClip clip = CreateClip(sprites);
            AnimatorController controller = CreateController(clip);
            CreatePrefab(material, controller, sprites[0]);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("Fire Sword Slash VFX created at " + Root);
        }

        private static void ConfigureTexture()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Missing source texture", TexturePath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            // DXT block compression creates visible checkerboard artifacts in
            // the soft, semi-transparent flame edges. Keep this small VFX sheet
            // uncompressed so its authored alpha is rendered faithfully.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.compressionQuality = 100;
            // V4 is 2048x2048: each of its 4x4 cells is 512px. Matching PPU
            // keeps each slash frame at the same 1-world-unit size as V3.
            importer.spritePixelsPerUnit = 512;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.spritesheet = BuildGrid(512);
        }

        private static SpriteMetaData[] BuildGrid(int cellSize)
        {
            var sprites = new SpriteMetaData[16];
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                int index = row * 4 + column;
                sprites[index] = new SpriteMetaData
                {
                    name = $"FireSwordSlash_{index:00}",
                    rect = new Rect(column * cellSize, (3 - row) * cellSize, cellSize, cellSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }
            return sprites;
        }

        private static Material CreateMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new FileNotFoundException("Missing additive shader", ShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimationClip CreateClip(Sprite[] sprites)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, AnimationPath);
            }
            clip.frameRate = 60f;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var keys = new ObjectReferenceKeyframe[16];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / 60f, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateController(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.layers[0].stateMachine.states = new ChildAnimatorState[0];
            AnimatorState state = controller.layers[0].stateMachine.AddState("FireSwordSlash");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreatePrefab(Material material, RuntimeAnimatorController controller, Sprite defaultSprite)
        {
            GameObject instance = new GameObject("FireSwordSlashVfx");
            try
            {
                var renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = material;
                renderer.sprite = defaultSprite;
                renderer.sortingLayerName = "Default";
                var animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                instance.AddComponent<FireSwordSlashVfx>();
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    // The first script reload after these source files are added creates the
    // Unity assets automatically; the menu item remains useful for rebuilding
    // them after changing the source texture.
    [InitializeOnLoad]
    internal static class FireSwordSlashAutoBuilder
    {
        static FireSwordSlashAutoBuilder()
        {
            EditorApplication.delayCall += CreateIfMissing;
        }

        private static void CreateIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FireSwordSlashAssetBuilder.PrefabPath) == null)
                FireSwordSlashAssetBuilder.CreateAssets();
        }
    }
}
