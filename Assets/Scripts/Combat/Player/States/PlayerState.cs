using UnityEngine;

public abstract class PlayerState : MonoBehaviour
{
    public abstract PlayerState Tick(PlayerManager playerManager , PlayerStats playerStats , PlayerAnimatitorManager playerAnimatitorManager);
}
