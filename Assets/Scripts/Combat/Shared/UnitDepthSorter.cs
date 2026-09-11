// UnitDepthSorter.cs
using UnityEngine;

/// <summary>
/// Makes the unit standing LOWER on the screen draw in FRONT of the one behind it.
///
/// THE PROBLEM THIS SOLVES
/// -----------------------
/// Every body part of every character in this project is a SpriteRenderer on
/// sorting layer Default with sorting order 1 - heroes and enemies alike. Within
/// one character the parts are separated by tiny Z offsets (the Valkyrie's span
/// about 0.011 local, ~0.002 after the 0.19 visual scale) and the camera is
/// orthographic with Transparency Sort Mode = Default, so Unity settles those ties
/// along the view axis. That works beautifully INSIDE a character and not at all
/// BETWEEN two of them: two units with equal order and equal Z sort arbitrarily,
/// which is why an enemy's head could be painted over a hero that is standing in
/// front of it.
///
/// THE FIX
/// -------
/// Give the whole unit a Z derived from its Y, so the existing Z-based tie-break
/// suddenly has a meaningful answer. Nothing else has to change: the parts keep
/// their relative order (the root moves them all together), and anything that
/// relies on an explicit sorting ORDER - the HP bar canvases at order 500, the
/// castle's foreground pieces at orders 4 and 5 - is untouched, because sorting
/// order is compared before Z ever is.
///
/// WHY Z AND NOT A SortingGroup
/// ----------------------------
/// A SortingGroup on the unit root would swallow the world-space HP-bar Canvas
/// parented under it and drag it down into the body's sorting layer, so bars
/// would stop being reliably on top. Z leaves every sorting layer exactly as the
/// artists authored it.
///
/// Added automatically by PlayerManager / EnemyManager - no prefab edit needed.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class UnitDepthSorter : MonoBehaviour
{
    /// <summary>
    /// World Z per world Y. Positive: a unit further UP the screen gets a larger Z,
    /// which is further from the camera, which means it is drawn BEHIND.
    ///
    /// Sized against the ~0.002 Z spread inside one character: at 0.25, units only
    /// 1 cm apart in Y already separate cleanly, while the whole battlefield stays
    /// inside a Z range of roughly +/-1.5 - nowhere near the camera's near plane.
    /// </summary>
    public const float DepthPerY = 0.25f;

    /// <summary>
    /// Runs in LateUpdate, after physics and after every mover has had its say, so
    /// nothing can overwrite the depth in the same frame it is rendered.
    /// Rigidbody2D never touches Z, so this cannot fight the physics engine.
    /// </summary>
    private void LateUpdate()
    {
        Vector3 p = transform.position;
        float z = p.y * DepthPerY;

        if (Mathf.Approximately(p.z, z)) return;

        p.z = z;
        transform.position = p;
    }
}
