using UnityEngine;

public class ZombieWinTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the collider belongs to a zombie
        if (other.CompareTag("Zombie"))
        {
            ZombieBase zombie = other.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                Debug.Log($"🧟 Zombie {zombie.name} crossed the line! Zombies win!");
                // Call your game over logic here
                NetworkGameManager.Instance?.OnZombieWin();
            }
        }
    }
}