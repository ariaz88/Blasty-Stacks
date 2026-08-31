using UnityEngine;

/// <summary>
/// The player's weapon hitbox.
///
/// The attack window is opened by the EnableDamageCollier animation event and closed by
/// DisableDamageCollider. That pair is the ONLY thing that should let a sword hurt anybody -
/// walking into an enemy must not.
///
/// THE BUG THIS FIXES: if an attack animation is cut short before it reaches its
/// DisableDamageCollider frame - the target dies, the unit retargets, the animator
/// transitions out - the collider stays enabled forever. The unit then walks around with a
/// live sword and damages whatever it brushes past. Caught live in play mode 2026-08-31: a
/// CowMinotaur sitting on the 'Idle' clip with its damage collider still enabled.
///
/// !! TWO EARLIER ATTEMPTS AT THIS GUARD BOTH BROKE COMBAT ENTIRELY. Do not repeat them:
///
///   1. `currentState == playerManager.AttackState` - measured: while genuinely swinging,
///      currentState reads PlayerCombatState, NEVER PlayerAttackState. Rejected 100% of hits.
///   2. "is an attack clip playing", via GetCurrentAnimatorClipInfo - measured: at the moment
///      EnableDamageCollier fires, the animator already reports 'Idle'. So this closed the
///      collider in the same frame it opened and the trigger never fired even once.
///
/// What IS reliable is the enable event itself. So the window is closed by a TIMEOUT rather
/// than by interrogating animator state: the weapon may stay live for at most
/// <see cref="MaxAttackWindow"/> seconds after it was opened. A real swing is far shorter, so
/// legitimate damage is untouched; a missed Disable event can now leak for that long at worst
/// instead of forever.
/// </summary>
public class PlayerDamageCollider : MonoBehaviour
{
    /// <summary>
    /// Longest a swing's damage window may stay open. Must comfortably exceed a real attack's
    /// active frames, or legitimate hits get cut; and stay small, since it bounds how long a
    /// stuck weapon can leak. Watch the console for "watchdog closed" lines to tune it.
    /// </summary>
    private const float MaxAttackWindow = 0.35f;

    private PlayerManager playerManager;
    private Animator animator;

    private bool wasEnabled;
    private float openedAt = -1f;

    private void Awake()
    {
        playerManager = GetComponentInParent<PlayerManager>();
        if (playerManager != null)
            animator = playerManager.GetComponentInChildren<Animator>(true);
    }

    /// <summary>
    /// True only on the copy living on the weapon collider itself.
    ///
    /// This component sits on BOTH the player root and the weapon child, and Unity delivers a
    /// trigger message to the collider's GameObject AND to the GameObject holding the attached
    /// Rigidbody2D - so without this both copies fired and every hit was applied TWICE.
    /// Confirmed live: duplicate diagnostic lines at identical timestamps.
    /// </summary>
    private bool IsWeaponInstance =>
        playerManager != null
        && playerManager.playerDamageCollider != null
        && playerManager.playerDamageCollider.gameObject == gameObject;

    private void LateUpdate()
    {
        if (!IsWeaponInstance) return;

        var col = playerManager.playerDamageCollider;
        if (col == null) return;

        // Opened this frame by the animation event - start the clock.
        if (col.enabled && !wasEnabled)
        {
            openedAt = Time.time;

            if (CombatDiagnostics.Enabled && CombatDiagnostics.LogColliderWindow)
                Debug.Log($"[CD] {Time.time:F2}s  weapon OPENED on '{playerManager.name}'" +
                          $"  clip='{CurrentClipName()}'");
        }

        // Still open past the allowed window - the Disable event never arrived.
        if (col.enabled && openedAt >= 0f && Time.time - openedAt > MaxAttackWindow)
        {
            col.enabled = false;

            if (CombatDiagnostics.Enabled)
                Debug.Log($"[CD] {Time.time:F2}s  >>> WATCHDOG CLOSED a stuck weapon on " +
                          $"'{playerManager.name}' after {MaxAttackWindow:F2}s  " +
                          $"clip='{CurrentClipName()}'");
        }

        wasEnabled = col.enabled;
    }

    private string CurrentClipName()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return "<no animator>";
        var infos = animator.GetCurrentAnimatorClipInfo(0);
        if (infos == null || infos.Length == 0 || infos[0].clip == null) return "<none>";
        return infos[0].clip.name;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Silences the duplicate delivery to the root copy - see IsWeaponInstance.
        if (!IsWeaponInstance) return;

        // We expect to hit ENEMY BODY or the enemy castle, nothing else.
        if (other.gameObject.layer != LayerMask.NameToLayer("EnemyLayer")
            && other.gameObject.layer != LayerMask.NameToLayer("EnemyCastle"))
            return;

        EnemyStats enemyStats = other.GetComponent<EnemyStats>();
        if (enemyStats != null)
        {
            float dmg = playerManager.DamageApplying(enemyStats.enemyManager);

            if (CombatDiagnostics.Enabled)
            {
                float before = enemyStats.currentHP;
                CombatDiagnostics.PlayerHit(
                    playerManager.name, other.name, CurrentClipName(),
                    true, dmg, before, Mathf.Max(0f, before - dmg));
            }

            enemyStats.ApplyDamageToEnemy(dmg);
            return;
        }

        EnemyGateStats gateStats = other.GetComponentInParent<EnemyGateStats>();
        if (gateStats != null)
        {
            float dmg = playerManager.DamageApplying(gateStats.enemyManager);
            gateStats.ApplyDamageToEnemy(dmg);
        }
    }
}
