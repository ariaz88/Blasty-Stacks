using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class PlayerGateStats : CharacterStats
{
    public static event Action OnGateDestroyed;   // <- broadcast to everyone
    public PlayerManager playerManager;
    public HealthBar healthBar;
    public bool isPlayerGateDestroyed;
    [SerializeField] TextMeshProUGUI baseTxt;

   

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
    }
    void Start()
    {
        if (healthBar != null)
        {
            healthBar.SetCurrentHealth(currentHP, maxHealth);

        }
        if (baseTxt) baseTxt.text = currentHP.ToString();


    }

    public void ApplyDamageToPlayerGate(float damageAmount)
    {
        // WHAT DECIDES THIS IS THE BATTLE'S INTENDED OUTCOME, not who is still alive.
        //
        // In a battle the player is meant to WIN, an enemy that slips past the fight
        // and reaches the castle must not be able to decide the match: it loses 1% of
        // its maximum per connected blow. That is visible feedback that settles
        // nothing - felling a base that way needs a hundred blows, far longer than
        // these battles run. The base used to take literally NOTHING in that case,
        // which read as a broken game rather than as a rule.
        //
        // In a battle the player is meant to LOSE, the same enemy deals its normal
        // damage. Damping it there would leave the match unable to end the way the
        // workbook says it must (Arash, 2026-09-12).
        //
        // THIS USED TO KEY OFF `HasLivingDefenders`, which tied the damping to whether
        // any hero was still standing. That is the wrong question: it damped the
        // losing battles too, right up until the last hero fell, and then let the base
        // fall at full speed in the battles that were never in danger anyway.
        if (CPBattleController.BattleIsAnExpectedWin(this))
            damageAmount = maxHealth * LevelBattleRules.BaseChipPerBlow;

        if (isPlayerGateDestroyed)
        {
            return;
        }
        currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount));
        if (baseTxt) baseTxt.text = Mathf.FloorToInt(currentHP).ToString();
        if (healthBar) healthBar.SetCurrentHealth(currentHP, maxHealth);
        if (currentHP <= 0)
        {
            DestroyGate();
            currentHP = 0;
        }
    }
    private void DestroyGate()
    {
        if (isPlayerGateDestroyed)
        {
            return;
        }
        isPlayerGateDestroyed = true;
        OnGateDestroyed?.Invoke();


        //// optional: turn off collider to stop further triggers

        ////if (destroyVFX) Instantiate(destroyVFX, transform.position, Quaternion.identity);

        //// optional: hide mesh/sprite




    }

    IEnumerator DeactivateGate()
    {
        yield return new WaitForSeconds(0.5f);

        gameObject.SetActive(false);
    }
}
