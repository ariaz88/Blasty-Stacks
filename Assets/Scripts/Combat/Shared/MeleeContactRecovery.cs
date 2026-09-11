using UnityEngine;

/// <summary>Small physical sidesteps after sustained missed melee contact.
/// Added only to stage 1-5 fighters; never changes damage, HP or gate outcomes.</summary>
[DefaultExecutionOrder(500)]
public sealed class MeleeContactRecovery : MonoBehaviour
{
    PlayerManager player;
    EnemyManager enemy;
    EnemyLocoMotion enemyMotion;
    CharacterStats health;
    Rigidbody2D body;
    CharacterStats trackedTarget;
    float lastContact, attemptStarted, retryAfter;
    int attempts;
    Vector2 destination;
    public bool IsRepositioning { get; private set; }
    public int RepositionCount { get; private set; }
    void Awake()
    {
        player = GetComponent<PlayerManager>(); enemy = GetComponent<EnemyManager>();
        enemyMotion = GetComponent<EnemyLocoMotion>();
        health = GetComponent<CharacterStats>(); body = GetComponent<Rigidbody2D>();
        lastContact = Time.time;
        // Enemy locomotion has to know to stand down while we drive the body. It
        // cannot find us with GetComponent because CPBattleController ADDS this
        // component at runtime, long after EnemyLocoMotion.Awake has run.
        if (enemyMotion) enemyMotion.BindContactRecovery(this);
    }
    public void ReportContact(CharacterStats target)
    {
        if (target == trackedTarget) { lastContact = Time.time; attempts = 0; }
    }
    void FixedUpdate()
    {
        if (!body || !health || health.currentHP <= 0 || GameplayPause.IsPaused || !LevelGameManager.IsBattleRunning) { Stop(); return; }
        if (player && !player.isUnlocked) { Stop(); return; }
        CharacterStats target = player ? player.currentTarget : enemyMotion ? enemyMotion.currentTarget : null;
        if (!target || target.currentHP <= 0) { trackedTarget = null; Stop(); return; }
        if (trackedTarget != target)
        {
            trackedTarget = target; lastContact = Time.time; attempts = 0; Stop();
        }
        float range = player ? player.maxAttackRange : enemyMotion.stoppingDistance;
        // Let the hero complete its approach or recovery while the enemy holds
        // position. Two recovery movers following each other recreate the pull
        // even when EnemyLocoMotion itself has correctly stopped.
        if (enemy && target is PlayerStats heroHealth)
        {
            var hero = heroHealth.PlayerManager;
            bool engagingUs = hero && hero.currentTarget && hero.currentTarget.enemyManager == enemy;
            if (engagingUs)
            {
                Stop(); lastContact = Time.time; return;
            }
        }
        if (IsRepositioning)
        {
            if (Vector2.Distance(body.position, destination) < 0.06f || Time.time - attemptStarted > 1.1f)
            {
                Stop(); retryAfter = Time.time + 1.5f; lastContact = Time.time; return;
            }
            body.bodyType = RigidbodyType2D.Dynamic;
            float speed = player ? player.CurrentMoveSpeed : enemyMotion.CurrentMoveSpeed;
            body.MovePosition(Vector2.MoveTowards(body.position, destination, Mathf.Max(0.5f, speed) * Time.fixedDeltaTime));
            if (player) { player.SetAnimMoving(true); player.FaceLeft(target.transform.position.x < transform.position.x); }
            else { enemyMotion.SetAnimMoving(true); enemy.FaceLeft(target.transform.position.x < transform.position.x); }
            return;
        }
        // Leave a successful fight alone. Retry only near melee distance and
        // between animations, rather than dragging a fighter during its swing.
        if (Time.time < retryAfter || Time.time - lastContact < 2.5f || Vector2.Distance(body.position, target.transform.position) > range + 0.35f) return;
        if (player ? player.isPerformingAction : enemy.isPerformingAction) return;
        float side = body.position.x < target.transform.position.x ? -1 : 1;
        // Goes through MeleeEngagement so a retry can only ever land on a LEGAL
        // fighting spot. The old line put the unit at the target's exact Y with a
        // hand-rolled radius that shrank to 0.55 * range on every third attempt -
        // i.e. it deliberately walked the attacker deeper INTO the body it was
        // meant to be standing beside, which is what made the pair overlap.
        //
        // A retry varies the SLOT (a step up or down the same flank), never the
        // side. The old `if ((attempts & 1) != 0) side = -side;` marched the unit
        // straight through the target to its far side, and a unit standing on the
        // far side of something that is also trying to stand on ITS far side is
        // the one arrangement that has no solution - see MeleeEngagement.StandPoint.
        destination = MeleeEngagement.StandPoint(
            target.transform.position, range, side, 1 + (attempts % 2));
        attempts++; RepositionCount++; attemptStarted = Time.time; IsRepositioning = true;
        if (player && player.playerDamageCollider) player.playerDamageCollider.enabled = false;
        if (enemy && enemy.enemyDamageCollider) enemy.enemyDamageCollider.enabled = false;
    }
    void Stop()
    {
        if (IsRepositioning && body) body.linearVelocity = Vector2.zero;
        IsRepositioning = false;
    }
}
