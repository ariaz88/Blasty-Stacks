/// <summary>
/// ONE switchboard for features that are TEMPORARILY off in this build.
///
/// Nothing here deletes anything. Every system these flags gate is still fully
/// authored - its scripts, its scene objects and its ScriptableObjects are all
/// untouched. Flipping a flag back to true is the ONLY thing needed to bring the
/// feature back; there is no other edit to undo.
///
/// Why a static class rather than a ScriptableObject or an Inspector tick-box:
/// the systems below live in 20+ stage scenes. A serialized field would have to
/// be re-authored in every one of them, and a scene that was missed would
/// silently keep the feature on. A static is the same answer in every scene.
///
/// `static readonly`, NOT `const`, on purpose: a const false makes every guarded
/// branch provably unreachable, and the compiler then fills the Unity console
/// with CS0162 "unreachable code" warnings on perfectly correct code. A readonly
/// static is resolved at run time, so the guards compile clean.
///
/// WHEN THE NEXT VERSION SHIPS: set the flag(s) back to true. Nothing else.
/// </summary>
public static class GameFeatureFlags
{
    /// <summary>
    /// The whole in-battle roguelite layer: XP gain, the level bar, the level-up
    /// pause and the skill-card pick screen.
    ///
    /// OFF = RogueliteManager disables itself in Awake, so it never subscribes to
    /// the battle-start event, never grants XP and never opens a card screen. It
    /// also switches its own UI (the XP bar root, the card panel and the level-up
    /// overlay) off first, because those objects are authored INSIDE the stage
    /// scenes and some of them start active.
    ///
    /// A disabled component is also invisible to FindObjectOfType, which is how
    /// EnemyManager and PlayerWaveManager reach it - so their calls quietly become
    /// no-ops with no edits of their own.
    /// </summary>
    public static readonly bool RogueliteEnabled = false;

    /// <summary>
    /// The "your army is collapsing - buy one more hero?" prompt (LastStandOffer).
    ///
    /// OFF = the component disables itself in Awake and never subscribes, so the
    /// offer cell is never even instantiated.
    /// </summary>
    public static readonly bool LastStandOfferEnabled = false;

    /// <summary>
    /// The gem buy-back on the battle HUD's Heroes Stats panel.
    ///
    /// This gates the PURCHASE only, not the panel. The panel itself stays fully
    /// live: one card per hero type, the "alive/total" count, and the grey
    /// "Cell DeActive" frame the moment a type is wiped out.
    ///
    /// OFF = a wiped-out card keeps showing its count ("0/3") instead of swapping
    /// in the gem price, the "Cost  Gem" object never appears, and its button is
    /// never wired. So the panel reads as a pure scoreboard and no hero can be
    /// added to the battle for gems.
    /// </summary>
    public static readonly bool HeroBuyBackEnabled = false;
}
