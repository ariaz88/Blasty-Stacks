using UnityEngine;

public class EnemyAnimatorManager : AnimatorManager
{
    EnemyLocomotionManager locomotion;
    EnemyDamageCollider enemyDamageCollider;
    HeroAttackVfx attackVfx;

    void Awake()
    {
        locomotion = GetComponentInParent<EnemyLocomotionManager>();
        enemyDamageCollider = GetComponentInChildren<EnemyDamageCollider>();
        attackVfx = GetComponentInParent<HeroAttackVfx>();
    }

    public void EnableEnemyDamageCollier()
    {
        enemyDamageCollider.enemyDmgCollider.enabled = true;
        // Optional per-enemy swing VFX (same component the heroes use), on the hitbox frame.
        if (attackVfx != null) attackVfx.Play();

    }

    public void DisableEnemyDamageCollider()
    {
        enemyDamageCollider.enemyDmgCollider.enabled = false;

    }


















}
