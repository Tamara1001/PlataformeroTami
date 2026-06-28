using UnityEngine;

namespace Platformer.Player
{
    public class PlayerCollectibles : MonoBehaviour
    {
        public bool HasKey { get; private set; }
        public int Coins { get; private set; }

        public void AddKey()
        {
            HasKey = true;
            Debug.Log("[PlayerCollectibles] ¡Llave obtenida!");
        }

        public void AddCoin()
        {
            Coins++;
            Debug.Log($"[PlayerCollectibles] Monedas: {Coins}");
        }
    }
}