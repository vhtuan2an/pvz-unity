using UnityEngine;

public class ZombieLaneClick : MonoBehaviour
{
    public Transform spawnPoint; 

    public void RequestSpawnZombieOnLane()
    {
        if (spawnPoint == null)
        {
            Debug.LogError("SpawnPoint missing on lane!");
            return;
        }

        ZombieManager.Instance?.TrySpawnZombieOnLane(spawnPoint);
    }
}
