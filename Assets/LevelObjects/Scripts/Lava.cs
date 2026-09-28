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
                player.ReturnToStart();
            }
        }
    }
}