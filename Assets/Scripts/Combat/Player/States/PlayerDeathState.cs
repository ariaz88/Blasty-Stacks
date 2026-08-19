using UnityEngine;

public class PlayerDeathState : PlayerState
{
    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
    {
        if (playerManager.playerRigidbody)
        {
            playerManager.playerRigidbody.linearVelocity = Vector2.zero;
            playerManager.playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }
        playerManager.playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0, 0);
        playerManager.playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);

        playerAnimatitorManager.anim.Play("Dying");
        Destroy(playerManager.gameObject, 0.5f);
        return this;
    }
}
