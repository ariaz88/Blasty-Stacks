using UnityEngine;

public class PlayerAnimatitorManager : AnimatorManager
{
    PlayerManager PlayerManager;
    private void Awake()
    {
        
        PlayerManager = GetComponentInParent<PlayerManager>();
        
    }
    public void EnableDamageCollier()
    {
        PlayerManager.playerDamageCollider.enabled = true;

    }

    public void DisableDamageCollider()
    {
        PlayerManager.playerDamageCollider.enabled = false;

    }





}
