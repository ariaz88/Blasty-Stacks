using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bulk-converts every TMP_FontAsset under Assets/ from Dynamic to Static atlas population.
///
/// A Dynamic font asset treats its glyph table + atlas texture as a rebuildable cache, so
/// deleting Library/ (or cloning the repo fresh) leaves the atlas empty and the text renders
/// blank until someone opens Font Asset Creator and presses Generate Font Atlas by hand.
/// Static bakes the glyphs permanently into the .asset, which survives reimports and builds.
///
/// Everything is written through SerializedObject because TMP marks clearDynamicDataOnBuild
/// and the sourceFontFile setter internal - they are not reachable from Assembly-CSharp.
/// </summary>
public static class TMPFontAssetStaticBaker
{
    const string k_MenuRoot = "Tools/Blasty/Fonts/";

    // Printable ASCII. Always baked in, on top of whatever the asset already contains, so a
    // font whose atlas was already wiped still comes back with a usable character set.
    const int k_BaseCharFirst = 32;
    const int k_BaseCharLast = 126;

    [MenuItem(k_MenuRoot + "Bake All TMP Fonts To Static", false, 10)]
    static void BakeAll()
    {
        bool ok = EditorUtility.DisplayDialog(
            "Bake TMP fonts to Static",
            "Every TMP_FontAsset under Assets/ will be switched to Static atlas population, " +
            "have Clear Dynamic Data On Build turned off, and have its atlas repopulated from " +
            "its source font file.\n\nThis rewrites the .asset files. Commit or stash first if " +
            "you want a way back.",
            "Bake", "Cancel");

        if (ok)
            Run(false);
    }

    [MenuItem(k_MenuRoot + "Report Only (Dry Run)", false, 11)]
    static void ReportOnly()
    {
        Run(true);
    }

    static void Run(bool dryRun)
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        var report = new StringBuilder();
        int baked = 0, skipped = 0, failed = 0;

        report.AppendLine(dryRun
            ? "=== TMP font static bake - DRY RUN (nothing written) ==="
            : "=== TMP font static bake ===");

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                // Fonts living inside a package are read-only; leave them alone.
                if (!path.StartsWith("Assets/"))
                    continue;

                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                    continue;

                EditorUtility.DisplayProgressBar("Baking TMP fonts", path, (float)i / Mathf.Max(1, guids.Length));

                switch (Process(font, path, dryRun, report))
                {
                    case Result.Baked:   baked++;   break;
                    case Result.Skipped: skipped++; break;
                    case Result.Failed:  failed++;  break;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        FixTmpSettings(dryRun, report);

        if (!dryRun && baked > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        report.AppendLine();
        report.AppendFormat("{0} baked, {1} already ok, {2} failed.", baked, skipped, failed);

        if (failed > 0)
            Debug.LogWarning(report.ToString());
        else
            Debug.Log(report.ToString());
    }

    enum Result { Baked, Skipped, Failed }

    static Result Process(TMP_FontAsset font, string path, bool dryRun, StringBuilder report)
    {
        var so = new SerializedObject(font);
        SerializedProperty modeProp  = so.FindProperty("m_AtlasPopulationMode");
        SerializedProperty clearProp = so.FindProperty("m_ClearDynamicDataOnBuild");
        SerializedProperty guidProp  = so.FindProperty("m_SourceFontFileGUID");
        SerializedProperty fileProp  = so.FindProperty("m_SourceFontFile");

        if (modeProp == null)
        {
            report.AppendFormat("  FAIL   {0} - no m_AtlasPopulationMode field (unexpected TMP version)\n", path);
            return Result.Failed;
        }

        bool isStatic  = modeProp.intValue == (int)AtlasPopulationMode.Static;
        bool clearFlag = clearProp != null && clearProp.boolValue;
        bool hasGlyphs = font.characterTable != null && font.characterTable.Count > 0;

        // Already correct - nothing to do.
        if (isStatic && hasGlyphs && !clearFlag)
        {
            report.AppendFormat("  ok     {0} ({1} chars)\n", path, font.characterTable.Count);
            return Result.Skipped;
        }

        // Static and populated, but the build-time wipe flag is still set: flag flip is enough.
        if (isStatic && hasGlyphs)
        {
            if (!dryRun)
            {
                clearProp.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(font);
            }
            report.AppendFormat("  {0}  {1} - clear ClearDynamicDataOnBuild\n",
                dryRun ? "would fix" : "FIXED   ", path);
            return Result.Baked;
        }

        // Everything below needs the source .ttf/.otf so the atlas can be repopulated.
        Font source = ResolveSourceFont(font, so, guidProp, fileProp, path, report);
        if (source == null)
        {
            report.AppendFormat("  FAIL   {0} - no source font file found; assign it by hand in the Inspector\n", path);
            return Result.Failed;
        }

        if (dryRun)
        {
            report.AppendFormat("  would bake {0} (mode={1}, clearOnBuild={2}, chars={3}, source={4})\n",
                path, (AtlasPopulationMode)modeProp.intValue, clearFlag,
                font.characterTable == null ? 0 : font.characterTable.Count, source.name);
            return Result.Baked;
        }

        // 1. Point the asset at its source font and force Dynamic - TryAddCharacters refuses
        //    to run on a Static asset, and it needs a loadable font face.
        if (guidProp != null)
            guidProp.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        if (fileProp != null)
            fileProp.objectReferenceValue = source;
        modeProp.intValue = (int)AtlasPopulationMode.Dynamic;
        so.ApplyModifiedPropertiesWithoutUndo();

        font.ReadFontAssetDefinition();

        // 2. Bake the glyphs into the atlas texture.
        string characters = BuildCharacterSet(font);
        string missing;
        font.TryAddCharacters(characters, out missing, includeFontFeatures: true);

        // 3. Freeze it. Static convention: keep the GUID, null the live reference.
        //    A fresh SerializedObject, so the glyph/atlas data TryAddCharacters just wrote
        //    onto the instance is what gets serialized back out.
        var frozen = new SerializedObject(font);
        frozen.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static;

        SerializedProperty frozenClear = frozen.FindProperty("m_ClearDynamicDataOnBuild");
        if (frozenClear != null)
            frozenClear.boolValue = false;

        SerializedProperty frozenFile = frozen.FindProperty("m_SourceFontFile");
        if (frozenFile != null)
            frozenFile.objectReferenceValue = null;

        frozen.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(font);

        report.AppendFormat("  BAKED  {0} - source '{1}', {2} chars",
            path, source.name, font.characterTable == null ? 0 : font.characterTable.Count);

        if (!string.IsNullOrEmpty(missing))
            report.AppendFormat(" ({0} not in the font face)", missing.Length);

        report.AppendLine();
        return Result.Baked;
    }

    /// <summary>
    /// Union of the printable-ASCII baseline and whatever the asset already carries, so a
    /// bake never loses characters that were previously generated.
    /// </summary>
    static string BuildCharacterSet(TMP_FontAsset font)
    {
        var unicodes = new HashSet<uint>();

        for (int c = k_BaseCharFirst; c <= k_BaseCharLast; c++)
            unicodes.Add((uint)c);

        if (font.characterTable != null)
        {
            foreach (TMP_Character ch in font.characterTable)
            {
                if (ch != null && ch.unicode != 0)
                    unicodes.Add(ch.unicode);
            }
        }

        var sb = new StringBuilder(unicodes.Count);
        foreach (uint u in unicodes)
        {
            // Surrogate halves and out-of-range values would throw in ConvertFromUtf32.
            if (u > 0x10FFFF || (u >= 0xD800 && u <= 0xDFFF))
                continue;

            sb.Append(char.ConvertFromUtf32((int)u));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Finds the .ttf/.otf behind a font asset: the live reference first, then the stored GUID,
    /// then a name match against every Font in the project (face family+style, or the asset
    /// name minus its SDF/Fallback decoration). Same-folder candidates win ties.
    /// </summary>
    static Font ResolveSourceFont(TMP_FontAsset font, SerializedObject so, SerializedProperty guidProp,
                                  SerializedProperty fileProp, string path, StringBuilder report)
    {
        var direct = fileProp != null ? fileProp.objectReferenceValue as Font : null;
        if (direct != null)
            return direct;

        if (guidProp != null && !string.IsNullOrEmpty(guidProp.stringValue))
        {
            var byGuid = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guidProp.stringValue));
            if (byGuid != null)
                return byGuid;
        }

        string assetKey = Normalize(font.name);

        SerializedProperty familyProp = so.FindProperty("m_FaceInfo.m_FamilyName");
        SerializedProperty styleProp = so.FindProperty("m_FaceInfo.m_StyleName");
        string faceKey = Normalize((familyProp != null ? familyProp.stringValue : "") +
                                   (styleProp != null ? styleProp.stringValue : ""));

        string folder = (Path.GetDirectoryName(path) ?? "").Replace('\\', '/');

        Font sameFolder = null, anyFolder = null;
        int matches = 0;

        foreach (string g in AssetDatabase.FindAssets("t:Font"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.StartsWith("Assets/"))
                continue;

            string key = Normalize(Path.GetFileNameWithoutExtension(p));
            if (key != assetKey && key != faceKey)
                continue;

            var candidate = AssetDatabase.LoadAssetAtPath<Font>(p);
            if (candidate == null)
                continue;

            matches++;

            if (anyFolder == null)
                anyFolder = candidate;

            if (sameFolder == null && (Path.GetDirectoryName(p) ?? "").Replace('\\', '/') == folder)
                sameFolder = candidate;
        }

        Font picked = sameFolder ?? anyFolder;

        if (matches > 1 && picked != null)
        {
            report.AppendFormat("  note   {0} - {1} font files matched; picked '{2}'\n",
                path, matches, AssetDatabase.GetAssetPath(picked));
        }

        return picked;
    }

    /// <summary>Lowercase, alphanumerics only, with the SDF/fallback decoration stripped.</summary>
    static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.Replace("sdf", "").Replace("fallback", "").ToString();
    }

    /// <summary>
    /// TMP Settings seeds ClearDynamicDataOnBuild onto every newly created font asset, so
    /// turning it off there stops the problem coming back with the next font.
    /// </summary>
    static void FixTmpSettings(bool dryRun, StringBuilder report)
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.StartsWith("Assets/"))
                continue;

            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (settings == null)
                continue;

            var so = new SerializedObject(settings);
            SerializedProperty clearProp = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearProp == null || !clearProp.boolValue)
                continue;

            if (!dryRun)
            {
                clearProp.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            report.AppendFormat("  {0}  {1} - cleared default ClearDynamicDataOnBuild\n",
                dryRun ? "would fix" : "FIXED ", path);
        }
    }
}
