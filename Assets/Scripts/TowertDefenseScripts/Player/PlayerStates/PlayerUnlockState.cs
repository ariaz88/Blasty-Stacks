using UnityEngine;

public class PlayerUnlockState : PlayerState
{
    public PlayerPursueTargetState playerPursueTargetState;

    [Header("Targeting (2D)")]
    [SerializeField] private float scanRadius = 12f;
    [SerializeField] private float scanEverySeconds = 0.15f;
    [SerializeField] private LayerMask enemyMask = ~0;      // default: everything
    [SerializeField] private string enemyTag = "";          // optional; leave empty to ignore tag

    private float _nextScanTime = 0f;
    private bool _didInitialScan = false;

    //public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
    //{
    //    // Drive your “unlock” move anim
    //    playerAnimatitorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime);

    //    // If target already set (maybe by something else), switch now.
    //    if (playerManager.currentTarget != null)
    //        //Debug.Log(" 0");
    //    return playerPursueTargetState;

    //    // --- Reacquire logic ---
    //    // Do an immediate first scan (no waiting for Time.time)
    //    if (!_didInitialScan || Time.time >= _nextScanTime)
    //    {
    //        _didInitialScan = true;
    //        _nextScanTime = Time.time + scanEverySeconds;

    //        var found = TryFindNearestEnemy2D(playerManager.transform.position);
    //        if (found != null)
    //        {
    //            playerManager.currentTarget = found;


    //            Debug.Log(" 1");
    //            return playerPursueTargetState;
    //        }
    //        else
    //        {
    //            Debug.Log(" 2");

    //            return this;
    //        }
    //    }
    //    else
    //    {
    //        Debug.Log(" 3");

    //        return this;

    //    }


    //}
    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
    {
        playerAnimatitorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime);

        if (playerManager.currentTarget != null)
        {
            return playerPursueTargetState;
        }

        if (!_didInitialScan || Time.time >= _nextScanTime)
        {
            _didInitialScan = true;
            _nextScanTime = Time.time + scanEverySeconds;

            var found = TryFindNearestEnemy2D(playerManager.transform.position);
            if (found != null)
            {
                playerManager.currentTarget = found;

                if (playerPursueTargetState == null)
                {
                    Debug.LogError("[UnlockState] playerPursueTargetState is NOT assigned!");
                    return this;
                }

                Debug.Log("[UnlockState] Found target -> Pursue");
                return playerPursueTargetState;
            }

            return this; // nothing found this scan
        }

        return this; // between scans
    }

    private EnemyStats TryFindNearestEnemy2D(Vector3 origin)
    {
        // 1) Physics2D overlap
        var hits = Physics2D.OverlapCircleAll(origin, scanRadius, enemyMask);
        EnemyStats bestEnemy = null;
        float best = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (!col) continue;

            // Try on this object, then on its parents
            var enemy = col.GetComponent<EnemyStats>();
            if (!enemy) enemy = col.GetComponentInParent<EnemyStats>();
            if (!enemy) continue;

            if (!string.IsNullOrEmpty(enemyTag) && !enemy.CompareTag(enemyTag))
                continue;

            float d = (enemy.transform.position - origin).sqrMagnitude;
            if (d < best) { best = d; bestEnemy = enemy; }
        }

        if (bestEnemy != null)
            return bestEnemy;

        // 2) Fallback: brute force search in scene (helps catch layer/mask mistakes)
        var all = Object.FindObjectsOfType<EnemyStats>(includeInactive: false);
        best = float.PositiveInfinity;
        for (int i = 0; i < all.Length; i++)
        {
            var e = all[i];
            if (!e) continue;
            if (!string.IsNullOrEmpty(enemyTag) && !e.CompareTag(enemyTag))
                continue;

            float d = (e.transform.position - origin).sqrMagnitude;
            if (d < best && d <= scanRadius * scanRadius)
            {
                best = d;
                bestEnemy = e;
            }
        }

        return bestEnemy;
    }

#if UNITY_EDITOR
    // Visualize scan radius in Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        var tr = Application.isPlaying ? transform : (this as Object) ? (transform) : null;
        if (tr != null)
            Gizmos.DrawWireSphere(tr.position, scanRadius);
    }
#endif
}
