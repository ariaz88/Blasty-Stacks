using UnityEngine;

/// <summary>
/// The three baked card sprites HeroCardRevealDirector draws with (back, face, glow).
///
/// WHY THIS EXISTS: the director is self-bootstrapping - nothing in a scene holds a
/// reference to it, so nothing could hand it its sprites. It used to fetch them with
/// UnityEditor.AssetDatabase, which only exists in the Editor; every BUILD fell back
/// to a blank white texture, so the cards rendered as white rectangles and the teal
/// glow as a solid cyan block. This asset lives at
/// Assets/Resources/VFX/HeroCardSprites.asset, so Resources.Load finds it in a build
/// and its references pull the PNGs (which stay in Assets/Arts/VFX) into the build.
/// </summary>
[CreateAssetMenu(fileName = "HeroCardSprites", menuName = "Blasty/VFX/Hero Card Sprite Set")]
public class HeroCardSpriteSet : ScriptableObject
{
    /// <summary>Path under any Resources folder, without extension.</summary>
    public const string ResourcePath = "VFX/HeroCardSprites";

    [Tooltip("Face-down card - the \"?\" back.")]
    public Sprite back;

    [Tooltip("Face-up card frame the hero portrait sits on.")]
    public Sprite face;

    [Tooltip("Glow halo baked with padding around the card footprint.")]
    public Sprite glow;

    private static HeroCardSpriteSet cached;

    /// <summary>The project's sprite set, or null if the asset is missing.</summary>
    public static HeroCardSpriteSet Load()
    {
        if (!cached) cached = Resources.Load<HeroCardSpriteSet>(ResourcePath);
        return cached;
    }
}
