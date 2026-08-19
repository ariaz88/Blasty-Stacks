using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Units/Units Database", fileName = "UnitsDatabaseSO")]
public class UnitsDatabaseSO : ScriptableObject
{
    //UnitsDatabaseSO is a single/One time , shared asset that defines the roster and card order for your whole game.


    [Tooltip("Order here = order of cards in the Units UI (left → right).")]
    [SerializeField] private List<UnitDefinitionSO> units = new();

    public IReadOnlyList<UnitDefinitionSO> Units => units;

    public UnitDefinitionSO GetById(int unitId)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u != null && u.unitId == unitId)
                return u;
        }
        return null;
    }

    public int IndexOf(int unitId)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u != null && u.unitId == unitId)
                return i;
        }
        return -1;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Warn about nulls or duplicate IDs in editor
        var seen = new HashSet<int>();
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;

            if (!seen.Add(u.unitId))
                Debug.LogWarning($"[UnitsDatabaseSO] Duplicate unitId {u.unitId} at index {i}: {u.name}", this);
        }
    }
#endif
}
