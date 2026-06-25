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

    //private void OnAnimatorMove()
    //{
    //    if (PlayerManager.isPerformingAction)
    //    {
    //        // During actions (e.g., attacks), stop movement to prevent drift
    //        PlayerManager.playerRigidbody.linearVelocity = Vector2.zero;
    //        return;
    //    }

    //    // Respect Animator’s root motion flag
    //    if (!anim.applyRootMotion) return;

    //    float dt = Time.deltaTime;
    //    // Use Vector2 for 2D movement
    //    Vector2 delta = anim.deltaPosition; // Animator.deltaPosition works in 2D

    //    // Avoid division by zero (though unlikely in Unity)
    //    if (dt > 0f)
    //    {
    //        Vector2 vel = delta / dt;
    //        PlayerManager.playerRigidbody.linearVelocity = vel;
    //    }
    //    else
    //    {
    //        PlayerManager.playerRigidbody.linearVelocity = Vector2.zero;
    //    }
    //}

}
