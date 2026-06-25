using System.Collections.Generic;

public static class GameState
{
    

    private static List<int> _pending = new();

    public static void Set(List<int> ids)
    {
        _pending = new List<int>(ids);
    }

    public static bool HasAny() => _pending.Count > 0;

    public static int Pop()
    {
        if (_pending.Count == 0) return -1;
        int id = _pending[0];
        _pending.RemoveAt(0);
        return id;
    }

    public static void Clear() => _pending.Clear();
}
