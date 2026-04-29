using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Main Camera transform for relative movement.")]
    public Transform playerCamera;
    [Tooltip("Reference to the player's Animator component.")]
    public Animator animator;
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

    [Header("Dash / Roll")]
    public float dashForce = 25f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool isDashing;
    private float dashEndTime;
    private float lastDashTime = -100f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public LayerMask wallLayer;
    [Tooltip("Offset to the character's feet. -1 represents the bottom of a default Unity Capsule of height 2.")]
    public float feetOffset = -1f;
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
        
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        // Auto-assign animator if it's on the same object or a child model
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        GatherInputs();
        CheckGroundAndWalls();
        HandleJumpAndDashInputs();
        UpdateAnimations(); // Added Animation Logic
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        Move();
        ApplyCustomGravity();
        HandleWallSlide();
    }

    #region Input Handling
    
    private void GatherInputs()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        
        jumpHeld = Input.GetButton("Jump");
        if (Input.GetButtonDown("Jump")) jumpPressed = true;
        if (Input.GetKeyDown(KeyCode.LeftShift)) dashPressed = true;
    }

    private void HandleJumpAndDashInputs()
    {
        if (isDashing && Time.time >= dashEndTime)
        {
            EndDash();
        }

        if (dashPressed && Time.time >= lastDashTime + dashCooldown && moveInput.sqrMagnitude > 0)
        {
            StartDash();
            dashPressed = false;
        }

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
        Vector3 camForward = playerCamera.forward;
        Vector3 camRight = playerCamera.right;
        
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        if (isGrounded && OnSlope())
        {
            moveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
        }

        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 targetVelocity = moveDirection * maxSpeed;

        Vector3 velocityDiff = targetVelocity - currentHorizontalVel;
        
        float accelRate = (moveInput.sqrMagnitude > 0) ? acceleration : deceleration;
        Vector3 appliedForce = velocityDiff * accelRate;
        
        rb.AddForce(appliedForce, ForceMode.Acceleration);

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveDirection.x, 0f, moveDirection.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void ApplyCustomGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
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
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpsRemaining--;

        // ANIMATION: Trigger either Jump or JumpRun
        if (animator != null)
        {
            Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (currentHorizontalVel.magnitude > 1f || moveInput.sqrMagnitude > 0)
            {
                animator.SetTrigger("JumpRun");
            }
            else
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    private void ExecuteWallJump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        
        Vector3 jumpVector = (wallNormal * wallJumpSideForce) + (Vector3.up * wallJumpUpForce);
        rb.AddForce(jumpVector, ForceMode.Impulse);

        transform.forward = wallNormal;

        // ANIMATION: Trigger Jump for wall jumping
        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    private void StartDash()
    {
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        lastDashTime = Time.time;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        Vector3 dashDir = transform.forward;
        rb.AddForce(dashDir * dashForce, ForceMode.VelocityChange);

        // ANIMATION: Trigger Roll
        if (animator != null)
        {
            animator.SetTrigger("Roll");
        }
    }

    private void EndDash()
    {
        isDashing = false;
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    #endregion

    #region Environment Detection

    private void CheckGroundAndWalls()
    {
        Vector3 feetPos = transform.position + Vector3.up * feetOffset;
        Vector3 checkOrigin = feetPos + Vector3.up * (groundCheckRadius + 0.05f);
        
        isGrounded = Physics.SphereCast(checkOrigin, groundCheckRadius, Vector3.down, out RaycastHit groundHit, groundCheckDistance, groundLayer);

        if (isGrounded)
        {
            jumpsRemaining = maxJumps;
            
            float angle = Vector3.Angle(Vector3.up, groundHit.normal);
            if (angle > maxSlopeAngle)
            {
                isGrounded = false;
            }
        }

        Vector3 wallCheckDir = transform.forward;
        
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
        Vector3 feetPos = transform.position + Vector3.up * feetOffset;
        if (Physics.Raycast(feetPos + Vector3.up * 0.1f, Vector3.down, out slopeHit, 0.3f, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    #endregion

    #region Animation
    
    private void UpdateAnimations()
    {
        if (animator == null) return;

        // 1. Locomotion Speed
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float currentSpeed = currentHorizontalVel.magnitude;
        animator.SetFloat("Speed", currentSpeed);

        // 2. Grounded State
        animator.SetBool("IsGrounded", isGrounded);

        // 3. Falling State
        // Triggers true if not grounded and moving downwards (ignoring dash freeze)
        bool isFalling = !isGrounded && rb.linearVelocity.y < -0.1f && !isDashing;
        animator.SetBool("IsFalling", isFalling);
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 feetPos = transform.position + Vector3.up * feetOffset;
        Vector3 checkOrigin = feetPos + Vector3.up * (groundCheckRadius + 0.05f);
        Gizmos.DrawWireSphere(checkOrigin - Vector3.up * groundCheckDistance, groundCheckRadius);
    }
}
