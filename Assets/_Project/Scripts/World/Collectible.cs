// =============================================================================
//  Collectible.cs  (UPDATED)
//  Project : Platformer Prototype
//
//  PURPOSE
//  -------
//  Handles collision detection, spawns a pickup VFX particle system, routes the
//  correct action to PlayerCollectibles, and destroys itself.
//
//  CHANGES FROM ORIGINAL
//  ---------------------
//  • Added [Header] "VFX" + pickupEffect GameObject reference.
//  • Completed SpeedBoost and JumpBoost switch cases (were placeholders).
//  • Added null-guard before VFX instantiation and auto-destroy of the VFX.
// =============================================================================

using UnityEngine;
using Platformer.Player;

namespace Platformer.World
{
    [RequireComponent(typeof(Collider))]
    public class Collectible : MonoBehaviour
    {
        public enum CollectibleType { Key, Coin, SpeedBoost, JumpBoost }

        [Header("Item Settings")]
        [Tooltip("Type of collectible — determines which effect is applied to the player.")]
        public CollectibleType type;

        [Header("VFX")]
        [Tooltip("Optional Particle System prefab to spawn at the pick-up position.")]
        [SerializeField] private GameObject _pickupEffect;

        [Tooltip("Seconds before the spawned VFX GameObject is auto-destroyed. " +
                 "Set to match your particle system's duration.")]
        [SerializeField] private float _effectLifetime = 2f;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            PlayerCollectibles collectibles = other.GetComponent<PlayerCollectibles>();
            if (collectibles == null) return;

            // ── Spawn pickup VFX ──────────────────────────────────────────────
            if (_pickupEffect != null)
            {
                GameObject vfx = Instantiate(_pickupEffect, transform.position, Quaternion.identity);
                Destroy(vfx, _effectLifetime);
            }

            // ── Apply effect based on type ────────────────────────────────────
            switch (type)
            {
                case CollectibleType.Key:
                    collectibles.AddKey();
                    break;

                case CollectibleType.Coin:
                    collectibles.AddCoin();
                    break;

                case CollectibleType.SpeedBoost:
                    collectibles.ActivateSpeedBoost();
                    break;

                case CollectibleType.JumpBoost:
                    collectibles.ActivateJumpBoost();
                    break;
            }

            // Destroy visual immediately so the player can't collect it twice
            Destroy(gameObject);
        }
    }
}