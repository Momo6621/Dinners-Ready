using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float depthSpeed = 5f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 20f;
    [SerializeField] private float jumpBufferFrames = 0.1f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private int maxAirJumps = 1;
    [SerializeField] private float gravityScale = 5f;
    [SerializeField] private float fallGravityScale = 10f;

    [Header("Wall")]
    [SerializeField] private float wallSlidingSpeed = 2f;
    [SerializeField] private Vector2 wallJumpPower = new Vector2(8f, 16f);
    [SerializeField] private float wallJumpDuration = 0.4f;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask wallLayer;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashTime = 0.2f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Attack")]
    [SerializeField] private float timeBetweenAttack = 0.3f;
    [SerializeField] private Transform sideAttackTransform, upAttackTransform, downAttackTransform;
    [SerializeField] private Vector2 sideAttackArea, upAttackArea, downAttackArea;
    [SerializeField] private LayerMask attackableLayer;
    [SerializeField] private float damage = 10f;

    [Header("Recoil")]
    [SerializeField] private int recoilXSteps = 5;
    [SerializeField] private int recoilYSteps = 5;
    [SerializeField] private float recoilXSpeed = 100f;
    [SerializeField] private float recoilYSpeed = 100f;

    [Header("Health")]
    public int health;
    public int maxHealth;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckY = 0.2f;
    [SerializeField] private float groundCheckX = 0.5f;
    [SerializeField] private LayerMask whatIsGround;

    public static Player Instance;
    [HideInInspector] public PlayerStatus pState;

    private Rigidbody rb;
    private Animator anim;
    private SphereCollider sc;

    private float xAxis, yAxis, zAxis;
    private float timeSinceAttack;
    private float jumpBufferCounter;
    private float coyoteTimeCounter;
    private int airJumpCounter;

    private bool canDash = true;
    private bool dashed;
    private bool isWallSliding;
    private bool isWallJumping;
    private float wallJumpDirection;
    private float wallJumpCounter;

    private int stepsXRecoiled, stepsYRecoiled;

    private Vector3 respawnPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        health = maxHealth;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        pState = GetComponent<PlayerStatus>();
        sc = GetComponent<SphereCollider>();
        respawnPoint = transform.position;
    }

    private void Update()
    {
        GetInputs();
        UpdateJumpVariables();

        if (pState.dashing) return;

        Move();
        Jump();
        StartDash();
        Attack();
        Recoil();
        WallSlide();
        WallJump();
        ApplyGravity();

        if (!isWallJumping) Flip();
    }

    private void GetInputs()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        yAxis = Input.GetAxisRaw("Vertical");
        zAxis = Input.GetAxisRaw("Depth");
    }

    private void Flip()
    {
        if (xAxis == 0) return;

        bool shouldFaceRight = xAxis > 0;
        if (pState.lookingRight != shouldFaceRight)
        {
            pState.lookingRight = shouldFaceRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1f;
            transform.localScale = scale;
        }
    }

    private void Move()
    {
        if (!isWallJumping)
        {
            rb.linearVelocity = new Vector3(speed * xAxis, rb.linearVelocity.y, depthSpeed * zAxis);
        }
        anim.SetBool("Walking", (rb.linearVelocity.x != 0 || rb.linearVelocity.z != 0) && Grounded());
    }

    private void ApplyGravity()
    {
        if (Grounded() || pState.dashing) return;

        float scale = rb.linearVelocity.y < 0 ? fallGravityScale : gravityScale;
        rb.linearVelocity += Vector3.up * Physics.gravity.y * (scale - 1) * Time.deltaTime;
    }

    private void StartDash()
    {
        if (Input.GetButtonDown("Dash") && canDash && !dashed)
        {
            StartCoroutine(Dash());
            dashed = true;
        }
        if (Grounded()) dashed = false;
    }

    private IEnumerator Dash()
    {
        canDash = false;
        pState.dashing = true;
        anim.SetTrigger("Dashing");

        rb.linearVelocity = new Vector3(transform.localScale.x * dashSpeed, 0, 0);
        yield return new WaitForSeconds(dashTime);

        pState.dashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void Attack()
    {
        timeSinceAttack += Time.deltaTime;
        if (!Input.GetKeyDown(KeyCode.X) || timeSinceAttack < timeBetweenAttack) return;

        timeSinceAttack = 0;
        anim.SetTrigger("Attacking");

        if (yAxis > 0)
            Hit(upAttackTransform, upAttackArea, ref pState.recoilingY, recoilYSpeed);
        else if (yAxis < 0 && !Grounded())
            Hit(downAttackTransform, downAttackArea, ref pState.recoilingY, recoilYSpeed);
        else
            Hit(sideAttackTransform, sideAttackArea, ref pState.recoilingX, recoilXSpeed);
    }

    private void Hit(Transform attackTransform, Vector2 attackArea, ref bool recoilDir, float recoilStrength)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackTransform.position, attackArea, 0, attackableLayer);
        if (hits.Length > 0) recoilDir = true;

        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null)
            {
                Vector3 direction = (transform.position - hit.transform.position).normalized;
                enemy.EnemyHit(damage, direction, recoilStrength);
            }
        }
    }

    private void Recoil()
    {
        if (pState.recoilingX)
        {
            rb.linearVelocity = new Vector3(pState.lookingRight ? -recoilXSpeed : recoilXSpeed, rb.linearVelocity.y, rb.linearVelocity.z);

            if (++stepsXRecoiled >= recoilXSteps) StopRecoilX();
        }

        if (pState.recoilingY)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, yAxis < 0 ? recoilYSpeed : -recoilYSpeed, rb.linearVelocity.z);
            airJumpCounter = 0;

            if (++stepsYRecoiled >= recoilYSteps || Grounded()) StopRecoilY();
        }
    }

    private void StopRecoilX() { stepsXRecoiled = 0; pState.recoilingX = false; }
    private void StopRecoilY() { stepsYRecoiled = 0; pState.recoilingY = false; }

    public void TakeDamage(float dmg)
    {
        health = Mathf.Clamp(health - Mathf.RoundToInt(dmg), 0, maxHealth);
        StartCoroutine(InvincibilityFrames());
    }

    private IEnumerator InvincibilityFrames()
    {
        pState.invincible = true;
        yield return new WaitForSeconds(1f);
        pState.invincible = false;
    }

    private bool IsWalled() => Physics2D.OverlapCircle(wallCheck.position, 2f, wallLayer);

    private void WallSlide()
    {
        isWallSliding = IsWalled() && !Grounded() && xAxis != 0f;
        if (isWallSliding)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -wallSlidingSpeed, float.MaxValue), rb.linearVelocity.z);
    }

    private void WallJump()
    {
        if (isWallSliding)
        {
            isWallJumping = false;
            wallJumpDirection = -transform.localScale.x;
            wallJumpCounter = 0.2f;
            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump") && wallJumpCounter > 0f)
        {
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpPower.x, wallJumpPower.y);
            wallJumpCounter = 0f;

            if (transform.localScale.x != wallJumpDirection)
            {
                pState.lookingRight = !pState.lookingRight;
                Vector3 scale = transform.localScale;
                scale.x *= -1f;
                transform.localScale = scale;
            }

            Invoke(nameof(StopWallJumping), wallJumpDuration);
        }
    }

    private void StopWallJumping() => isWallJumping = false;

    public bool Grounded()
    {
        Vector3 pos = groundCheckPoint.position;
        return Physics.Raycast(pos, Vector3.down, groundCheckY, whatIsGround)
            || Physics.Raycast(pos + new Vector3(groundCheckX, 0, 0), Vector3.down, groundCheckY, whatIsGround)
            || Physics.Raycast(pos + new Vector3(-groundCheckX, 0, 0), Vector3.down, groundCheckY, whatIsGround);
    }

    private void Jump()
    {
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            pState.jumping = false;
        }

        if (!pState.jumping && jumpBufferCounter > 0 && coyoteTimeCounter > 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            pState.jumping = true;
        }
        else if (!Grounded() && airJumpCounter < maxAirJumps && Input.GetButtonDown("Jump"))
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            pState.jumping = true;
            airJumpCounter++;
        }

        anim.SetBool("Jumping", !Grounded());
    }

    private void UpdateJumpVariables()
    {
        if (Grounded())
        {
            coyoteTimeCounter = coyoteTime;
            pState.jumping = false;
            airJumpCounter = 0;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        jumpBufferCounter = Input.GetButtonDown("Jump") ? jumpBufferFrames : jumpBufferCounter - Time.deltaTime * 15;
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.CompareTag("Enemy") && health <= 0)
            transform.position = respawnPoint;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(sideAttackTransform.position, sideAttackArea);
        Gizmos.DrawWireCube(upAttackTransform.position, upAttackArea);
        Gizmos.DrawWireCube(downAttackTransform.position, downAttackArea);
    }
}s