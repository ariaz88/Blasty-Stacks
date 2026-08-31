using UnityEngine;
public class EnemyDamageCollider : MonoBehaviour
{
    private EnemyManager enemyManager;
    private float damageToPlayer;

    private int playerBodyLayer;
    private int playerCastleLayer;
    public CapsuleCollider2D enemyDmgCollider;

    private void Awake()
    {
        enemyManager = GetComponentInParent<EnemyManager>();

        playerBodyLayer = LayerMask.NameToLayer("PlayerLayer");
        playerCastleLayer = LayerMask.NameToLayer("PlayerCastle");
    }

    private void Start()
    {
        if (enemyDmgCollider != null)
        {
            enemyDmgCollider.enabled = false;
        }
    }

    /// <summary>
    /// Watchdog, mirroring the one in PlayerDamageCollider and closing the same hole: an
    /// attack cut short before its DisableEnemyDamageCollider frame would otherwise leave the
    /// weapon live, and the enemy would then damage whatever it walked into.
    ///
    /// Closed by TIMEOUT rather than by testing IsAttacking. IsAttacking is reliable enough to
    /// read, but gating on it risks cutting a swing short the instant the flag drops - and the
    /// player-side equivalent of that mistake silently made enemies invulnerable. A timeout
    /// cannot cancel a legitimate hit; it can only bound how long a stuck weapon leaks.
    /// </summary>
    private const float MaxAttackWindow = 0.35f;

    private bool wasEnabled;
    private float openedAt = -1f;

    private void LateUpdate()
    {
        if (enemyDmgCollider == null) return;

        if (enemyDmgCollider.enabled && !wasEnabled)
            openedAt = Time.time;

        if (enemyDmgCollider.enabled && openedAt >= 0f
            && Time.time - openedAt > MaxAttackWindow)
        {
            enemyDmgCollider.enabled = false;

            if (CombatDiagnostics.Enabled)
                Debug.Log($"[CD] {Time.time:F2}s  >>> WATCHDOG CLOSED a stuck weapon on " +
                          $"'{(enemyManager ? enemyManager.name : name)}'");
        }

        wasEnabled = enemyDmgCollider.enabled;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (enemyManager == null) return;

        int otherLayer = other.gameObject.layer;

        // Mirrors the guard in PlayerDamageCollider, and for the same reason: the collider
        // being enabled is NOT sufficient proof that an attack is happening. It is switched
        // on by the EnableEnemyDamageCollier animation event and switched off by
        // DisableEnemyDamageCollider, so an attack cut short before the disable frame leaves
        // the weapon live and the enemy then damages whatever it walks into.
        bool isAttacking = enemyManager.IsAttacking;

        // =========================
        // Player BODY hit
        // =========================
        if (otherLayer == playerBodyLayer)
        {
            PlayerStats playerStats = other.GetComponent<PlayerStats>();
            if (playerStats == null)
                return;

            PlayerManager pm = playerStats.PlayerManager;
            if (pm == null)
                return;

            // A player in the lock state is not a valid target.
            if (pm.currentState == pm.PlayerLockState)
                return;

            damageToPlayer = enemyManager.DamageApplying(pm);

            if (CombatDiagnostics.Enabled)
            {
                float before = playerStats.currentHP;
                float after = isAttacking ? Mathf.Max(0f, before - damageToPlayer) : before;
                CombatDiagnostics.EnemyHit(enemyManager.name, other.name,
                                           isAttacking, damageToPlayer, before, after);
            }

            if (!isAttacking) return;

            playerStats.ApplyDamageToPlayer(damageToPlayer);
            return;
        }

        // =========================
        // Player CASTLE hit
        // =========================
        if (otherLayer == playerCastleLayer)
        {
            PlayerGateStats gateStats = other.GetComponentInParent<PlayerGateStats>();
            if (gateStats == null)
                return;

            if (!isAttacking) return;

            damageToPlayer = enemyManager.DamageApplying(gateStats.playerManager);
            gateStats.ApplyDamageToPlayerGate(damageToPlayer);
        }
    }
}
