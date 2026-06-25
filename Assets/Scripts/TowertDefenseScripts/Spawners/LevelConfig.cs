using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TD/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("Stage / Chapter")]
    public int levelNumber = 1;
    public float startDelay = 2f;
    public float targetStageDuration = 90f;

    [Header("Waves (sequential)")]
    public List<Wave> waves = new();

}

public enum SpawnFormation
{
    AllTogetherGrid,  // interleaved types, laid out as a grid in the area
    TwoRows           // row 0 (front) then row 1 (back) with optional delay
}
public enum FrontRowAnchor { MinY, MaxY }   // front is at the bottom or the top?


[Serializable]
public class Wave
{
    public string name = "Wave";
    public float delayBeforeWave = 5f;

    [Header("Formation")]
    public SpawnFormation formation = SpawnFormation.AllTogetherGrid;

    [Tooltip("Spawn area rectangle (world space). x:[minX,maxX], y:[minY,maxY].")]
    public Vector2 spawnMin = new Vector2(1f, 2f);
    public Vector2 spawnMax = new Vector2(10f, 4f);

    [Tooltip("Columns for grid layout (AllTogetherGrid). If 0, auto = ceil(sqrt(total)).")]
    public int gridColumns = 0;

    [Tooltip("Distance between adjacent slots (x,y) as a minimum; auto-spaced if 0.")]
    public Vector2 minSlotSpacing = new Vector2(0.4f, 0.4f);


    [Header("TwoRows options")]
    [Tooltip("Vertical offset between front/back rows (world units). If 0, auto from area height.")]

    public FrontRowAnchor frontAnchor = FrontRowAnchor.MinY;   // <-- set this in Inspector

    public float rowYOffset = 0f;
    [Tooltip("Delay between front row and back row (seconds).")]
    public float secondRowDelay = 0.2f;

    [Header("Entries (types and counts)")]
    public List<WaveEntry> entries = new();

    [Header("Optional: cap concurrency for this wave (0 = unlimited)")]
    public int concurrencyCap = 0; // you can ignore this if you’re not using drip-spawn
}

public enum RowIndex { FrontRow = 0, BackRow = 1 }

[Serializable]
public class WaveEntry
{
    [Header("Archetype")]
    public GameObject enemyPrefab;   // prefab has EnemyManager
    public UnitStatsSO statsBase;    // which UnitStatsSO to use

    [Header("Counts & Placement")]
    [Min(1)] public int count = 3;

    [Tooltip("Only used for TwoRows. Choose which row this entry belongs to.")]
    public RowIndex row = RowIndex.FrontRow;

    [Header("Per-entry unit level (progression, not CP weights)")]
    [Min(1)] public int unitLevel = 1;
}
