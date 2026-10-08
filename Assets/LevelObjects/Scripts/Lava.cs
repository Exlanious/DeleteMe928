using UnityEngine;

namespace LevelObjects
{
    public class Lava : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player != null)
            {
                GameSession session = player.GetComponent<GameSession>();
                if (session != null) session.EndRun();
                else player.ReturnToStart();
            }
        }
    }
}
