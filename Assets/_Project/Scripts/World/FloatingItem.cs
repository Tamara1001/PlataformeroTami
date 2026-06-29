// =============================================================================
//  FloatingItem.cs
//  Project : Platformer Prototype
//
//  PURPOSE
//  -------
//  Lightweight visual-only script that makes a collectible item hover in place
//  using a sine wave and continuously spin on its Y-axis.
//  Attach directly to the collectible root GameObject (or its visual child).
// =============================================================================

using UnityEngine;

namespace Platformer.World
{
    public class FloatingItem : MonoBehaviour
    {
        [Header("Hover Settings")]
        [Tooltip("Total vertical distance (in meters) the item travels up and down.")]
        [SerializeField] private float _hoverAmplitude = 0.25f;

        [Tooltip("How fast the item bobs up and down (cycles per second).")]
        [SerializeField] private float _hoverFrequency = 1.2f;

        [Tooltip("Phase offset so that nearby identical items don't all sync together.")]
        [SerializeField] private float _phaseOffset = 0f;

        [Header("Rotation Settings")]
        [Tooltip("Degrees per second to spin on the Y-axis.")]
        [SerializeField] private float _rotationSpeed = 90f;

        // We store the spawn Y so the hover is always relative to the original
        // placement, not accumulated drift.
        private float _originY;

        private void Awake()
        {
            _originY = transform.position.y;
        }

        private void Update()
        {
            // ── Hover (sine wave, world-space Y) ──────────────────────────────
            float newY = _originY + Mathf.Sin((Time.time + _phaseOffset) * _hoverFrequency * Mathf.PI * 2f)
                         * _hoverAmplitude;

            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // ── Spin (local Y-axis) ───────────────────────────────────────────
            transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
