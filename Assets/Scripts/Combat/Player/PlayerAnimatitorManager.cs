using UnityEngine;

public class PlayerAnimatitorManager : AnimatorManager
{
    PlayerManager PlayerManager;
    HeroAttackVfx attackVfx;
    private void Awake()
    {

        PlayerManager = GetComponentInParent<PlayerManager>();
        attackVfx = GetComponentInParent<HeroAttackVfx>();

    }
    public void EnableDamageCollier()
    {
        PlayerManager.playerDamageCollider.enabled = true;
        // Optional per-hero swing VFX, fired on the frame the hitbox opens.
        if (attackVfx != null) attackVfx.Play();

    }

    public void DisableDamageCollider()
    {
        PlayerManager.playerDamageCollider.enabled = false;

    }





}
