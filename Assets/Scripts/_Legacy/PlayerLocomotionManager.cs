using UnityEngine;
using UnityEngine.AI;

public class PlayerLocomotionManager : MonoBehaviour
{
    public Rigidbody playerRigidbody;

    [HideInInspector]
    public EnemyStats currentTarget;

    public LayerMask enemyDetectionLayer;

    PlayerManager playerManager;
    public PlayerAnimatitorManager playerAnimatorManager;

   public NavMeshAgent navMeshAgent;

    [Header("Floats")]
    [HideInInspector]
    public float distanceFromTarget = 1.5f; 
    public float stoppingDistance = 0.5f;

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
        playerAnimatorManager = GetComponentInChildren<PlayerAnimatitorManager>();
        navMeshAgent = GetComponentInChildren<NavMeshAgent>();

        if (playerRigidbody == null)
        {
        playerRigidbody = GetComponent<Rigidbody>();
        }
    }
    private void Start()
    {
        navMeshAgent.enabled = false;
        playerRigidbody.isKinematic = false;
        navMeshAgent.updatePosition = false; // Prevent NavMeshAgent from updating position
        navMeshAgent.updateRotation = false; // We'll handle rotation manually
    }

    public void HandleDetection()
    {

        Collider[] colliders = Physics.OverlapSphere(transform.position , playerManager.detectionRadius , enemyDetectionLayer );

        for (int i = 0; i < colliders.Length; i++)
        {
            EnemyStats enemyStats = colliders[i].GetComponent<EnemyStats>();

            if (enemyStats != null)
            {
                Vector3 targetDirection = enemyStats.transform.position - transform.position;
                float viewableAngle = Vector3.Angle(targetDirection, transform.forward);
                if (viewableAngle > playerManager.minimumDetectionAngle  && viewableAngle < playerManager.maximumDetectionAngle)
                {
                    currentTarget = enemyStats;
                }
            }
        }

    }


    public void HandleMoveToTarget()
    {
        if (playerManager.isPerformingAction)
        {
            return;
        }

        if (playerManager.isInteracting)
        {
            return;
        }

        Vector3 targetDirection = currentTarget.transform.position - transform.position;
        distanceFromTarget = Vector3.Distance(currentTarget.transform.position , transform.position);
        float viewableAngle = Vector3.Angle(targetDirection, transform.forward);

        if (playerManager.isPerformingAction)
        {
            playerAnimatorManager.anim.SetFloat("Vertical" , 0 , 0.1f  , Time.deltaTime);
            navMeshAgent.enabled = false;
        }
        else
        {
            if ( distanceFromTarget > stoppingDistance )
            {

                playerAnimatorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime);
            }
            else if (distanceFromTarget <= stoppingDistance)
            {
                playerAnimatorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime);
                //playerManager.AttackTarget();

            }
        }
        //HandleRotatesTowardTarget();

        navMeshAgent.transform.localPosition = Vector3.zero;
        navMeshAgent.transform.localRotation = Quaternion.identity;
    }
    public void HandleMoveToTarget2()
    {
        if (playerManager.isPerformingAction)
        {
            playerAnimatorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime);
            navMeshAgent.enabled = false; // Ensure NavMeshAgent is disabled during attacks
            return;
        }
        if (  playerManager.isInteracting)
        {
            playerAnimatorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime);
            return;
        }

        Vector3 targetDirection = currentTarget.transform.position - transform.position;
        distanceFromTarget = Vector3.Distance(currentTarget.transform.position, transform.position);
        float viewableAngle = Vector3.Angle(targetDirection, transform.forward);

        if (distanceFromTarget > stoppingDistance)
        {
            playerAnimatorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime);
            //navMeshAgent.enabled = true; // Enable NavMeshAgent for movement
            //navMeshAgent.SetDestination(currentTarget.transform.position);
        }
        else if (distanceFromTarget <= stoppingDistance)
        {
            playerAnimatorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime);
            navMeshAgent.enabled = false; // Disable NavMeshAgent when in attack range
        }

        navMeshAgent.transform.localPosition = Vector3.zero;
        navMeshAgent.transform.localRotation = Quaternion.identity;
    }

    public void HandleRotatesTowardTarget()
    {
        //Rotate Normally

        if (playerManager.isPerformingAction)
        {
            Vector3 direction = currentTarget.transform.position - transform.position;
            direction.y = 0;
            direction.Normalize();
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(transform.rotation , targetRotation , playerManager.rotationSpeed/ Time.deltaTime);
        }

        // Rotate with NavMesh (Pathfinding)
        else
        {
            Vector3 relativeDirection = transform.InverseTransformDirection(navMeshAgent.desiredVelocity);

            Vector3 targetVelocity = playerRigidbody.linearVelocity;

            navMeshAgent.enabled = true;

            navMeshAgent.SetDestination(currentTarget.transform.position);
            playerRigidbody.linearVelocity = targetVelocity;

            transform.rotation = Quaternion.Slerp(transform.rotation ,  navMeshAgent.transform.rotation, playerManager.rotationSpeed / Time.deltaTime);

        }
    }

    // PlayerLocomotionManager.cs
    public void DisableAgentForAction()
    {
        if (navMeshAgent == null) return;
        if (navMeshAgent.enabled)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
            navMeshAgent.enabled = false;
        }
    }

    public void EnableAgent()
    {
        if (navMeshAgent == null) return;
        navMeshAgent.enabled = true;
        navMeshAgent.isStopped = false;
    }


}
