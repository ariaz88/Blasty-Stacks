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


    // --- Shadow ground-follow settings (add near your existing shadow fields) ---
    [Header("Shadow Ground-Follow")]
    [SerializeField] private bool detachShadowDuringJump = true;
    [SerializeField] private AnimationCurve shadowProgressCurve = null;
    // If null, we'll use an ease-in (t^2) at runtime to make it lag then catch up.

    [SerializeField] private Vector3 shadowGroundAxisMask = new Vector3(0, 1, 0);
    // Which axes the shadow should move along. For your setup (forward on Y), keep (0,1,0).
    // If you needed X-forward, you'd use (1,0,0).

    // --- runtime bookkeeping ---
    private Transform shadowOriginalParent;
    private Vector3 shadowStartPos;     // where the shadow begins this jump
    private Vector3 shadowXZHold;       // fixed X/Z (or axes not used by ground axis mask)



    private void Awake()
    { 
        pm = GetComponent<PlayerManager>();

        CacheBaseScales();
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

    private void BeginJump1()
    {
        ConsumeJumpBuffer();

        int facingY = GetFacingYSign();
        startPos = transform.position;
        endPos = startPos + new Vector3(0f, jumpDistanceY * facingY, 0f);

        tElapsed = 0f;
        isJumping = true;
        loopPlayed = false;

        tLoopSwapAt = Mathf.Min(jumpStartToLoopDelay, jumpDuration * 0.5f);

        PlayAnim(jumpStartState);
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
        if (shadow && detachShadowDuringJump)
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
        if (!shadow) return;

        // Choose the progress curve: ease-in to create lag, but still hit 1 at the end.
        float s = shadowProgressCurve != null ? Mathf.Clamp01(shadowProgressCurve.Evaluate(t01))
                                              : t01 * t01; // default: t^2 (lag then catch up)

        // Compute target ground position along the chosen axis, using linear (no arc).
        // Axes masked with 1 move from start->end; axes with 0 stay at cached value.
        Vector3 start = shadowStartPos;
        Vector3 end = start;

        // We only move on the axis specified by shadowGroundAxisMask.
        // Your forward is Y, so we lerp Y from start.y to end.y == yLinear’s end.
        if (shadowGroundAxisMask.x != 0f) end.x = Mathf.Lerp(start.x, endPos.x, s);
        if (shadowGroundAxisMask.y != 0f) end.y = Mathf.Lerp(start.y, endPos.y, s); // ground follows player's forward on Y
        if (shadowGroundAxisMask.z != 0f) end.z = Mathf.Lerp(start.z, endPos.z, s);

        // Lock non-moving axes to their cached values
        if (shadowGroundAxisMask.x == 0f) end.x = shadowXZHold.x;
        if (shadowGroundAxisMask.y == 0f) end.y = shadowXZHold.y;
        if (shadowGroundAxisMask.z == 0f) end.z = shadowXZHold.z;

        shadow.position = end;
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

    private void UpdateShadowScale1(float arcT)
    {
        if (!shadow) return;

        // the player's scale factor this frame
        float playerU = GetCurrentPlayerUniformScale();

        float arcForShadow = Mathf.Pow(arcT, shadowScaleResponse);
        float shadowWorldU = Mathf.Lerp(1f, shadowApexScale, arcForShadow); // target WORLD scale

        // counter-scale locally to achieve target WORLD scale:
        float childLocalU = shadowWorldU / Mathf.Max(playerU, 1e-6f);

        Vector3 shAbs = shadowBaseLocalAbs * childLocalU;
        shadow.localScale = new Vector3(
            shAbs.x * shadowBaseLocalSign.x,
            shAbs.y * shadowBaseLocalSign.y,
            shAbs.z * shadowBaseLocalSign.z
        );
    }
    private void UpdateShadowScale(float arcT)
    {
        if (!shadow) return;

        float playerU = detachShadowDuringJump ? 1f : GetCurrentPlayerUniformScale();

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
    private void Land1()
    {
        SnapToEndY();

        if (resetScaleOnLand)
            RestorePlayerScale();

        if (shadow && resetShadowOnLand)
            RestoreShadowScale();

        isJumping = false;
        loopPlayed = false;
    }
    private void Land()
    {
        SnapToEndY();

        if (resetScaleOnLand) RestorePlayerScale();
        if (shadow)
        {
            if (resetShadowOnLand) RestoreShadowScale();

            // Snap shadow exactly to player's landing forward position along ground axis
            if (detachShadowDuringJump)
            {
                // Build final ground-aligned pos: same end on forward axis, keep held axes.
                Vector3 final = shadow.position;

                if (shadowGroundAxisMask.x != 0f) final.x = endPos.x;
                if (shadowGroundAxisMask.y != 0f) final.y = endPos.y; // forward on Y
                if (shadowGroundAxisMask.z != 0f) final.z = endPos.z;

                if (shadowGroundAxisMask.x == 0f) final.x = shadowXZHold.x;
                if (shadowGroundAxisMask.y == 0f) final.y = shadowXZHold.y;
                if (shadowGroundAxisMask.z == 0f) final.z = shadowXZHold.z;

                shadow.position = final;

                // Re-parent back
                shadow.SetParent(shadowOriginalParent, true);
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
