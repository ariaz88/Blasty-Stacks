using UnityEngine;


public class CombatAgent : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public UnitStatsSO stats;
    public Transform attackPoint; // where we measure from (fallback: this.transform)
    private CharacterStats health;
    private float nextAttackTime;
    private float damageAppling = 1f;

    private void Awake()
    {
        health = GetComponent<CharacterStats>();
        if (health == null) health = gameObject.AddComponent<CharacterStats>();
        if (stats == null)
            Debug.LogWarning($"{name}: UnitStatsSO not assigned."); health.Init(stats.maxHP);

        if (attackPoint == null) attackPoint = transform;
    }

    /// <summary>
    /// Try to perform one attack into the target. Returns true if we landed a hit.
    /// Call this from your AI/tower logic when you want to attack.
    /// </summary>
    public bool TryAttack(CombatAgent target)
    {
        if (stats == null || target == null) return false;
        if (!health.IsAlive || !target.health.IsAlive) return false;

        // Attack speed gate
        if (Time.time < nextAttackTime) return false;

        // Range gate
        if (!CombatMath.InRange(stats, attackPoint.position, target.attackPoint.position))
            return false;

        // Compute symmetrical damage and apply
        //damageAppling = CombatMath.DamagePerHit(stats, target.stats);

        //target.health.ApplyDamage(dmg);

        // Schedule next attack
        nextAttackTime = Time.time + CombatMath.TimeBetweenAttacks(stats);

        // (Optional) trigger animation / VFX / sound here
        // Debug.Log($"{name} hit {target.name} for {dmg} dmg. {target.health.currentHP} HP left.");

        return true;
    }

    /// <summary>
    /// Convenience method if something else already computed damage externally.
    /// </summary>
    public void ReceiveDamage(float damage)
    {
        if (!health.IsAlive) return;

        //health.ApplyDamage(damage);
    }

   
}
