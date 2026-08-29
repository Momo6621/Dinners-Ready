using UnityEngine;

/// <summary>
/// 2D melee-combat movement system.
/// Handles walking, dashing, facing direction, and attack "lunge" movement
/// (movement that's driven by the attack itself rather than player input,
/// similar to Hollow Knight / Dead Cells style combat).
/// 
/// Attach to a GameObject with Rigidbody2D + Collider2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MeleeMovement2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 60f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Attack Lunge")]
    [SerializeField] private float attackLungeSpeed = 4f;
    [SerializeField] private float attackLungeDuration = 0.12f;
    [SerializeField] private float attackMoveMultiplier = 0.2f; // how much normal input still applies during attack

    [Header("Ground Check (optional, for jump support)")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private float jumpForce = 12f;

    private Rigidbody2D rb;
    private float moveInput;
    private bool facingRight = true;
    private bool isGrounded;

    // State
    private enum State { Normal, Dashing, Attacking }
    private State currentState = State.Normal;

    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;

    private float attackTimer;
    private Vector2 attackLungeDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // Movement input (disabled/blended during dash & attack)
        moveInput = Input.GetAxisRaw("Horizontal");

        if (moveInput > 0 && !facingRight) Flip();
        else if (moveInput < 0 && facingRight) Flip();

        // Dash
            if (Input.GetKeyDown(KeyCode.LeftShift) && currentState == State.Normal && dashCooldownTimer <= 0f)
            {
                StartDash();
            }

        // Attack
        if (Input.GetMouseButtonDown(0) && currentState != State.Dashing)
        {
            StartAttack();
        }

        // Jump (optional)
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            if (Input.GetButtonDown("Jump") && isGrounded && currentState == State.Normal)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
        }

        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case State.Normal:
                NormalMove();
                break;
            case State.Dashing:
                DashMove();
                break;
            case State.Attacking:
                AttackMove();
                break;
        }
    }

    private void NormalMove()
    {
        float targetSpeed = moveInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        float movement = speedDiff * accelRate * Time.fixedDeltaTime;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
    }

    private void StartDash()
    {
        currentState = State.Dashing;
        dashTimer = dashDuration;
        dashDirection = new Vector2(facingRight ? 1f : -1f, 0f);
        // If you want dash to ignore gravity, set rb.gravityScale = 0f here and restore after.
    }

    private void DashMove()
    {
        rb.linearVelocity = dashDirection * dashSpeed;

        dashTimer -= Time.fixedDeltaTime;
        if (dashTimer <= 0f)
        {
            currentState = State.Normal;
            dashCooldownTimer = dashCooldown;
        }
    }

    private void StartAttack()
    {
        // Only starts a lunge if not already attacking (extend with combo logic as needed)
        if (currentState == State.Attacking) return;

        currentState = State.Attacking;
        attackTimer = attackLungeDuration;
        attackLungeDirection = new Vector2(facingRight ? 1f : -1f, 0f);

        // Trigger animation here, e.g. animator.SetTrigger("Attack");
    }

    private void AttackMove()
    {
        // Blend a small forward lunge with limited player steering
        float lungeX = attackLungeDirection.x * attackLungeSpeed;
        float steer = moveInput * moveSpeed * attackMoveMultiplier;

        rb.linearVelocity = new Vector2(lungeX + steer, rb.linearVelocity.y);

        attackTimer -= Time.fixedDeltaTime;
        if (attackTimer <= 0f)
        {
            currentState = State.Normal;
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
