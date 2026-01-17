using UnityEngine;
using Unity.Netcode;

public class PlantBase : NetworkBehaviour
{
    [Header("Plant Stats")]
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] public int sunCost = 100;
    [SerializeField] public float cooldown = 7.5f;
    [SerializeField] public Sprite packetImage;

    [Header("Positioning")]
    [SerializeField] protected Vector3 pivotOffset = Vector3.zero;

    protected int currentHealth;
    protected Tile occupiedTile;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        
        // Apply pivot offset to position
        if (pivotOffset != Vector3.zero)
        {
            transform.position += pivotOffset;
            Debug.Log($"{gameObject.name} applied pivot offset: {pivotOffset}");
        }

        FindOccupiedTile();
        
        // Add static sorting
        if (GetComponent<DynamicSorting>() == null)
        {
            var sorting = gameObject.AddComponent<DynamicSorting>();
            // Use reflection or just public field if accessible (it is serialized private, but we can rely on default)
            // Or just let it run. Default is !isStatic, which is fine, or we can assume Plants don't move much
            // But let's check if we can set isStatic via reflection or just leave it dynamic for now (cheap enough)
            // Actually, we can't set private serialized fields easily without reflection.
            // Let's just AddComponent. It will run in LateUpdate which handles potential minor movements or just works.
        }
    }

    private void FindOccupiedTile()
    {
        // Find all tiles and check which one has this plant as occupant
        Tile[] allTiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        foreach (Tile tile in allTiles)
        {
            if (tile.GetOccupyingPlant() == gameObject)
            {
                occupiedTile = tile;
                Debug.Log($"Plant {name} found its tile: {tile.name}");
                break;
            }
        }
    }

    // Forward mouse clicks to the tile below
    private void OnMouseDown()
    {
        if (occupiedTile != null)
        {
            Debug.Log($"🌿 Plant {name} clicked, forwarding to tile: {occupiedTile.name}");
            PlantManager.Instance?.TryPlaceOnTile(occupiedTile);
        }
        else
        {
            Debug.LogWarning($"⚠️ Plant {name} clicked but no tile reference found!");
            FindOccupiedTile(); // Try to find it again
            if (occupiedTile != null)
            {
                PlantManager.Instance?.TryPlaceOnTile(occupiedTile);
            }
        }
    }

    public virtual void TakeDamage(int damage)
    {
        // Chỉ server xử lý damage
        if (!IsServer)
            return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        // Only server handles death
        if (!IsServer)
            return;

        Debug.Log($"{gameObject.name} died!");

        // Clear tile occupancy
        if (occupiedTile != null)
        {
            occupiedTile.Clear();
            Debug.Log($"Tile {occupiedTile.name} cleared by {gameObject.name} death.");
        }

        // Despawn from network before destroy
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }

        Destroy(gameObject);
    }
}