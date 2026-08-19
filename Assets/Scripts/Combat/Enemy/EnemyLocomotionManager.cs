using UnityEngine;

public class EnemyLocomotionManager : MonoBehaviour
{
    public Rigidbody enemyRigidbody;
    public EnemyAnimatorManager enemyAnimatorManager;
    public EnemyManager enemyManager;


    //[HideInInspector]
    public PlayerStats currentTarget;

    public LayerMask playerDetectionLayer;



    [Header("Floats")]
    [HideInInspector]
    public float distanceFromTarget = 1.5f;
    public float stoppingDistance = 0.5f;

    private void Awake()
    {
        enemyManager = GetComponent<EnemyManager>();
        enemyAnimatorManager = GetComponentInChildren<EnemyAnimatorManager>();

        if (enemyRigidbody == null)
        {
            enemyRigidbody = GetComponent<Rigidbody>();
        }
    }
    private void Start()
    {
        enemyRigidbody.isKinematic  = false;
        
    }

    public void HandleDetection()
    {

        Collider[] colliders = Physics.OverlapSphere(transform.position, enemyManager.detectionRadius, playerDetectionLayer);

        for (int i = 0; i < colliders.Length; i++)
        {
            PlayerStats playerStats = colliders[i].GetComponent<PlayerStats>();

            if (playerStats != null)
            {
                Vector3 targetDirection = playerStats.transform.position - transform.position;
                float viewableAngle = Vector3.Angle(targetDirection, transform.forward);
                    currentTarget = playerStats;
                //if (viewableAngle > enemyManager.minimumDetectionAngle && viewableAngle < enemyManager.maximumDetectionAngle)
                //{
                //}
            }
        }

    }

    public void HandleMoveToTarget()
    {
        if (enemyManager.isPerformingAction)
        {
            return;
        }

        if (enemyManager.isInteracting)
        {
            return;
        }

        Vector3 targetDirection = currentTarget.transform.position - transform.position;
        distanceFromTarget = Vector3.Distance(currentTarget.transform.position, transform.position);
        float viewableAngle = Vector3.Angle(targetDirection, transform.forward);

        if (enemyManager.isPerformingAction)
        {
            enemyAnimatorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime);
        }
        else
        {
            if (distanceFromTarget > stoppingDistance)
            {

                enemyAnimatorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime);
                Debug.Log(" Move the Enemy");
            }
            else if (distanceFromTarget <= stoppingDistance)
            {
                enemyAnimatorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime);
                //playerManager.AttackTarget();

            }
        }
        //HandleRotatesTowardTarget();

    
    }

    public void HandleRotatesTowardTarget()
    {
        //Rotate Normally

        if (enemyManager.isPerformingAction)
        {
            Vector3 direction = currentTarget.transform.position - transform.position;
            direction.y = 0;
            direction.Normalize();
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, enemyManager.rotationSpeed / Time.deltaTime);
        }

       
    }
}
