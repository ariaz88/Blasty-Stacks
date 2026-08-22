using UnityEngine;


public class FrogJumpTransformOnly : MonoBehaviour
{

    [Header("Refs")]
    [SerializeField] private Animator animator;             // optional
    [SerializeField] private SpriteRenderer spriteRenderer; // optional
    [SerializeField] private Transform shadow;              // assign your Shadow child
    PlayerManager pm;

    [Header("Animator States")]
    [SerializeField] private string jumpStartState = "JumpStart";
    [SerializeField] private string jumpLoopState = "JumpLoop";

    [Header("Input")]
    [SerializeField] private KeyCode jumpKey = KeyCode.LeftShift;

    [Header("Jump Shape (transform-only)")]
    [Tooltip("Forward distance along Y traveled per jump (+up / -down).")]
    [SerializeField] private float jumpDistanceY = 3.0f;
    [Tooltip("Visual arc height (does not affect landing Y).")]
    [SerializeField] private float arcHeight = 1.5f;
    [Tooltip("Time to complete jump, for a jump of Reference Distance.")]
    [SerializeField, Min(0.05f)] private float jumpDuration = 0.45f;

    [Header("Distance Scaling")]
    [Tooltip("ON = long jumps take longer and arc higher, short jumps are quick and " +
             "flat, so every jump moves at a believable speed.\n" +
             "OFF = every jump takes exactly Jump Duration, which makes a long jump " +
             "look like the unit was fired from a cannon.")]
    [SerializeField] private bool scaleWithDistance = true;

    [Tooltip("The distance that plays at exactly Jump Duration and Arc Height. " +
             "Jumps longer than this take more time, shorter ones less.")]
    [SerializeField, Min(0.1f)] private float referenceDistance = 3f;

    [Tooltip("Hard limits on the scaled duration (x = min, y = max seconds), so a " +
             "tiny hop is not instant and a huge leap does not float forever.")]
    [SerializeField] private Vector2 durationClamp = new Vector2(0.28f, 0.85f);

    // Per-jump values, resolved in BeginJump. The serialized fields above are the
    // BASELINE; these are what the jump actually plays at.
    private float activeDuration;
    private float activeArcHeight;

    [Header("Timing")]
    [SerializeField] private float jumpStartToLoopDelay = 0.12f;

    [Header("Facing on Y")]
    [SerializeField] private bool useFlipYFacing = false;

    [Header("QoL (buffer)")]
    [SerializeField] private float jumpBuffer = 0.12f;

    [Header("Air Scale Effect (Player)")]
    [Tooltip("Scale at apex (e.g., 1.2 = 20% bigger).")]
    [SerializeField, Range(1f, 1.5f)] private float apexScale = 1.2f;
    [SerializeField, Min(0.1f)] private float scaleResponse = 1.0f;
    [SerializeField] private bool resetScaleOnLand = true;

    [Tooltip("Scale the unit SETTLES AT once it lands in the field, as a fraction " +
             "of the scale it had on the stage. 0.9 = 90%. 1 = the old behaviour " +
             "(lands back at its original size).")]
    [SerializeField, Range(0.1f, 1f)] private float landedScaleMultiplier = 0.9f;

    [Header("Air Scale Effect (Shadow)")]
    [Tooltip("Shadow world scale at apex (e.g., 0.75 = 25% smaller).")]
    [SerializeField, Range(0.5f, 1f)] private float shadowApexScale = 0.75f;
    [SerializeField, Min(0.1f)] private float shadowScaleResponse = 1.0f;
    [SerializeField] private bool resetShadowOnLand = true;

    // ====== runtime state ======
    
    private bool isJumping;
    public bool IsJumping => isJumping;

    private bool loopPlayed;
    private float lastJumpPressedTime = -999f;
    private float tElapsed;
    private float tLoopSwapAt;
    private Vector3 startPos, endPos;

    // scale bookkeeping (player)
    private Vector3 baseScaleAbs;
    private Vector3 baseScaleSign;

    // shadow bookkeeping
    private Vector3 shadowBaseLocalAbs;
    private Vector3 shadowBaseLocalSign;

    // ====== lifecycle ======


    // --- Shadow behaviour during a jump ---
    public enum ShadowJumpMode
    {
        /// <summary>Shadow stays parented and simply rides along with the unit,
        /// arc included. Current design choice: the shadow is always glued to
        /// the character.</summary>
        StickToCharacter = 0,

        /// <summary>Shadow detaches and travels along the GROUND (the arc-free
        /// path), so it reads as a real cast shadow. Kept for when we want that
        /// look back.</summary>
        GroundProjected = 1,
    }

    [Header("Shadow Behaviour")]
    [Tooltip("Stick To Character = the shadow never leaves the unit (current design).\n" +
             "Ground Projected = the shadow detaches and slides along the ground while the " +
             "unit arcs over it.")]
    [SerializeField] private ShadowJumpMode shadowMode = ShadowJumpMode.StickToCharacter;

    [Header("Shadow Ground-Follow (Ground Projected mode only)")]
    [Tooltip("How far along the ground the shadow has travelled, as a function of jump " +
             "progress. MUST start at 0 and end at 1 - a curve that starts high teleports " +
             "the shadow to the landing spot on frame one. Leave empty for the default t^2 " +
             "(lag, then catch up).")]
    [SerializeField] private AnimationCurve shadowProgressCurve = null;

    [SerializeField] private Vector3 shadowGroundAxisMask = new Vector3(0, 1, 0);
    // Which axes the shadow should move along. For your setup (forward on Y), keep (0,1,0).
    // If you needed X-forward, you'd use (1,0,0).

    // --- runtime bookkeeping ---
    private Transform shadowOriginalParent;
    private Vector3 shadowStartPos;     // where the shadow begins this jump
    private Vector3 shadowXZHold;       // fixed X/Z (or axes not used by ground axis mask)

    private bool DetachShadow => shadowMode == ShadowJumpMode.GroundProjected;



    private void Awake()
    { 
        pm = GetComponent<PlayerManager>();

        CacheBaseScales();
    }

    // If the unit dies or is pooled mid-jump while its shadow is detached, the
    // shadow is a ROOT object and would be left behind in the scene forever.
    private void OnDisable() => ReattachShadowIfDetached();

    private void ReattachShadowIfDetached()
    {
        if (!shadow || !shadowOriginalParent) return;

        shadow.SetParent(shadowOriginalParent, true);
        shadowOriginalParent = null;
        isJumping = false;
    }

    private void Update()
    {
        HandleInput();
        TryBeginJump();
        if (isJumping) TickJump();
    }

    // ====== input & start ======
    private void HandleInput()
    {
        if (Input.GetKeyDown(jumpKey))
            lastJumpPressedTime = Time.time;
    }

    private void TryBeginJump()
    {
        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (!isJumping && buffered)
            BeginJump();
    }


    // When set, the next BeginJump lands on this exact world Y instead of
    // travelling the fixed jumpDistanceY. Consumed by BeginJump.
    private bool hasTargetYOverride;
    private float targetWorldY;

    public bool TriggerJump()
    {
        if (isJumping) return false;

        // Preserve the CURRENT facing/sign before scaling starts
        Vector3 s = transform.localScale;
        baseScaleSign = new Vector3(
            (s.x >= 0f) ? 1f : -1f,
            (s.y >= 0f) ? 1f : -1f,
            (s.z >= 0f) ? 1f : -1f
        );

        BeginJump();  // uses your existing flow (animation, timers, endPos, etc.)
        return true;
    }

    /// <summary>
    /// Jump so the unit LANDS on <paramref name="worldY"/>, rather than moving a
    /// fixed distance. This is how successive waves land on their own lane and
    /// stop piling up on each other.
    /// </summary>
    public bool TriggerJumpTo(float worldY)
    {
        if (isJumping) return false;

        hasTargetYOverride = true;
        targetWorldY = worldY;

        return TriggerJump();
    }

    private void BeginJump()
    {
        ConsumeJumpBuffer();

        // Re-read the scale we are STARTING from, so "90% of its scale on the
        // stage" is measured against the real on-stage size rather than whatever
        // was cached back in Awake.
        CacheBaseScales();

        int facingY = GetFacingYSign();
        startPos = transform.position;

        if (hasTargetYOverride)
        {
            // Land exactly on the requested lane, whatever the distance.
            endPos = new Vector3(startPos.x, targetWorldY, startPos.z);
            hasTargetYOverride = false;
        }
        else
        {
            endPos = startPos + new Vector3(0f, jumpDistanceY * facingY, 0f);
        }

        ResolveJumpShapeForDistance();

        tElapsed = 0f;
        isJumping = true;
        loopPlayed = false;
        tLoopSwapAt = Mathf.Min(jumpStartToLoopDelay, activeDuration * 0.5f);

        // --- Shadow detach + cache ---
        // Only in GroundProjected mode. In StickToCharacter the shadow stays a
        // child and needs no bookkeeping at all - it just rides along.
        if (shadow && DetachShadow)
        {
            shadowOriginalParent = shadow.parent;
            shadow.SetParent(null, true); // keep world position/rotation/scale

            shadowStartPos = shadow.position;

            // Keep non-forward axes fixed (e.g., X/Z). We cache them once.
            // Axes with mask component == 0 stay fixed.
            shadowXZHold = new Vector3(
                shadowGroundAxisMask.x == 0 ? shadowStartPos.x : 0f,
                shadowGroundAxisMask.y == 0 ? shadowStartPos.y : 0f,
                shadowGroundAxisMask.z == 0 ? shadowStartPos.z : 0f
            );
        }

        PlayAnim(jumpStartState);
    }


    /// <summary>
    /// Works out how long this particular jump should take and how high it should
    /// arc, from the distance it actually covers.
    ///
    /// Duration uses a SQUARE ROOT of the distance ratio, not a straight one.
    /// That is how a real projectile behaves: for a fixed launch angle the range
    /// grows with the square of the launch speed while the flight time grows only
    /// linearly with it, so time scales with sqrt(range). Straight-line scaling
    /// makes long jumps crawl and short hops look snapped.
    ///
    /// Arc height scales LINEARLY, because a projectile's peak height really is
    /// proportional to its range - so a long leap visibly goes higher.
    /// </summary>
    private void ResolveJumpShapeForDistance()
    {
        if (!scaleWithDistance)
        {
            activeDuration = jumpDuration;
            activeArcHeight = arcHeight;
            return;
        }

        float distance = Mathf.Abs(endPos.y - startPos.y);
        float ratio = distance / Mathf.Max(0.0001f, referenceDistance);

        activeDuration = Mathf.Clamp(jumpDuration * Mathf.Sqrt(ratio),
                                     Mathf.Min(durationClamp.x, durationClamp.y),
                                     Mathf.Max(durationClamp.x, durationClamp.y));

        activeArcHeight = arcHeight * ratio;
    }

    // ====== per-frame jump ======
    private void TickJump()
    {
        UpdateTimers(out float t01);

        float yLinear = Mathf.Lerp(startPos.y, endPos.y, t01);
        float arcT = HeightCurve(t01);
        float yWithArc = yLinear + arcT * activeArcHeight;

        ApplyPositionY(yWithArc);
        UpdatePlayerScale(arcT, t01);
        UpdateShadowScale(arcT);

        UpdateShadowGroundFollow(t01, yLinear);


        MaybeSwapToLoop(t01);

        if (t01 >= 1f)
            Land();
    }

    private void UpdateShadowGroundFollow(float t01, float yLinear)
    {
        // StickToCharacter never touches the shadow's position - it is still a
        // child, so it follows the unit for free. Writing a world position here
        // while the shadow is parented is what made it flash to the landing spot
        // and then snap back onto the character.
        if (!shadow || !DetachShadow) return;

        float s = ShadowProgress01(t01);

        // Travel the SAME delta the unit travels, so the shadow keeps the local
        // offset it was authored with (under the feet) instead of collapsing onto
        // the unit's origin. Lerping straight to endPos threw that offset away and
        // - because Land() re-parents with worldPositionStays - the loss was
        // permanent after the first jump.
        Vector3 delta = endPos - startPos;
        Vector3 pos = shadowStartPos;

        if (shadowGroundAxisMask.x != 0f) pos.x = shadowStartPos.x + delta.x * s;
        if (shadowGroundAxisMask.y != 0f) pos.y = shadowStartPos.y + delta.y * s;
        if (shadowGroundAxisMask.z != 0f) pos.z = shadowStartPos.z + delta.z * s;

        // Lock non-moving axes to their cached values
        if (shadowGroundAxisMask.x == 0f) pos.x = shadowXZHold.x;
        if (shadowGroundAxisMask.y == 0f) pos.y = shadowXZHold.y;
        if (shadowGroundAxisMask.z == 0f) pos.z = shadowXZHold.z;

        shadow.position = pos;
    }

    /// <summary>
    /// Ground progress for the shadow, 0..1.
    ///
    /// A serialized AnimationCurve field is NEVER null in Unity - it deserializes
    /// as an empty curve - so the old `curve != null` test always took the curve
    /// branch, however broken the authored curve was. We validate the shape
    /// instead: it needs at least two keys and must actually start at 0, because
    /// a curve whose first key sits at ~1 (which is what the character prefabs
    /// were carrying) puts the shadow on the landing spot on the very first frame.
    /// </summary>
    private float ShadowProgress01(float t01)
    {
        if (shadowProgressCurve != null && shadowProgressCurve.length >= 2)
        {
            float first = shadowProgressCurve.Evaluate(0f);
            if (first <= 0.05f)
                return Mathf.Clamp01(shadowProgressCurve.Evaluate(t01));
        }

        return t01 * t01; // default: lag, then catch up
    }

    private void UpdateTimers(out float t01)
    {
        tElapsed += Time.deltaTime;
        t01 = Mathf.Clamp01(tElapsed / activeDuration);
    }

    private void MaybeSwapToLoop(float t01)
    {
        if (!loopPlayed && tElapsed >= tLoopSwapAt && t01 < 1f)
        {
            PlayAnim(jumpLoopState);
            loopPlayed = true;
        }
    }

    // ====== effects ======
    private void UpdatePlayerScale(float arcT, float t01)
    {
        float arcForScale = Mathf.Pow(arcT, scaleResponse);

        // The "ground" size eases from the on-stage scale down to the landed
        // scale across the whole jump, and the arc bump rides on top of it.
        // Doing it this way means the unit is ALREADY at landedScaleMultiplier
        // when it touches down, so there is no pop on landing - at t01 = 1 the
        // arc term is 0 and this lands exactly on baseScaleAbs * multiplier.
        float groundU = Mathf.Lerp(1f, landedScaleMultiplier, t01);
        float playerScaleU = groundU * Mathf.Lerp(1f, apexScale, arcForScale);

        Vector3 scaledAbs = baseScaleAbs * playerScaleU;
        transform.localScale = new Vector3(
            scaledAbs.x * baseScaleSign.x,
            scaledAbs.y * baseScaleSign.y,
            scaledAbs.z * baseScaleSign.z
        );
    }

    private void UpdateShadowScale(float arcT)
    {
        if (!shadow) return;

        // Detached: the shadow is a root object, so its local scale IS its world
        // scale. Parented: counter-scale so the unit's arc bump does not inflate
        // the shadow with it.
        float playerU = DetachShadow ? 1f : GetCurrentPlayerUniformScale();

        float arcForShadow = Mathf.Pow(arcT, shadowScaleResponse);
        float shadowWorldU = Mathf.Lerp(1f, shadowApexScale, arcForShadow); // 1 -> smaller at apex
        float childLocalU = shadowWorldU / Mathf.Max(playerU, 1e-6f);

        Vector3 shAbs = shadowBaseLocalAbs * childLocalU;
        shadow.localScale = new Vector3(
            shAbs.x * shadowBaseLocalSign.x,
            shAbs.y * shadowBaseLocalSign.y,
            shAbs.z * shadowBaseLocalSign.z
        );
    }


    // ====== finish ======
    private void Land()
    {
        SnapToEndY();

        if (resetScaleOnLand) RestorePlayerScale();
        if (shadow)
        {
            if (resetShadowOnLand) RestoreShadowScale();

            // Snap shadow exactly to player's landing forward position along ground axis
            if (DetachShadow)
            {
                // Finish the same delta the unit travelled, so the authored local
                // offset survives the round trip (see UpdateShadowGroundFollow).
                Vector3 delta = endPos - startPos;
                Vector3 final = shadow.position;

                if (shadowGroundAxisMask.x != 0f) final.x = shadowStartPos.x + delta.x;
                if (shadowGroundAxisMask.y != 0f) final.y = shadowStartPos.y + delta.y; // forward on Y
                if (shadowGroundAxisMask.z != 0f) final.z = shadowStartPos.z + delta.z;

                if (shadowGroundAxisMask.x == 0f) final.x = shadowXZHold.x;
                if (shadowGroundAxisMask.y == 0f) final.y = shadowXZHold.y;
                if (shadowGroundAxisMask.z == 0f) final.z = shadowXZHold.z;

                shadow.position = final;

                // Re-parent back
                shadow.SetParent(shadowOriginalParent, true);
                shadowOriginalParent = null;
            }
        }

        isJumping = false;
        loopPlayed = false;
    }


    // ====== helpers ======
    private void CacheBaseScales()
    {
        var s = transform.localScale;
        baseScaleAbs = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        baseScaleSign = new Vector3(Sgn(s.x), Sgn(s.y), Sgn(s.z));

        if (shadow)
        {
            var sh = shadow.localScale;
            shadowBaseLocalAbs = new Vector3(Mathf.Abs(sh.x), Mathf.Abs(sh.y), Mathf.Abs(sh.z));
            shadowBaseLocalSign = new Vector3(Sgn(sh.x), Sgn(sh.y), Sgn(sh.z));
        }
    }

    private void RestorePlayerScale()
    {
        // NOT back to the original size any more: the unit settles at
        // landedScaleMultiplier of the scale it had on the stage. The sign is
        // preserved so this never disturbs which way the unit is facing.
        transform.localScale = new Vector3(
            baseScaleAbs.x * landedScaleMultiplier * baseScaleSign.x,
            baseScaleAbs.y * landedScaleMultiplier * baseScaleSign.y,
            baseScaleAbs.z * landedScaleMultiplier * baseScaleSign.z
        );
    }

    private void RestoreShadowScale()
    {
        shadow.localScale = new Vector3(
            shadowBaseLocalAbs.x * shadowBaseLocalSign.x,
            shadowBaseLocalAbs.y * shadowBaseLocalSign.y,
            shadowBaseLocalAbs.z * shadowBaseLocalSign.z
        );
    }

    private void ApplyPositionY(float y)
    {
        var p = transform.position;
        p.y = y;
        transform.position = p;
    }

    private void SnapToEndY()
    {
        var p = transform.position;
        p.y = endPos.y;
        transform.position = p;
    }

    private void ConsumeJumpBuffer() => lastJumpPressedTime = -999f;
    private float HeightCurve(float t) => 4f * t * (1f - t); // bell: 0→1→0

    private int GetFacingYSign()
    {
        if (useFlipYFacing && spriteRenderer != null)
            return spriteRenderer.flipY ? -1 : 1;

        float sy = transform.localScale.y;
        if (Mathf.Approximately(sy, 0f)) return 1;
        return sy > 0f ? 1 : -1;
    }

    private void PlayAnim(string state)
    {
        if (pm.playerAnimatitorManager /*&& !string.IsNullOrEmpty(state)*/)
            pm.playerAnimatitorManager.PlayTargetAnimation(state, true);

    }

    private float GetCurrentPlayerUniformScale()
    {
        // assumes uniform scaling during jump (we apply same factor to x,y,z)
        // infer by comparing current abs scale to base abs
        Vector3 absNow = new Vector3(Mathf.Abs(transform.localScale.x),
                                     Mathf.Abs(transform.localScale.y),
                                     Mathf.Abs(transform.localScale.z));
        // pick Y as reference (all equal anyway)
        return Mathf.Approximately(baseScaleAbs.y, 0f) ? 1f : absNow.y / baseScaleAbs.y;
    }

    private float Sgn(float v) => (v >= 0f) ? 1f : -1f;
}
