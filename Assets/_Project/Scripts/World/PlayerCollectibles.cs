// =============================================================================
//  PlayerCollectibles.cs  (UPDATED)
//  Project : Platformer Prototype
//
//  PURPOSE
//  -------
//  Tracks persistent collectibles (keys) and manages temporary power-ups
//  via Coroutines.  Power-ups modify PlayerController3D values through dedicated
//  public helper methods exposed at the bottom of that script.
//
//  Coin counting is now centralised in GameManager so the data survives
//  scene reloads and is accessible for record-keeping without extra plumbing.
//
//  ARCHITECTURE NOTES
//  ------------------
//  • Coroutines are stored in fields so a second pick-up of the same type
//    cancels and restarts the timer instead of stacking incorrectly.
//  • The script obtains a PlayerController3D reference lazily via GetComponent
//    on Awake; if your player has multiple components, cache it instead.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace Platformer.Player
{
    public class PlayerCollectibles : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  POWER-UP CONFIGURATION
        // ─────────────────────────────────────────────────────────────────────

        [Header("Speed Boost")]
        [Tooltip("Multiplier applied to _maxSpeed while the boost is active (e.g. 1.6 = +60%).")]
        [SerializeField] private float _speedBoostMultiplier = 1.6f;

        [Tooltip("Seconds the Speed Boost lasts.")]
        [SerializeField] private float _speedBoostDuration = 5f;

        [Header("Jump Boost")]
        [Tooltip("Multiplier applied to _jumpForce while the boost is active (e.g. 1.4 = +40%).")]
        [SerializeField] private float _jumpBoostMultiplier = 1.4f;

        [Tooltip("Seconds the Jump Boost lasts.")]
        [SerializeField] private float _jumpBoostDuration = 5f;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY STATE
        // ─────────────────────────────────────────────────────────────────────

        public bool HasKey       { get; private set; }

        public bool SpeedBoosted { get; private set; }
        public bool JumpBoosted  { get; private set; }

        /// <summary>Remaining seconds on the active Speed Boost (0 if inactive).</summary>
        public float SpeedBoostTimeLeft { get; private set; }

        /// <summary>Remaining seconds on the active Jump Boost (0 if inactive).</summary>
        public float JumpBoostTimeLeft  { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        private PlayerController3D _controller;
        private Coroutine _speedBoostCoroutine;
        private Coroutine _jumpBoostCoroutine;

        // ─────────────────────────────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _controller = GetComponent<PlayerController3D>();

            if (_controller == null)
            {
                Debug.LogError("[PlayerCollectibles] PlayerController3D not found on this " +
                               "GameObject. Power-ups will not work.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PERSISTENT COLLECTIBLES
        // ─────────────────────────────────────────────────────────────────────

        public void AddKey()
        {
            HasKey = true;
            Debug.Log("[PlayerCollectibles] ¡Llave obtenida!");
        }

        public void AddCoin()
        {
            // Coin count is owned by GameManager so it persists across scenes
            // and feeds directly into the record-keeping / HUD systems.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoin();
            }
            else
            {
                Debug.LogWarning("[PlayerCollectibles] AddCoin: GameManager.Instance is null. " +
                                 "La moneda no fue registrada.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TEMPORARY POWER-UPS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts (or restarts) the Speed Boost, cancelling any active timer first.
        /// </summary>
        public void ActivateSpeedBoost()
        {
            if (_controller == null) return;

            // Cancel existing boost so the timer resets cleanly on re-collection
            if (_speedBoostCoroutine != null)
            {
                StopCoroutine(_speedBoostCoroutine);
                // Controller is already boosted; we'll re-apply via new coroutine
            }

            _speedBoostCoroutine = StartCoroutine(SpeedBoostRoutine());
        }

        /// <summary>
        /// Starts (or restarts) the Jump Boost, cancelling any active timer first.
        /// </summary>
        public void ActivateJumpBoost()
        {
            if (_controller == null) return;

            if (_jumpBoostCoroutine != null)
            {
                StopCoroutine(_jumpBoostCoroutine);
            }

            _jumpBoostCoroutine = StartCoroutine(JumpBoostRoutine());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COROUTINES
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator SpeedBoostRoutine()
        {
            // Apply boost
            _controller.ApplySpeedBoost(_speedBoostMultiplier);
            SpeedBoosted     = true;
            SpeedBoostTimeLeft = _speedBoostDuration;

            Debug.Log($"[PlayerCollectibles] Speed Boost activado por {_speedBoostDuration}s " +
                      $"(x{_speedBoostMultiplier}).");

            // Count down
            while (SpeedBoostTimeLeft > 0f)
            {
                SpeedBoostTimeLeft -= Time.deltaTime;
                yield return null;
            }

            // Revert
            _controller.RevertSpeedBoost();
            SpeedBoosted       = false;
            SpeedBoostTimeLeft = 0f;
            _speedBoostCoroutine = null;

            Debug.Log("[PlayerCollectibles] Speed Boost finalizado.");
        }

        private IEnumerator JumpBoostRoutine()
        {
            // Apply boost
            _controller.ApplyJumpBoost(_jumpBoostMultiplier);
            JumpBoosted     = true;
            JumpBoostTimeLeft = _jumpBoostDuration;

            Debug.Log($"[PlayerCollectibles] Jump Boost activado por {_jumpBoostDuration}s " +
                      $"(x{_jumpBoostMultiplier}).");

            // Count down
            while (JumpBoostTimeLeft > 0f)
            {
                JumpBoostTimeLeft -= Time.deltaTime;
                yield return null;
            }

            // Revert
            _controller.RevertJumpBoost();
            JumpBoosted       = false;
            JumpBoostTimeLeft = 0f;
            _jumpBoostCoroutine = null;

            Debug.Log("[PlayerCollectibles] Jump Boost finalizado.");
        }
    }
}