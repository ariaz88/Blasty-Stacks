using System.Collections.Generic;
using UnityEngine;

public static class PieceIdAllocator
{
    // Smallest available integer allocator: 1,2,3,…; reuses freed ids.
    static readonly SortedSet<int> free = new();   // keeps smallest at free.Min
    static readonly HashSet<int> inUse = new();
    static int next = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Reset()
    {
        free.Clear();
        inUse.Clear();
        next = 1;
    }

    public static int Acquire()
    {
        int id;
        if (free.Count > 0) { id = free.Min; free.Remove(id); }
        else { id = next++; }
        inUse.Add(id);
        return id;
    }

    public static void Release(int id)
    {
        if (id <= 0) return;
        if (inUse.Remove(id)) free.Add(id);
    }
}
