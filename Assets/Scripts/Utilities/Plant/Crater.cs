using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Crater : NetworkBehaviour
{
    [SerializeField] private float duration = 3.0f;
    
    private Tile occupiedTile;

    public override void OnNetworkSpawn()
    {
        // Run occupancy logic on ALL clients so the grid is blocked locally
        FindAndOccupyTile();

        // Server handles the lifetime/despawn
        if (IsServer)
        {
            StartCoroutine(LifeCycle());
        }
    }

    private void FindAndOccupyTile()
    {
        // Find all tiles and check distance to find the one underneath
        Tile[] allTiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        float closestDist = float.MaxValue;
        Tile closestTile = null;

        foreach (Tile tile in allTiles)
        {
            float dist = Vector3.Distance(transform.position, tile.transform.position);
            if (dist < 0.5f && dist < closestDist)
            {
                closestDist = dist;
                closestTile = tile;
            }
        }

        if (closestTile != null)
        {
            occupiedTile = closestTile;
            // Use ForceOccupy to claim the tile even if DoomShroom hasn't fully cleared its reference yet
            occupiedTile.ForceOccupy(gameObject);
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
