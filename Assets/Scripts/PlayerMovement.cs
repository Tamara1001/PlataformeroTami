using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Main Camera transform for relative movement.")]
    public Transform playerCamera;
    private Rigidbody rb;

    [Header("Movement")]
    public float maxSpeed = 8f;
    public float acceleration = 60f;
    public float deceleration = 60f;
    public float rotationSpeed = 15f;

    [Header("Jump mechanics")]
    public float jumpForce = 12f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public int maxJumps = 2;
    private int jumpsRemaining;

    [Header("Wall Jump")]
    public float wallJumpUpForce = 10f;
    public float wallJumpSideForce = 12f;
    public float wallSlideSpeed = 2f;
    private bool isTouchingWall;
    private Vector3 wallNormal;

    [Header("Dash")]
    public float dashForce = 25f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool isDashing;
    private float dashEndTime;
    private float lastDashTime = -100f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public LayerMask wallLayer;
    public float groundCheckRadius = 0.4f;
    public float groundCheckDistance = 0.1f;
    public float maxSlopeAngle = 45f;
    private bool isGrounded;
    private RaycastHit slopeHit;

    // Inputs
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool dashPressed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Ensure Rigidbody respects rotation lock and interpolate for smooth movement
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    private void Update()
    {
        GatherInputs();
        CheckGroundAndWalls();
        HandleJumpAndDashInputs();
    }

    private void FixedUpdate()
    {
        // Don't apply standard physical movement constraints when dashing
        if (isDashing) return;

        Move();
        ApplyCustomGravity();
        HandleWallSlide();
    }

    #region Input Handling
    
    // Abstracting out inputs makes it easy to replace with the New Input System later
    private void GatherInputs()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        
        jumpHeld = Input.GetButton("Jump");
        // We capture button downs in Update, and consume them in fixed/logic steps to ensure no missed events.
        if (Input.GetButtonDown("Jump")) jumpPressed = true;
        if (Input.GetKeyDown(KeyCode.LeftShift)) dashPressed = true;
    }

    private void HandleJumpAndDashInputs()
    {
        // Dash State handling
        if (isDashing && Time.time >= dashEndTime)
        {
            EndDash();
        }

        if (dashPressed && Time.time >= lastDashTime + dashCooldown && moveInput.sqrMagnitude > 0)
        {
            StartDash();
            dashPressed = false;
        }

        // Jump Handling
        if (jumpPressed)
        {
            jumpPressed = false;
            
            if (isGrounded || jumpsRemaining > 0)
            {
                ExecuteJump();
            }
            else if (isTouchingWall && !isGrounded)
            {
                ExecuteWallJump();
            }
        }
    }

    #endregion

    #region Physics Movement

    private void Move()
    {
        // 1. Calculate camera-relative forward and right
        Vector3 camForward = playerCamera.forward;
        Vector3 camRight = playerCamera.right;
        
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // 2. Adjust for slopes (so we maintain speed when going up/down)
        if (isGrounded && OnSlope())
        {
            moveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
        }

        // 3. Acceleration and Deceleration logic targeting maxSpeed
        // Using rb.linearVelocity (Unity 6+) vs older rb.velocity
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 targetVelocity = moveDirection * maxSpeed;

        Vector3 velocityDiff = targetVelocity - currentHorizontalVel;
        
        float accelRate = (moveInput.sqrMagnitude > 0) ? acceleration : deceleration;
        Vector3 appliedForce = velocityDiff * accelRate;
        
        rb.AddForce(appliedForce, ForceMode.Acceleration);

        // 4. Character Rotation
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveDirection.x, 0f, moveDirection.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void ApplyCustomGravity()
    {
        // Variable Jump Base multiplier (Fast falling like Mario)
        if (rb.linearVelocity.y < 0)
        {
            // Falling down backwards/naturally faster
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            // Released jump early -> cut velocity by falling much faster
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private void HandleWallSlide()
    {
        if (isTouchingWall && !isGrounded && rb.linearVelocity.y < 0 && moveInput.sqrMagnitude > 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -wallSlideSpeed, rb.linearVelocity.z);
        }
    }

    #endregion

    #region Actions

    private void ExecuteJump()
    {
        // Reset Y velocity to keep jump height consistent regardless of falling velocity
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpsRemaining--;
    }

    private void ExecuteWallJump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        
        Vector3 jumpVector = (wallNormal * wallJumpSideForce) + (Vector3.up * wallJumpUpForce);
        rb.AddForce(jumpVector, ForceMode.Impulse);

        // Immediately face away from the physical wall
        transform.forward = wallNormal;
    }

    private void StartDash()
    {
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        lastDashTime = Time.time;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        // Dash strictly in the geometry direction the player is currently facing
        Vector3 dashDir = transform.forward;
        rb.AddForce(dashDir * dashForce, ForceMode.VelocityChange);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.useGravity = true;
        // Kill massive sideways momentum to prevent sliding forever
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    #endregion

    #region Environment Detection

    private void CheckGroundAndWalls()
    {
        // 1. SphereCast for accurate volume-based ground detection
        Vector3 checkOrigin = transform.position + Vector3.up * (groundCheckRadius + 0.05f);
        
        isGrounded = Physics.SphereCast(checkOrigin, groundCheckRadius, Vector3.down, out RaycastHit groundHit, groundCheckDistance, groundLayer);

        if (isGrounded)
        {
            jumpsRemaining = maxJumps;
            
            // Validate the angle isn't too steep to stand on
            float angle = Vector3.Angle(Vector3.up, groundHit.normal);
            if (angle > maxSlopeAngle)
            {
                isGrounded = false;
            }
        }

        // 2. Line Raycast for Walls
        Vector3 wallCheckDir = transform.forward;
        
        // Contextually predict wall intersection toward the movement side
        if (moveInput.sqrMagnitude > 0)
        {
            Vector3 camForward = playerCamera.forward;
            Vector3 camRight = playerCamera.right;
            camForward.y = 0; camRight.y = 0;
            wallCheckDir = (camForward.normalized * moveInput.y + camRight.normalized * moveInput.x).normalized;
        }

        Vector3 startWallRay = transform.position + Vector3.up * 0.5f;
        isTouchingWall = Physics.Raycast(startWallRay, wallCheckDir, out RaycastHit wallHit, groundCheckRadius + 0.3f, wallLayer);
        
        if (isTouchingWall)
        {
            wallNormal = wallHit.normal;
        }
    }

    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out slopeHit, groundCheckRadius + 0.5f, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Visualization inside Unity Scene View
        Gizmos.color = Color.green;
        Vector3 checkOrigin = transform.position + Vector3.up * (groundCheckRadius + 0.05f);
        Gizmos.DrawWireSphere(checkOrigin - Vector3.up * groundCheckDistance, groundCheckRadius);
    }
}
