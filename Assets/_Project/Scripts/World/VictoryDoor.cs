using UnityEngine;
using Platformer.Player;

namespace Platformer.World
{
    [RequireComponent(typeof(Collider))]
    public class VictoryDoor : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerCollectibles collectibles = other.GetComponent<PlayerCollectibles>();

                if (collectibles != null)
                {
                    if (collectibles.HasKey)
                    {
                        Debug.Log("[VictoryDoor] ¡Tienes la llave! Abriendo puerta... VICTORIA");

                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.ChangeState(GameManager.GameState.Victory);
                        }
                    }
                    else
                    {
                        Debug.Log("[VictoryDoor] Puerta bloqueada. ¡Necesitas encontrar la llave!");
                    }
                }
            }
        }
    }
}