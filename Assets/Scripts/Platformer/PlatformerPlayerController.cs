using UnityEngine;

/// <summary>横板通关 - 玩家控制器</summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlatformerPlayerController : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;

    [Header("地面检测")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private bool isGrounded;
    private float moveInput;
    private SpriteRenderer sr;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }

        // 翻转角色朝向
        if (moveInput > 0 && sr != null)   sr.flipX = false;
        if (moveInput < 0 && sr != null)   sr.flipX = true;
    }

void FixedUpdate()
    {
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
