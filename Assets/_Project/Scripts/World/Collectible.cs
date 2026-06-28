using UnityEngine;
using Platformer.Player;

namespace Platformer.World
{
    [RequireComponent(typeof(Collider))]
    public class Collectible : MonoBehaviour
    {
        public enum CollectibleType { Key, Coin, SpeedBoost, JumpBoost }

        [Header("Item Settings")]
        public CollectibleType type;

        private void OnTriggerEnter(Collider other)
        {
            // Verificamos si el que entró al trigger es el jugador
            if (other.CompareTag("Player"))
            {
                PlayerCollectibles collectibles = other.GetComponent<PlayerCollectibles>();

                if (collectibles != null)
                {
                    // Aplicamos el efecto según el tipo de ítem
                    switch (type)
                    {
                        case CollectibleType.Key:
                            collectibles.AddKey();
                            break;
                        case CollectibleType.Coin:
                            collectibles.AddCoin();
                            break;
                        case CollectibleType.SpeedBoost:
                            // Aquí podrías llamar a un método en PlayerController3D para subir la velocidad
                            Debug.Log("¡Velocidad aumentada!");
                            break;
                    }

                    // Destruimos el objeto visual (se agarró)
                    Destroy(gameObject);
                }
            }
        }
    }
}