using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Crater : NetworkBehaviour
{
    [SerializeField] private float duration = 3.0f;
    
    private Tile occupiedTile;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        FindAndOccupyTile();
        StartCoroutine(LifeCycle());
    }

    private void FindAndOccupyTile()
    {
        // Find all tiles and check distance to find the one underneath
        // Since Crater replaces DoomShroom, it should be at the exact same position
        Tile[] allTiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        float closestDist = float.MaxValue;
        Tile closestTile = null;

        foreach (Tile tile in allTiles)
        {
            float dist = Vector3.Distance(transform.position, tile.transform.position);
            if (dist < 0.5f && dist < closestDist) // Threshold to be considered "on" the tile
            {
                closestDist = dist;
                closestTile = tile;
            }
        }

        if (closestTile != null)
        {
            occupiedTile = closestTile;
            
            // If the tile was occupied by the DoomShroom, it might have been cleared when DoomShroom died.
            // If DoomShroom is still there (race condition?), we need to force occupy or handle it.
            // But DoomShroom calls Die() immediately after spawning this.
            // Die() clears the tile.
            // So we might need to wait a frame or force occupancy.
            
            // To ensure we grab it after DoomShroom clears it (or overwrite it), let's try occupying.
            // Note: If DoomShroom is currently occupying it, TryOccupy returns false.
            // We should probably rely on DoomShroom clearing it first, or forcefully set it.
            // But Tile script doesn't have ForceOccupy.
            
            // Let's assume DoomShroom clears the tile when it calls Die(), which happens right after spawn.
            // So we might need to wait a tiny bit or retry.
            
           StartCoroutine(TryOccupyRoutine(closestTile));
        }
    }

    private IEnumerator TryOccupyRoutine(Tile tile)
    {
        // Wait a frame to allow DoomShroom to Die() and clear the tile
        yield return null; 

        if (tile.TryOccupy(gameObject))
        {
            Debug.Log($"Crater occupied tile {tile.name}");
        }
        else
        {
            Debug.LogWarning($"Crater failed to occupy tile {tile.name}. Is it still occupied?");
        }
    }

    private IEnumerator LifeCycle()
    {
        yield return new WaitForSeconds(duration);
        
        if (occupiedTile != null)
        {
            occupiedTile.Clear();
        }

        if (IsSpawned)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null) 
            {
                netObj.Despawn();
            }
        }
        
        Destroy(gameObject);
    }
}
