using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A save/restore point for the melee weapon hitboxes, so tuning them is never a
/// one-way edit.
///
/// WHY IT EXISTS. A swing damages every unit its box overlaps - neither damage
/// collider checks the attacker's currentTarget - so an over-wide box makes a unit's
/// eight blows arrive from two attackers, and on screen it looks like it died in
/// four. That was traced from a level 4 recording on 2026-09-12 (see
/// `Docs/Fight Editing.txt`, Parts 5 and 12). The boxes are tuned by hand in the
/// Inspector; this only remembers the result and puts it back.
///
/// THE DEFAULT IS A CAPTURED SNAPSHOT, NOT A NUMBER IN THIS FILE. Arash re-tuned
/// every collider by hand on 2026-09-12, so a hardcoded table would be stale the
/// moment it was written. Capture writes the CURRENT state of every weapon box to
/// WeaponHitboxDefaults.json; Restore puts that state back. Re-capture whenever a
/// tuning pass is worth keeping.
///
/// WHY THE AUTHORED NUMBERS LOOK NOTHING LIKE THE WIDTH YOU SEE. These colliders sit
/// on weapon BONES scaled between 0.06 and 0.51, so Player_Valkyrie's authored 3.86
/// is 0.73 in the world. The only meaningful figure is size.x * lossyScale.x. Read it
/// with the report item below - never off the Inspector field alone.
/// </summary>
internal static class WeaponHitboxWidth
{
    private const string DefaultsAsset = "Assets/Scripts/Editor/WeaponHitboxDefaults.json";

    /// <summary>
    /// The previous snapshot, kept when a capture overwrites it. One step of undo for
    /// the DEFAULT itself, so a capture run by mistake - after an experiment rather
    /// than after a good tuning pass - is recoverable by renaming this file.
    /// </summary>
    private const string PreviousAsset = "Assets/Scripts/Editor/WeaponHitboxDefaults.prev.json";

    [Serializable]
    private class Entry
    {
        public string path;          // prefab asset path
        public string unit;          // prefab name, so the file is readable
        public float sizeX, sizeY;   // LOCAL collider size
        public float offsetX, offsetY;
        public bool horizontal;      // capsule direction
        public float worldWidth;     // informational: sizeX * lossyScale.x at capture
    }

    [Serializable]
    private class Snapshot
    {
        public string captured;
        public List<Entry> entries = new List<Entry>();
    }

    // ---------------------------------------------------------------- menu items

    /// <summary>
    /// Writes the CURRENT state of every weapon box to the defaults file. Run this
    /// after a tuning pass that is worth keeping.
    /// </summary>
    [MenuItem("Tools/Blasty/Weapon Hitboxes/Capture current widths as default")]
    private static void Capture()
    {
        var snapshot = new Snapshot { captured = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) continue;

            var cap = WeaponBox(go, out _);
            if (!cap) continue;

            snapshot.entries.Add(new Entry
            {
                path = path,
                unit = go.name,
                sizeX = cap.size.x,
                sizeY = cap.size.y,
                offsetX = cap.offset.x,
                offsetY = cap.offset.y,
                horizontal = cap.direction == CapsuleDirection2D.Horizontal,
                worldWidth = cap.size.x * Mathf.Abs(cap.transform.lossyScale.x),
            });
        }

        string full = FullPath(DefaultsAsset);
        if (File.Exists(full)) File.Copy(full, FullPath(PreviousAsset), overwrite: true);
        File.WriteAllText(full, JsonUtility.ToJson(snapshot, true));
        AssetDatabase.Refresh();

        Debug.Log($"[HITBOX] captured {snapshot.entries.Count} weapon box(es) as the default " +
                  $"({snapshot.captured}) -> {DefaultsAsset}" +
                  (File.Exists(FullPath(PreviousAsset)) ? $"\n   previous default kept at {PreviousAsset}" : ""));
    }

    /// <summary>
    /// Puts every weapon box back to the captured default - size, offset and capsule
    /// direction, exactly as recorded.
    ///
    /// Deliberately applies WITHOUT a reach check. These values are somebody's own
    /// tuning, and refusing to restore part of a snapshot would leave the project in
    /// a state that was never captured and never tuned. The report item flags reach
    /// problems instead.
    /// </summary>
    [MenuItem("Tools/Blasty/Weapon Hitboxes/Restore default widths")]
    private static void Restore()
    {
        var snapshot = Load();
        if (snapshot == null) return;

        var sb = new StringBuilder($"[HITBOX] restored the default captured {snapshot.captured}\n");
        int changed = 0, missing = 0;

        foreach (var entry in snapshot.entries)
        {
            var root = PrefabUtility.LoadPrefabContents(entry.path);
            if (!root) { Debug.LogWarning($"[HITBOX] missing prefab: {entry.path}"); missing++; continue; }

            try
            {
                var cap = WeaponBox(root, out _);
                if (!cap) { Debug.LogWarning($"[HITBOX] no weapon box on {entry.path}"); missing++; continue; }

                float before = cap.size.x * Mathf.Abs(cap.transform.lossyScale.x);
                cap.direction = entry.horizontal ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                cap.size = new Vector2(entry.sizeX, entry.sizeY);
                cap.offset = new Vector2(entry.offsetX, entry.offsetY);
                float after = cap.size.x * Mathf.Abs(cap.transform.lossyScale.x);

                PrefabUtility.SaveAsPrefabAsset(root, entry.path);
                changed++;
                sb.AppendLine($"   {entry.unit,-28} world width {before:F3} -> {after:F3}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.AppendLine($"   restored: {changed}" + (missing > 0 ? $", skipped: {missing}" : ""));
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Logs the live width of every weapon box in the project against the captured
    /// default, and flags any unit whose own stand point has fallen OUTSIDE its box -
    /// the silent failure of narrowing too far, where a unit swings and connects with
    /// nothing at all.
    /// </summary>
    [MenuItem("Tools/Blasty/Weapon Hitboxes/Report current widths")]
    private static void Report()
    {
        var snapshot = Load(quiet: true);
        var defaults = new Dictionary<string, float>();
        if (snapshot != null)
            foreach (var e in snapshot.entries) defaults[e.path] = e.worldWidth;

        var sb = new StringBuilder("[HITBOX] current weapon box widths, in WORLD units\n");
        sb.AppendLine($"   {"unit",-28} {"width",7} {"default",9}  {"centre",7} {"standoff",9}");

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) continue;

            var cap = WeaponBox(go, out float standoff);
            if (!cap) continue;

            float width = cap.size.x * Mathf.Abs(cap.transform.lossyScale.x);
            float centre = BoxCentre(go, cap);
            bool reaches = standoff >= centre - width * 0.5f && standoff <= centre + width * 0.5f;
            string def = defaults.TryGetValue(path, out float d) ? d.ToString("F3") : "-";

            sb.AppendLine($"   {go.name,-28} {width,7:F3} {def,9}  {centre,7:F3} {standoff,9:F3}" +
                          (reaches ? "" : "   <<< STAND POINT OUTSIDE THE BOX - this unit cannot land a hit") +
                          (cap.direction == CapsuleDirection2D.Vertical ? "   (vertical capsule)" : ""));
        }
        Debug.Log(sb.ToString());
    }

    // ------------------------------------------------------------------- helpers

    private static Snapshot Load(bool quiet = false)
    {
        string full = FullPath(DefaultsAsset);
        if (!File.Exists(full))
        {
            if (!quiet)
                Debug.LogError($"[HITBOX] no default captured yet. Run " +
                               $"Tools/Blasty/Weapon Hitboxes/Capture current widths as default first.");
            return null;
        }

        var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(full));
        if (snapshot == null || snapshot.entries == null || snapshot.entries.Count == 0)
        {
            if (!quiet) Debug.LogError($"[HITBOX] {DefaultsAsset} is empty or unreadable.");
            return null;
        }
        return snapshot;
    }

    private static string FullPath(string assetPath) =>
        Path.Combine(Directory.GetCurrentDirectory(), assetPath);

    /// <summary>
    /// The weapon box a unit actually swings with - the one its manager holds, not any
    /// collider that happens to be on the prefab - plus the distance that unit comes to
    /// rest at, so callers can check the box still covers its target.
    /// </summary>
    private static CapsuleCollider2D WeaponBox(GameObject root, out float standoff)
    {
        standoff = 0f;
        var player = root.GetComponent<PlayerManager>();
        var enemy = root.GetComponent<EnemyManager>();
        if (!player && !enemy) return null;

        var cap = (player ? player.playerDamageCollider : (Collider2D)enemy.enemyDamageCollider) as CapsuleCollider2D;
        if (!cap) return null;

        float range = player ? player.maxAttackRange : 0f;
        if (!player)
        {
            var loco = root.GetComponent<EnemyLocoMotion>();
            range = loco ? loco.stoppingDistance : 0f;
        }
        standoff = MeleeEngagement.Standoff(range);
        return cap;
    }

    /// <summary>
    /// How far the box sits from the unit's own origin along the direction it faces.
    /// ABSOLUTE on purpose: a prefab authored facing left carries a negative scale, and
    /// comparing a positive standoff against its mirrored span reports every such unit
    /// as broken.
    /// </summary>
    private static float BoxCentre(GameObject root, CapsuleCollider2D cap) =>
        Mathf.Abs((cap.transform.position.x - root.transform.position.x)
                  + cap.offset.x * cap.transform.lossyScale.x);
}
