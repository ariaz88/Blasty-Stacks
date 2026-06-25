using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]


public class TopDownMover2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 0.4f;   // units/second
    public int direction = 1;        // 1 = up, -1 = down
    public bool isMoving = true;     // toggle at runtime if needed

    Rigidbody2D rb;
    Animator anim;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();

        // Rigidbody2D recommended setup:
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;
    }

    void Update()
    {
        // Drive the Blend Tree:
        if (isMoving)
        {
            anim.SetFloat("Horizontal", 0f);
            //anim.SetFloat("Vertical", direction > 0 ? 1f : 1f); // (0,1) = Walking in your tree
            if (direction>0 || direction<0)
            {
                anim.SetFloat("Vertical", 1);
            }
            else
            {
                anim.SetFloat("Vertical", 0f); // (0,0) = Idle

            }
        }
        else
        {
            anim.SetFloat("Horizontal", 0f);
            anim.SetFloat("Vertical", 0f); // (0,0) = Idle
        }
    }

    void FixedUpdate()
    {
        if (!isMoving) return;

        Vector2 delta = new Vector2(0f, direction * moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(rb.position + delta);
    }
}
