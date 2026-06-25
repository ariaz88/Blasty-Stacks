using UnityEngine;

public class EnemyAnimatorManager : AnimatorManager
{
    EnemyLocomotionManager locomotion;
    EnemyDamageCollider enemyDamageCollider;

    void Awake()
    {
        locomotion = GetComponentInParent<EnemyLocomotionManager>();
        //enemyManager = GetComponentInParent<EnemyManager>();
        enemyDamageCollider = GetComponentInChildren<EnemyDamageCollider>();
    }

    public void EnableEnemyDamageCollier()
    {
        enemyDamageCollider.enemyDmgCollider.enabled = true;

    }

    public void DisableEnemyDamageCollider()
    {
        enemyDamageCollider.enemyDmgCollider.enabled = false;

    }

    // Pure root-motion translation: RB velocity from deltaPosition
    //void OnAnimatorMove()
    //{
    //    // If we’re in an attack, don’t translate (prevents drift/lunge unless you want it)
    //    if (enemyManager.isPerformingAction)
    //    {
    //        locomotion.enemyRigidbody.linearVelocity = Vector3.zero;
    //        return;
    //    }

    //    //if (!anim.applyRootMotion) return; // respect Animator’s flag like a good citizen

    //    float dt = Time.deltaTime;
    //    Vector3 delta = anim.deltaPosition;
    //    delta.y = 0f;
    //    //// Optional: neutralize Animator.speed scaling so “faster attack” doesn’t lunge farther
    //    //if (IsInTaggedState("Attack") && anim.speed != 0f) delta /= anim.speed;

    //    Vector3 vel = delta / dt;
    //    locomotion.enemyRigidbody.linearVelocity = vel;
    //}

    //bool IsInTaggedState(string tag)
    //{
    //    var info = anim.GetCurrentAnimatorStateInfo(0);
    //    if (info.IsTag(tag)) return true;
    //    var next = anim.GetNextAnimatorStateInfo(0);
    //    return next.IsTag(tag);
    //}

    //public System.Collections.IEnumerator ResetSpeedOnExit(float resetTo, string tag)
    //{
    //    while (IsInTaggedState(tag)) yield return null;
    //    anim.speed = resetTo;
    //}

    //private void OnAnimatorMove3()
    //{
    //    if (!anim.applyRootMotion) return;

    //    float dt = Time.deltaTime;

    //    // Original animator delta (usually X/Z on ground)
    //    Vector3 dp = anim.deltaPosition;

    //    // Remap Z -> Y, and zero Z so we stay in 2D
    //    Vector3 dpXY = new Vector3(dp.x, dp.z, 0f);

    //    // Optional: if you change Animator.speed during attacks but want same travel, neutralize:
    //    //if (anim.speed != 0f && IsInAttackState()) dpXY /= anim.speed;

    //    // Convert to velocity and apply to RB
    //    Vector3 v = dpXY / dt;
    //    locomotion.enemyRigidbody.linearVelocity = v;

    //    // You can ignore deltaRotation or remap Yaw->Z:
    //    // float yaw = anim.deltaRotation.eulerAngles.y;
    //    // var extraRot = Quaternion.Euler(0f, 0f, yaw);
    //    // transform.rotation = extraRot * transform.rotation;
    //}
    //void OnAnimatorMove()
    //{
    //    // If we’re in an attack, don’t translate (prevents drift unless desired)
    //    if (enemyManager.isPerformingAction)
    //    {
    //        locomotion.enemyRigidbody.linearVelocity = Vector2.zero;
    //        return;
    //    }

    //    // Respect Animator’s root motion flag
    //    if (!anim.applyRootMotion) return;

    //    float dt = Time.deltaTime;
    //    // Use Vector2 for 2D movement
    //    Vector2 delta = anim.deltaPosition; // Animator.deltaPosition is still valid in 2D

    //    // Avoid division by zero (though unlikely in Unity)
    //    if (dt > 0f)
    //    {
    //        Vector2 vel = delta / dt;
    //        locomotion.enemyRigidbody.linearVelocity = vel;
    //    }
    //    else
    //    {
    //        locomotion.enemyRigidbody.linearVelocity = Vector2.zero;
    //    }
    //}

    //private bool IsInAttackState()
    //{
    //    var s0 = anim.GetCurrentAnimatorStateInfo(0);
    //    var sN = anim.GetNextAnimatorStateInfo(0);
    //    return s0.IsTag("Attack") || sN.IsTag("Attack");
    //}

}
