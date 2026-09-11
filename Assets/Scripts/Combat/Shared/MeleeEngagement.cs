// MeleeEngagement.cs
using UnityEngine;

/// <summary>
/// WHERE a melee unit has to stand to be able to hit its target, and how to tell
/// whether it is standing there yet. One rule, shared by heroes and enemies, so
/// the two factions cannot end up with different ideas of "close enough".
///
/// WHY THIS EXISTS
/// ---------------
/// Every melee weapon in this project is a WIDE, FLAT trigger bolted to a hand or
/// weapon bone: Player_Valkyrie's sword box is 7.68 x 1.29 local units offset
/// +4.11 on X, which at the visual's 0.19 scale is roughly 1.46 wide and only
/// 0.24 TALL, reaching sideways from the character. A weapon shaped like that can
/// only touch something standing BESIDE its owner. Two units stacked vertically -
/// one directly above the other, which is the natural result of enemies marching
/// straight down a lane into heroes walking straight up it - are inside each
/// other's attack RANGE while being completely outside each other's attack BOX.
/// They then stand there swinging and neither one loses a single HP.
///
/// Before this, both sides decided "I have arrived" from a plain radial distance
/// (PlayerPursueTargetState compared to maxAttackRange, EnemyLocoMotion compared
/// to stoppingDistance). Radial distance cannot tell "beside" from "on top of",
/// so a unit approaching from directly below stopped directly below - in range,
/// unable to hit, and visually sunk into its target's sprite.
///
/// HOW THE SIDE CHOICE STAYS STABLE
/// --------------------------------
/// Both units run this same code against each other, so the choice has to agree
/// or they chase each other sideways forever. The rule is "stand on the side of
/// the target that I am ALREADY on": since A is on the opposite side of B from
/// where B is of A, both derive the SAME final arrangement and converge on it.
/// The single ambiguous case is a dead-level approach (dx ~ 0), and that is what
/// <see cref="PlayerPreferredSide"/> / <see cref="EnemyPreferredSide"/> break -
/// they are deliberately OPPOSITE. Giving both factions the same preference is
/// the one thing that must never be done: both would then want to be on the
/// target's right, and the pair would slide across the map together.
/// </summary>
public static class MeleeEngagement
{
    /// <summary>
    /// Fraction of attack range to stand at. Must stay below 1: the stand point
    /// has to be comfortably INSIDE range, or the mover reports "arrived" while
    /// the state machine still reports "too far" and the unit freezes on the spot.
    /// </summary>
    public const float StandoffFactor = 0.85f;

    /// <summary>Floor for the standoff, for any unit authored with a tiny range.</summary>
    public const float MinStandoff = 0.2f;

    /// <summary>
    /// How far off the target's own height a unit may stand and still connect,
    /// as a fraction of range. Derived from the weapon boxes: a hero swinging at
    /// 0.72 to the side still overlaps an enemy body capsule with a 0.25 height
    /// difference, but not with a 0.8 one.
    /// </summary>
    public const float BandFactor = 0.3f;

    /// <summary>Floor for the vertical band, in world units.</summary>
    public const float MinBand = 0.1f;

    /// <summary>
    /// How far off-centre in X a unit must be before it reads its own position as
    /// "I am on this side". Inside this dead zone the faction preference decides,
    /// which is what stops two dead-level units from picking the same side.
    /// </summary>
    public const float SideDeadZoneX = 0.25f;

    /// <summary>
    /// A unit closer than standoff * this has sunk INTO its target and is sent
    /// back out to its stand point. Below 1 by a clear margin so that arriving
    /// normally (which lands exactly on the standoff) never trips it.
    /// </summary>
    public const float ArrivalSlack = 0.8f;


    /// <summary>Heroes settle on their target's LEFT when the approach is dead level.</summary>
    public const float PlayerPreferredSide = -1f;

    /// <summary>Enemies settle on their target's RIGHT - the opposite of the heroes, on purpose.</summary>
    public const float EnemyPreferredSide = 1f;

    /// <summary>Distance from the target this unit should come to rest at.</summary>
    public static float Standoff(float range) => Mathf.Max(MinStandoff, range * StandoffFactor);

    /// <summary>Half-height of the strip around the target's own Y where a hit can land.</summary>
    public static float Band(float range) => Mathf.Max(MinBand, range * BandFactor);

    /// <summary>
    /// Which side of the target to approach: +1 = target's right, -1 = its left.
    ///
    /// <paramref name="heldSide"/> is the side this unit chose last time (0 if it
    /// has not chosen yet). It is kept while the unit is inside the dead zone, so
    /// a unit that is already walking around its target does not reverse halfway
    /// just because it crossed the target's centre line.
    /// </summary>
    public static float ChooseSide(Vector2 self, Vector2 target, float preferredSide, float heldSide)
    {
        float dx = self.x - target.x;

        if (Mathf.Abs(dx) >= SideDeadZoneX)
            return dx < 0f ? -1f : 1f;

        if (heldSide != 0f) return heldSide;

        return preferredSide < 0f ? -1f : 1f;
    }

    /// <summary>
    /// The exact point to walk to: beside the target, at standoff distance, inside
    /// the vertical band.
    ///
    /// <paramref name="slot"/> spreads multiple attackers on one target without any
    /// of them pushing each other (see AttackSlotRegistry for why pushing is
    /// banned here). It fans them UP AND DOWN the same side, never across to the
    /// other one.
    ///
    /// THE SLOT MUST NOT FLIP THE SIDE - this was tried and it broke badly.
    /// The side rule above is only self-consistent because both halves of a fighting
    /// pair derive it from the same geometry. An attacker whose slot sent it to the
    /// far side wanted to stand on its target's right while that target, engaging it
    /// back, also wanted to stand on ITS target's right - two demands with no
    /// solution, so the pair walked sideways together for as long as the fight
    /// lasted. A simulated 2-heroes-vs-1-enemy fight drifted 9 world units off the
    /// lane in 12 seconds and never landed a hit. Attackers who genuinely approach
    /// from opposite sides get opposite sides for free, from their own positions.
    ///
    /// The point always stays exactly <see cref="Standoff"/> from the target, so no
    /// slot can ever fall outside attack range - that invariant is what keeps the
    /// mover and the state machine from disagreeing and freezing a unit in place.
    /// </summary>
    public static Vector2 StandPoint(Vector2 target, float range, float side, int slot)
    {
        float radius = Standoff(range);
        float band = Band(range);

        if (slot < 0) slot = 0;

        // 0 = level with the target, then alternately above and below it, halving
        // the step each time round so later slots interleave instead of colliding.
        float dy = 0f;
        if (slot > 0)
        {
            int rank = (slot + 1) / 2;                       // 1,1,2,2,3,3...
            float magnitude = band / Mathf.Pow(2f, rank - 1);
            dy = (slot % 2 == 1) ? magnitude : -magnitude;
        }

        // Keep the point ON the standoff circle, so |offset| is always the radius.
        float dx = Mathf.Sqrt(Mathf.Max(0.01f, radius * radius - dy * dy));

        return target + new Vector2((side < 0f ? -1f : 1f) * dx, dy);
    }

    /// <summary>
    /// TRUE when this unit is standing somewhere its weapon can actually reach the
    /// target: inside range, not sunk into the target, and level enough with it.
    ///
    /// All three conditions are satisfied with margin by the point
    /// <see cref="StandPoint"/> returns, which is what guarantees that "walk to the
    /// stand point" and "am I in position" can never disagree forever.
    /// </summary>
    public static bool InAttackPosition(Vector2 self, Vector2 target, float range)
    {
        Vector2 d = target - self;

        float distance = d.magnitude;
        if (distance > range) return false;
        if (distance < Standoff(range) * ArrivalSlack) return false;

        return Mathf.Abs(d.y) <= Band(range);
    }
}
