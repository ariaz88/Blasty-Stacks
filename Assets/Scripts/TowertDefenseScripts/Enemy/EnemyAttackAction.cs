using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy/Attack Action")]
public class EnemyAttackAction : ScriptableObject
{
    public string animationName = "Attack_01";
    public float minDistanceNeededToAttack = 0;
    public float maxDistanceNeededToAttack = 3;
    public float recoveryTime = 0.6f;
    public float speedMultiplier = 1.0f; // 1 = normal
}
