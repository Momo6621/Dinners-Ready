using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class BaseSol : MonoBehaviour
{
    public static BaseSol Instance;
    [Header("Vertical")]
    [SerializeField] private float jumpForce = 20f;
    private float jumpBufferCounter = 0;
    [SerializeField] private float jumpBufferTime;
    private float coyoteTimeCounter = 0;
    [SerializeField] private float coyoteTime;
    private int airJumpCounter = 0;
    [SerializeField] private int maxAirJumps;
    [SerializeField] private float gravityScale = 5;
    [SerializeField] private float ascendGravityScale = 3f;
    [SerializeField] private float fallGravityScale = 10;

    [Header("Horizontal")]
    [SerializeField] private float speed = 14;
    [SerializeField] private float stopSpeed = 35f;
    [SerializeField] private float deceleration = 50f;
    [SerializeField] private float groundStickSpeed = 2f;

    [Header("Braking")]
    [SerializeField] private float tapBrake = 2f;
    [SerializeField] private float brakeStart = 20f;
    [SerializeField] private float brakeMax = 80f;
    [SerializeField] private float brakeRamp = 0.35f;
    private float brakeTimer;

    [Header("Wall")]
    private bool isWallSliding;
    [SerializeField] private float wallSlidingSpeed = 10f;
    private bool isWallJumping;
    [SerializeField] private float wallJumpingTime = 0.2f;
    private float wallJumpingCounter;
    [SerializeField] private float ledgeCheckDistance = 0.5f;
    [SerializeField] private float wallJumpingDuration = 0.4f;
    [SerializeField] private float wallCheckRadius = 0.2f;
    [SerializeField] private float flipWallBuffer = 0.5f;
    [SerializeField] private Vector2 wallJumpingPower = new Vector2(16f, 32f);
    [SerializeField] private float wallSlideGravityScale = 4f;
    [SerializeField] private Collider2D col;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 30f;
    [SerializeField] private float dashDistance = 4f;
    [SerializeField] private float dashCooldown = 0.2f;
    [SerializeField] private float dashBounceMultiplier = 0.6f;
    [SerializeField] private float bounceSpeed = 8f;
    [SerializeField] private int maxDashBounces = 1;
    [SerializeField] private float dashBounceLockoutTime = 0.1f;
    [SerializeField] private float dashBounceTime = 0.12f;
    [SerializeField] private float dashGravity = 5f;
    [SerializeField] private float dashSteerSpeed = 10f;

    [Header("Momentum")]
    [SerializeField] private float dashMomentumDecayTime = 0.4f;
    [SerializeField] private float jumpMomentumDecayTime = 0.3f;
    [SerializeField] private float wallJumpMomentumDecayTime = 0.6f;
    private bool preservingMomentum;
    private float momentumTimer;

    [Header("Ground")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckY = 0.2f;
    [SerializeField] private float groundCheckX = 0.5f;
    [SerializeField] private float ledgeBuffer = 0.15f;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private LayerMask wallLayer;

    [Header("Attack")]
    [SerializeField] private float timeBetweenAttack;
    [SerializeField] private float timeSinceAttack;
    [SerializeField] Transform SideAttackTransform, UpAttackTransform, DownAttackTransform;
    [SerializeField] Vector2 SideAttackArea, UpAttackArea, DownAttackArea;
    [SerializeField] LayerMask attackableLayer;
    [SerializeField] private float damage;

    [Header("Recoil")]
    [SerializeField] int recoilXsteps = 5;
    [SerializeField] int recoilYsteps = 5;
    [SerializeField] float recoilXSpeed = 100;
    [SerializeField] float recoilYSpeed = 100;
    int stepsXRecoiled, stepsYRecoiled;

    [Header("Health")]
    public int Health;
    public int maxHealth;

    [HideInInspector] public PlayerStateList pState;
    private Rigidbody2D rb;
    private float xAxis, yAxis;
    private float gravity;
    Animator anim;
    private bool canDash = true;
    private bool dashed;
    private bool isGrounded;
    private bool isWalled;
    private Coroutine dashCoroutine;
    private bool wasGrounded;
    private bool wasBraking;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    private bool lastGrounded;
    private bool lastWalled;

    Vector2 respawnPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        Health = maxHealth;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        pState = GetComponent<PlayerStateList>();
        gravity = rb.gravityScale;
        respawnPoint = transform.position;
        if (col == null) col = GetComponent<Collider2D>();

        // initialize facing from local scale so dash uses correct default direction
        pState.lookingRight = transform.localScale.x >= 0f;

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy") && Health <= 0)
        {
            Reset();
        }
    }

    void Reset()
    {
        Respawn();
    }

    void Respawn()
    {
        ResetDashState();
        transform.position = respawnPoint;
    }

    private void ResetDashState()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
        pState.dashing = false;
        canDash = true;
        dashed = false;
        rb.gravityScale = gravity;
        rb.linearVelocity = Vector2.zero;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(SideAttackTransform.position, SideAttackArea);
        Gizmos.DrawWireCube(UpAttackTransform.position, UpAttackArea);
        Gizmos.DrawWireCube(DownAttackTransform.position, DownAttackArea);

        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Vector3 center = groundCheckPoint.position;
            Vector3 right = center + new Vector3(groundCheckX, 0, 0);
            Vector3 left = center + new Vector3(-groundCheckX, 0, 0);

            Gizmos.DrawRay(center, Vector3.down * groundCheckY);
            Gizmos.DrawRay(right, Vector3.down * groundCheckY);
            Gizmos.DrawRay(left, Vector3.down * groundCheckY);

            Gizmos.DrawWireSphere(center + Vector3.down * groundCheckY, 0.03f);
            Gizmos.DrawWireSphere(right + Vector3.down * groundCheckY, 0.03f);
            Gizmos.DrawWireSphere(left + Vector3.down * groundCheckY, 0.03f);
        }

        Collider2D wallCol = col != null ? col : GetComponent<Collider2D>();
        if (wallCol != null)
        {
            Gizmos.color = Color.cyan;
            Bounds b = wallCol.bounds;
            Vector3 rightStart = new Vector3(b.max.x, b.center.y, 0);
            Vector3 leftStart = new Vector3(b.min.x, b.center.y, 0);

            Gizmos.DrawRay(rightStart, Vector3.right * wallCheckRadius);
            Gizmos.DrawRay(leftStart, Vector3.left * wallCheckRadius);
        }
    }

    private void Update()
    {
        isGrounded = Grounded();

        if (wasGrounded && !isGrounded && rb.linearVelocity.y <= 0f && !Input.GetButtonDown("Jump"))
        {
            preservingMomentum = true;
            momentumTimer = Mathf.Max(momentumTimer, ledgeBuffer);
        }
        wasGrounded = isGrounded;

        isWalled = IsWalled();
        TraceStates();

        GetInputs();
        UpdateJumpVariables();

        if (pState.dashing)
        {
            if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.X))
            {
                CancelDash();
            }
            else
            {
                return;
            }
        }

        WallSlide();
        if (isWalled && !isWallJumping)
            WallFlip();

        bool wallJumped = WallJump();

        if (!isWallJumping)
            Move();

        if (!wallJumped && !isWallJumping)
            Jump();

        StartDash();
        Attack();

        if (!isWallJumping && !isWalled && !IsNearWallForFlip())
            Flip();
    }

    private void GetInputs()
    {
        xAxis = 0f;
        yAxis = 0f;

        // keyboard input
        if (Keyboard.current != null)
        {
            float left = (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) ? -1f : 0f;
            float right = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) ? 1f : 0f;
            xAxis = left + right;

            float down = (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) ? -1f : 0f;
            float up = (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) ? 1f : 0f;
            yAxis = down + up;
        }

        // gamepad overrides keyboard if present
        if (Gamepad.current != null)
        {
            var stick = Gamepad.current.leftStick.ReadValue();
            xAxis = stick.x;
            yAxis = stick.y;
        }
    }

    private void Move()
    {
        if (preservingMomentum)
        {
            momentumTimer -= Time.deltaTime;
            if (momentumTimer <= 0f)
                preservingMomentum = false;
        }

        float currentX = rb.linearVelocity.x;

        if (!Mathf.Approximately(xAxis, 0f))
        {
            if (preservingMomentum && Mathf.Abs(currentX) > speed && Mathf.Sign(currentX) == Mathf.Sign(xAxis))
            {
                float slowDown = isGrounded ? stopSpeed : deceleration;
                currentX = Mathf.MoveTowards(currentX, xAxis * speed, slowDown * 0.25f * Time.deltaTime);
            }
            else
            {
                currentX = xAxis * speed;
            }
        }
        else if (!preservingMomentum)
        {
            float slowDown = isGrounded ? stopSpeed : deceleration;
            currentX = Mathf.MoveTowards(currentX, 0f, slowDown * Time.deltaTime);
        }

        bool brakeHeld = yAxis < -0.5f;
        bool brakePressed = brakeHeld && !wasBraking;

        if (brakePressed)
        {
            preservingMomentum = false;
            brakeTimer = 0f;
            currentX = Mathf.MoveTowards(currentX, 0f, tapBrake);
        }

        if (brakeHeld)
        {
            brakeTimer += Time.deltaTime;
            float ramp = Mathf.Clamp01(brakeTimer / Mathf.Max(brakeRamp, 0.01f));
            float brakeDeceleration = Mathf.Lerp(brakeStart, brakeMax, ramp);
            currentX = Mathf.MoveTowards(currentX, 0f, brakeDeceleration * Time.deltaTime);
        }
        else
        {
            brakeTimer = 0f;
        }

        wasBraking = brakeHeld;

        float currentY = rb.linearVelocity.y;
        if (isGrounded && currentY <= 0f)
            currentY = -groundStickSpeed;

        rb.linearVelocity = new Vector2(currentX, currentY);
        anim.SetBool("Walking", !Mathf.Approximately(rb.linearVelocity.x, 0f) && isGrounded);
    }

    private void Flip()
    {
        if (Mathf.Approximately(xAxis, 0f))
            return;

        pState.lookingRight = xAxis > 0f;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (pState.lookingRight ? 1f : -1f);
        transform.localScale = scale;
        if (pState.recoilingX || pState.recoilingY)
        {
            Recoil();
            return;
        }
    }

    private void StartDash()
    {
        // prevent dashing immediately after a wall jump so the wall-jump feels rigid
        if (isWallJumping)
            return;
        if (isGrounded)
            dashed = false;
        if (Time.timeSinceLevelLoad < 0.1f)
            return;

        if (Input.GetButtonDown("Dash") && canDash && !dashed)
        {
            dashed = true;
            if (dashCoroutine != null)
                StopCoroutine(dashCoroutine);
            dashCoroutine = StartCoroutine(Dash());
        }
    }

    private Vector2 GetInitialDashDirection()
    {
        Vector2 input = new Vector2(xAxis, yAxis);
        if (input.sqrMagnitude < 0.01f)
            return pState.lookingRight ? Vector2.right : Vector2.left;
        return input.normalized;
    }

    private void CancelDash()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        pState.dashing = false;
        rb.gravityScale = gravity;
        // use a bool parameter for dashing so the animation isn't canceled unexpectedly by triggers
        anim.SetBool("Dashing", false);

        preservingMomentum = true;
        momentumTimer = dashMomentumDecayTime;
    }

    IEnumerator Dash()
    {
        canDash = false;
        pState.dashing = true;
        isWallSliding = false;
        isWallJumping = false;

        // set dashing state on animator
        anim.SetBool("Dashing", true);

        Vector2 currentDashDir = GetInitialDashDirection();

        if (Mathf.Abs(currentDashDir.x) > 0.01f)
        {
            pState.lookingRight = currentDashDir.x > 0f;
            Vector3 localScale = transform.localScale;
            localScale.x = Mathf.Abs(localScale.x) * (pState.lookingRight ? 1f : -1f);
            transform.localScale = localScale;
        }

        float dashDuration = Mathf.Max(Time.fixedDeltaTime, dashDistance / Mathf.Max(Mathf.Abs(dashSpeed), 0.01f));
        float elapsed = 0f;
        rb.gravityScale = 0f;

        while (elapsed < dashDuration)
        {
            Vector2 inputDir = new Vector2(xAxis, yAxis);

            if (inputDir.sqrMagnitude > 0.05f)
            {
                float radiansToRotate = dashSteerSpeed * Time.fixedDeltaTime;
                currentDashDir = Vector3.RotateTowards(currentDashDir, inputDir.normalized, radiansToRotate, 0.0f).normalized;
            }

            if (isGrounded && currentDashDir.y < -0.1f)
            {
                float dashDirX = Mathf.Abs(currentDashDir.x) > 0.1f ? Mathf.Sign(currentDashDir.x) : (pState.lookingRight ? 1f : -1f);
                rb.linearVelocity = new Vector2(dashDirX * dashSpeed, 0f);
                CancelDash();
                yield return new WaitForSeconds(dashCooldown);
                canDash = true;
                yield break;
            }

            rb.linearVelocity = currentDashDir * dashSpeed;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.gravityScale = gravity;

        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(currentDashDir.x * dashSpeed, 0f);
        }
        else
        {
            rb.linearVelocity = currentDashDir * (dashSpeed * 0.5f);
        }

        pState.dashing = false;
        // ensure animator reflects dash end
        anim.SetBool("Dashing", false);
        preservingMomentum = true;
        momentumTimer = dashMomentumDecayTime;

        yield return new WaitForSeconds(dashCooldown);

        canDash = true;
        dashCoroutine = null;
    }

    private void Attack()
    {
        timeSinceAttack += Time.deltaTime;
        if (!Input.GetKeyDown(KeyCode.X) || timeSinceAttack < timeBetweenAttack)
            return;

        timeSinceAttack = 0f;
        anim.SetTrigger("Attacking");

        if (isGrounded || Mathf.Approximately(yAxis, 0f))
            Hit(SideAttackTransform, SideAttackArea, ref pState.recoilingX, recoilXSpeed);
        else if (yAxis > 0f)
            Hit(UpAttackTransform, UpAttackArea, ref pState.recoilingY, recoilYSpeed);
        else
            Hit(DownAttackTransform, DownAttackArea, ref pState.recoilingY, recoilYSpeed);
    }

    private void Hit(Transform _attackTransform, Vector2 _AttackArea, ref bool _recoilDir, float _recoilStrength)
    {
        Collider2D[] objectsToHit = Physics2D.OverlapBoxAll(_attackTransform.position, _AttackArea, 0, attackableLayer);

        if (objectsToHit.Length > 0)
        {
            _recoilDir = true;
        }
        for (int i = 0; i < objectsToHit.Length; i++)
        {
            Enemy enemy = objectsToHit[i].GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyHit(damage, (transform.position - objectsToHit[i].transform.position).normalized, _recoilStrength);
            }
        }
    }

    void Recoil()
    {
        if (pState.recoilingX)
        {
            if (pState.lookingRight)
            {
                rb.linearVelocity = new Vector2(-recoilXSpeed, 0);
            }
            else
            {
                rb.linearVelocity = new Vector2(-recoilXSpeed, 0);
            }
        }

        if (pState.recoilingY)
        {
            rb.gravityScale = 0;
            if (yAxis < 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, recoilYSpeed);
            }
            else
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -recoilYSpeed);
            }
            airJumpCounter = 0;
        }
        else
        {
            rb.gravityScale = gravity;
        }

        if (pState.recoilingX && stepsXRecoiled < recoilXsteps)
        {
            stepsXRecoiled++;
        }
        else
        {
            StopRecoilX();
        }

        if (pState.recoilingY && stepsYRecoiled < recoilYsteps)
        {
            stepsYRecoiled++;
        }
        else
        {
            StopRecoilY();
        }

        if (isGrounded)
        {
            StopRecoilY();
        }
    }

    void StopRecoilX()
    {
        stepsXRecoiled = 0;
        pState.recoilingX = false;
    }

    void StopRecoilY()
    {
        stepsYRecoiled = 0;
        pState.recoilingY = false;
    }

    public void TakeDamage(float _damage)
    {
        Health -= Mathf.RoundToInt(_damage);
        StartCoroutine(StopTakingDamage());
    }

    IEnumerator StopTakingDamage()
    {
        pState.invincible = true;
        HP();
        yield return new WaitForSeconds(1f);
        pState.invincible = false;
    }

    void HP()
    {
        Health = Mathf.Clamp(Health, 0, maxHealth);
    }

    private bool wallOnRight;

    private bool IsWalled()
    {
        Bounds b = col.bounds;
        Vector2 rightOrigin = new Vector2(b.max.x, b.center.y);
        Vector2 leftOrigin = new Vector2(b.min.x, b.center.y);

        bool rightHit = Physics2D.Raycast(rightOrigin, Vector2.right, wallCheckRadius, wallLayer);
        bool leftHit = Physics2D.Raycast(leftOrigin, Vector2.left, wallCheckRadius, wallLayer);

        if (debugMode)
        {
            Debug.DrawRay(rightOrigin, Vector2.right * wallCheckRadius, rightHit ? Color.green : Color.red);
            Debug.DrawRay(leftOrigin, Vector2.left * wallCheckRadius, leftHit ? Color.green : Color.red);
        }

        if (rightHit) { wallOnRight = true; return true; }
        if (leftHit) { wallOnRight = false; return true; }
        return false;
    }

    private void TraceStates()
    {
        if (!debugMode)
            return;

        if (isGrounded != lastGrounded)
            Debug.Log($"{name}: grounded = {isGrounded}");

        if (isWalled != lastWalled)
            Debug.Log($"{name}: wall = {isWalled}, right = {wallOnRight}");

        lastGrounded = isGrounded;
        lastWalled = isWalled;
    }

    private bool IsNearWallForFlip()
    {
        Bounds b = col.bounds;

        bool rightHit = Physics2D.Raycast(new Vector2(b.max.x, b.center.y), Vector2.right, flipWallBuffer, wallLayer);
        bool leftHit = Physics2D.Raycast(new Vector2(b.min.x, b.center.y), Vector2.left, flipWallBuffer, wallLayer);

        return rightHit || leftHit;
    }

    private void WallFlip()
    {
        pState.lookingRight = wallOnRight;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (wallOnRight ? 1f : -1f);
        transform.localScale = scale;
    }

    private void WallSlide()
    {
        if (isWallJumping)
        {
            isWallSliding = false;
            return;
        }

        bool wantsToSlide = yAxis < -0.5f;

        bool intoWall = wallOnRight ? xAxis > 0f : xAxis < 0f;
        if (!isWalled || isGrounded || !intoWall)
        {
            isWallSliding = false;
            return;
        }

        if (!wantsToSlide)
        {
            isWallSliding = false;
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(0f, -0.5f);
        }
        else
        {
            isWallSliding = true;
            rb.gravityScale = wallSlideGravityScale;
            rb.linearVelocity = new Vector2(0f, Mathf.Max(rb.linearVelocity.y, -wallSlidingSpeed));
        }
    }

    private bool WallJump()
    {
        if (isWalled && !isGrounded && !isWallJumping)
        {
            wallJumpingCounter = wallJumpingTime;
        }
        else if (!isWallJumping)
        {
            wallJumpingCounter -= Time.deltaTime;
        }

        if (!Input.GetButtonDown("Jump") || wallJumpingCounter <= 0f)
            return false;

        float direction = wallOnRight ? -1f : 1f;
        isWallJumping = true;
        rb.gravityScale = gravity;
        // set a fixed velocity for the wall-jump so it's rigid and consistent
        rb.linearVelocity = IsNearTop()
            ? new Vector2(0f, wallJumpingPower.y)
            : new Vector2(direction * wallJumpingPower.x, wallJumpingPower.y);
        wallJumpingCounter = 0f;
        // disable momentum preservation to keep the wall-jump trajectory rigid
        preservingMomentum = false;
        momentumTimer = 0f;
        pState.lookingRight = direction > 0f;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;

        CancelInvoke(nameof(StopWallJumping));
        Invoke(nameof(StopWallJumping), wallJumpingDuration);
        return true;
    }

    private void StopWallJumping()
    {
        isWallJumping = false;
    }

    public bool Grounded()
    {
        return Physics2D.Raycast(groundCheckPoint.position, Vector2.down, groundCheckY, whatIsGround)
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround)
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(-groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround);
    }

    private void Jump()
    {
        if (!pState.jumping && jumpBufferCounter > 0 && coyoteTimeCounter > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0;
            coyoteTimeCounter = 0;
            preservingMomentum = true;
            momentumTimer = Mathf.Max(momentumTimer, jumpMomentumDecayTime);
        }
        else if (!isGrounded && airJumpCounter < maxAirJumps && Input.GetButtonDown("Jump"))
        {
            pState.jumping = true;
            airJumpCounter++;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            preservingMomentum = true;
            momentumTimer = Mathf.Max(momentumTimer, jumpMomentumDecayTime);
        }

        anim.SetBool("Jumping", !isGrounded);
    }

    private bool IsNearTop()
    {
        Bounds b = col.bounds;
        float xPos = wallOnRight ? b.max.x : b.min.x;
        Vector2 origin = new Vector2(xPos, b.max.y);
        return !Physics2D.Raycast(origin, Vector2.up, ledgeCheckDistance, wallLayer);
    }

    private void UpdateJumpVariables()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            pState.jumping = false;
            airJumpCounter = 0;
            isWallJumping = false;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (!isWallSliding && !pState.dashing)
        {
            if (yAxis < -0.5f && !isGrounded)
            {
                rb.gravityScale = gravityScale * 10;
            }
            else
            {
                rb.gravityScale = rb.linearVelocity.y > 0 ? ascendGravityScale : fallGravityScale;
            }
        }
    }
}