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
        // WHILE THIS BASE STILL HAS DEFENDERS, a blow only CHIPS it - 1% of maximum.
        // Once every hero has fallen, the base takes full damage and the stage ends.
        //
        // This is outcome-neutral on purpose. It never asks who is supposed to win;
        // it encodes one structural rule - an attacker that slips past a live battle
        // must not decide the match on its own - mirroring the enemy gate's
        // HasLivingDefenders. A hundred connected blows to fell a defended base is
        // far longer than any battle here runs, so it settles nothing while still
        // giving visible feedback. Taking literally NOTHING read as a broken game.
        //
        // IT USED TO KEY OFF THE INTENDED OUTCOME (CPBattleController
        // .BattleIsAnExpectedWin), damping the base only in battles the script had
        // already decided the player would win. That question no longer exists: the
        // battle decides its own result.
        if (CPBattleController.HasLivingDefenders(this))
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
