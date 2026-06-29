// =============================================================================
//  PlayerController3D.cs
//  Project : Platformer Prototype
//
//  PURPOSE
//  -------
//  Controlador principal del jugador basado en Físicas (Rigidbody) para un 
//  plataformero 3D. Maneja la locomoción relativa a la cámara, detección de 
//  entorno (suelo/paredes), salto variable, wall jump y dash.
//
//  ARCHITECTURE NOTES
//  ------------------
//  • Estricto principio de encapsulamiento: variables serializadas privadas.
//  • Integración directa con el New Input System (Send Messages).
//  • Expone propiedades de solo lectura (IsGrounded, IsMoving, etc.) para que 
//    otros scripts (como PlayerDeathHandler o PlayerAnimator) puedan leer el 
//    estado sin modificar las físicas.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace Platformer.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerController3D : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED PARAMETERS  (private + [SerializeField])
        // ─────────────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Transform de la cámara principal para calcular el movimiento relativo.")]
        [SerializeField] private Transform _playerCamera;

        [Tooltip("Referencia al Animator del modelo (opcional).")]
        [SerializeField] private Animator _animator;

        [Header("Movement")]
        [SerializeField] private float _maxSpeed = 8f;
        [SerializeField] private float _acceleration = 60f;
        [SerializeField] private float _deceleration = 60f;
        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Jump Mechanics")]
        [SerializeField] private float _jumpForce = 12f;
        [SerializeField] private float _fallMultiplier = 2.5f;
        [SerializeField] private float _lowJumpMultiplier = 2f;
        [SerializeField] private int _maxJumps = 2;

        [Header("Wall Jump & Slide")]
        [SerializeField] private float _wallJumpUpForce = 10f;
        [SerializeField] private float _wallJumpSideForce = 12f;
        [SerializeField] private float _wallSlideSpeed = 2f;

        [Header("Dash / Roll")]
        [SerializeField] private float _dashForce = 25f;
        [SerializeField] private float _dashDuration = 0.2f;
        [SerializeField] private float _dashCooldown = 1f;

        [Header("Environment Detection")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private LayerMask _wallLayer;
        [SerializeField] private float _feetOffset = -1f;
        [SerializeField] private float _groundCheckRadius = 0.4f;
        [SerializeField] private float _groundCheckDistance = 0.1f;
        [SerializeField] private float _maxSlopeAngle = 45f;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        private Rigidbody _rb;

        // Inputs
        private Vector2 _moveInput;
        private bool _jumpPressed;
        private bool _jumpHeld;
        private bool _dashPressed;

        // Environment State
        private bool _isGrounded;
        private bool _isTouchingWall;
        private Vector3 _wallNormal;
        private RaycastHit _slopeHit;

        // Action State
        private int _jumpsRemaining;
        private bool _isDashing;
        private float _dashEndTime;
        private float _lastDashTime = -100f;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY PROPERTIES (Para compatibilidad con otros scripts)
        // ─────────────────────────────────────────────────────────────────────
        public bool IsGrounded => _isGrounded;
        public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;
        public bool IsDashing => _isDashing;
        public bool IsSprinting => false; // Stub para evitar errores si algo lo llama

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (_playerCamera == null && Camera.main != null)
            {
                _playerCamera = Camera.main.transform;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnDisable()
        {
            // Seguridad: Si el script se apaga (ej: al morir), frena las físicas
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
        }

        private void Update()
        {
            CheckGroundAndWalls();
            HandleJumpAndDashLogic();
            UpdateAnimations();
        }

        private void FixedUpdate()
        {
            if (_isDashing) return;

            Move();
            ApplyCustomGravity();
            HandleWallSlide();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NEW INPUT SYSTEM MESSAGE CALLBACKS
        // ─────────────────────────────────────────────────────────────────────

        private void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        private void OnJump(InputValue value)
        {
            _jumpHeld = value.isPressed;
            if (value.isPressed) _jumpPressed = true;
        }

        private void OnDash(InputValue value)
        {
            if (value.isPressed) _dashPressed = true;
        }

        // Stubs para evitar warnings de Unity al usar "Send Messages"
        private void OnLook(InputValue value) { }
        private void OnAttack(InputValue value) { }
        private void OnInteract(InputValue value) { }
        private void OnConsume(InputValue value) { }
        private void OnSprint(InputValue value) { }

        // ─────────────────────────────────────────────────────────────────────
        //  ACTION LOGIC
        // ─────────────────────────────────────────────────────────────────────

        private void HandleJumpAndDashLogic()
        {
            // Manejo del fin del Dash
            if (_isDashing && Time.time >= _dashEndTime)
            {
                EndDash();
            }

            // Inicio del Dash
            if (_dashPressed && Time.time >= _lastDashTime + _dashCooldown && _moveInput.sqrMagnitude > 0)
            {
                StartDash();
                _dashPressed = false;
            }

            // Manejo del Salto
            if (_jumpPressed)
            {
                _jumpPressed = false;

                if (_isGrounded || _jumpsRemaining > 0)
                {
                    ExecuteJump();
                }
                else if (_isTouchingWall && !_isGrounded)
                {
                    ExecuteWallJump();
                }
            }
        }

        private void ExecuteJump()
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _jumpsRemaining--;

            if (_animator != null)
            {
                Vector3 currentHorizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                _animator.SetTrigger(currentHorizontalVel.magnitude > 1f || IsMoving ? "JumpRun" : "Jump");
            }
        }

        private void ExecuteWallJump()
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            Vector3 jumpVector = (_wallNormal * _wallJumpSideForce) + (Vector3.up * _wallJumpUpForce);
            _rb.AddForce(jumpVector, ForceMode.Impulse);
            transform.forward = _wallNormal;

            if (_animator != null) _animator.SetTrigger("Jump");
        }

        private void StartDash()
        {
            _isDashing = true;
            _dashEndTime = Time.time + _dashDuration;
            _lastDashTime = Time.time;

            _rb.useGravity = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.AddForce(transform.forward * _dashForce, ForceMode.VelocityChange);

            if (_animator != null) _animator.SetTrigger("Roll");
        }

        private void EndDash()
        {
            _isDashing = false;
            _rb.useGravity = true;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PHYSICS MOVEMENT
        // ─────────────────────────────────────────────────────────────────────

        private void Move()
        {
            if (_playerCamera == null) return;

            Vector3 camForward = _playerCamera.forward;
            Vector3 camRight = _playerCamera.right;
            camForward.y = 0f; camRight.y = 0f;
            camForward.Normalize(); camRight.Normalize();

            Vector3 moveDirection = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;

            if (_isGrounded && OnSlope())
            {
                moveDirection = Vector3.ProjectOnPlane(moveDirection, _slopeHit.normal).normalized;
            }

            Vector3 currentHorizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            Vector3 targetVelocity = moveDirection * _maxSpeed;
            Vector3 velocityDiff = targetVelocity - currentHorizontalVel;

            float accelRate = IsMoving ? _acceleration : _deceleration;
            _rb.AddForce(velocityDiff * accelRate, ForceMode.Acceleration);

            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveDirection.x, 0f, moveDirection.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            }
        }

        private void ApplyCustomGravity()
        {
            if (_rb.linearVelocity.y < 0)
            {
                _rb.linearVelocity += Vector3.up * Physics.gravity.y * (_fallMultiplier - 1) * Time.fixedDeltaTime;
            }
            else if (_rb.linearVelocity.y > 0 && !_jumpHeld)
            {
                _rb.linearVelocity += Vector3.up * Physics.gravity.y * (_lowJumpMultiplier - 1) * Time.fixedDeltaTime;
            }
        }

        private void HandleWallSlide()
        {
            if (_isTouchingWall && !_isGrounded && _rb.linearVelocity.y < 0 && IsMoving)
            {
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, -_wallSlideSpeed, _rb.linearVelocity.z);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ENVIRONMENT DETECTION
        // ─────────────────────────────────────────────────────────────────────

        private void CheckGroundAndWalls()
        {
            Vector3 feetPos = transform.position + Vector3.up * _feetOffset;
            Vector3 checkOrigin = feetPos + Vector3.up * (_groundCheckRadius + 0.05f);

            _isGrounded = Physics.SphereCast(checkOrigin, _groundCheckRadius, Vector3.down, out RaycastHit groundHit, _groundCheckDistance, _groundLayer);

            if (_isGrounded)
            {
                _jumpsRemaining = _maxJumps;
                if (Vector3.Angle(Vector3.up, groundHit.normal) > _maxSlopeAngle) _isGrounded = false;
            }

            Vector3 wallCheckDir = transform.forward;
            if (IsMoving && _playerCamera != null)
            {
                Vector3 camF = _playerCamera.forward; Vector3 camR = _playerCamera.right;
                camF.y = 0; camR.y = 0;
                wallCheckDir = (camF.normalized * _moveInput.y + camR.normalized * _moveInput.x).normalized;
            }

            Vector3 startWallRay = transform.position + Vector3.up * 0.5f;
            _isTouchingWall = Physics.Raycast(startWallRay, wallCheckDir, out RaycastHit wallHit, _groundCheckRadius + 0.3f, _wallLayer);

            if (_isTouchingWall) _wallNormal = wallHit.normal;
        }

        private bool OnSlope()
        {
            Vector3 feetPos = transform.position + Vector3.up * _feetOffset;
            if (Physics.Raycast(feetPos + Vector3.up * 0.1f, Vector3.down, out _slopeHit, 0.3f, _groundLayer))
            {
                float angle = Vector3.Angle(Vector3.up, _slopeHit.normal);
                return angle < _maxSlopeAngle && angle != 0;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ANIMATION INTEGRATION
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateAnimations()
        {
            if (_animator == null) return;

            Vector3 horizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            float normalizedSpeed = Mathf.Clamp01(horizontalVel.magnitude / _maxSpeed);

            float currentAnimSpeed = _animator.GetFloat("Speed");
            _animator.SetFloat("Speed", Mathf.MoveTowards(currentAnimSpeed, normalizedSpeed, Time.deltaTime * 8f));

            _animator.SetBool("IsGrounded", _isGrounded);
            _animator.SetBool("IsFalling", !_isGrounded && _rb.linearVelocity.y < -0.5f && !_isDashing);
            _animator.SetBool("IsWallSliding", _isTouchingWall && !_isGrounded && _rb.linearVelocity.y < 0f && !_isDashing);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 checkOrigin = transform.position + Vector3.up * (_feetOffset + _groundCheckRadius + 0.05f);
            Gizmos.DrawWireSphere(checkOrigin - Vector3.up * _groundCheckDistance, _groundCheckRadius);
        }
#endif

        // ─────────────────────────────────────────────────────────────────────
        //  POWER-UP HELPERS  (called by PlayerCollectibles via Coroutine)
        //
        //  Design note: the original values are cached the first time Apply is
        //  called, so if the player grabs the same power-up twice while it is
        //  still active the Coroutine simply restarts and Revert still knows
        //  what the true baseline value is.
        // ─────────────────────────────────────────────────────────────────────

        // ── Speed Boost ───────────────────────────────────────────────────────

        private float _baseMaxSpeed = -1f;   // -1 signals "not cached yet"

        /// <summary>Multiplies _maxSpeed by the given factor. Safe to call repeatedly.</summary>
        public void ApplySpeedBoost(float multiplier)
        {
            // Only cache the real base value once so re-activating doesn't compound
            if (_baseMaxSpeed < 0f) _baseMaxSpeed = _maxSpeed;
            _maxSpeed = _baseMaxSpeed * multiplier;
        }

        /// <summary>Restores _maxSpeed to its original Inspector value.</summary>
        public void RevertSpeedBoost()
        {
            if (_baseMaxSpeed >= 0f)
            {
                _maxSpeed    = _baseMaxSpeed;
                _baseMaxSpeed = -1f;         // reset cache flag
            }
        }

        // ── Jump Boost ────────────────────────────────────────────────────────

        private float _baseJumpForce = -1f;  // -1 signals "not cached yet"

        /// <summary>Multiplies _jumpForce by the given factor. Safe to call repeatedly.</summary>
        public void ApplyJumpBoost(float multiplier)
        {
            if (_baseJumpForce < 0f) _baseJumpForce = _jumpForce;
            _jumpForce = _baseJumpForce * multiplier;
        }

        /// <summary>Restores _jumpForce to its original Inspector value.</summary>
        public void RevertJumpBoost()
        {
            if (_baseJumpForce >= 0f)
            {
                _jumpForce    = _baseJumpForce;
                _baseJumpForce = -1f;
            }
        }
    }
}