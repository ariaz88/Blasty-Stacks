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
    [Tooltip("Time to complete jump.")]
    [SerializeField, Min(0.05f)] private float jumpDuration = 0.45f;

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

        int facingY = GetFacingYSign();
        startPos = transform.position;
        endPos = startPos + new Vector3(0f, jumpDistanceY * facingY, 0f);

        tElapsed = 0f;
        isJumping = true;
        loopPlayed = false;
        tLoopSwapAt = Mathf.Min(jumpStartToLoopDelay, jumpDuration * 0.5f);

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


    // ====== per-frame jump ======
    private void TickJump()
    {
        UpdateTimers(out float t01);

        float yLinear = Mathf.Lerp(startPos.y, endPos.y, t01);
        float arcT = HeightCurve(t01);
        float yWithArc = yLinear + arcT * arcHeight;

        ApplyPositionY(yWithArc);
        UpdatePlayerScale(arcT);
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
        t01 = Mathf.Clamp01(tElapsed / jumpDuration);
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
    private void UpdatePlayerScale(float arcT)
    {
        float arcForScale = Mathf.Pow(arcT, scaleResponse);
        float playerScaleU = Mathf.Lerp(1f, apexScale, arcForScale); // 1->apexScale

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
        transform.localScale = new Vector3(
            baseScaleAbs.x * baseScaleSign.x,
            baseScaleAbs.y * baseScaleSign.y,
            baseScaleAbs.z * baseScaleSign.z
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
