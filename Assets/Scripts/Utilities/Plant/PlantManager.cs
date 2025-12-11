using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlantManager : MonoBehaviour
{
    public static PlantManager Instance { get; private set; }

    [Header("Grid")]
    public Transform plantsParent;
    public int currentSun = 100;

    [Header("UI")]
    public TextMeshProUGUI countText;

    private GameObject selectedPlantPrefab;
    private int selectedCost;
    private SeedPacket selectedSeedPacket;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateSunCounter();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleWorldClick();
        }
    }

    // Tiles take priority
    private void HandleWorldClick()
    {
        // Cast ray to world
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.RaycastAll(ray.origin, ray.direction, Mathf.Infinity);
        
        // Look for tile in hits
        Tile clickedTile = null;
        foreach (var hit in hits)
        {
            Tile tile = hit.collider.GetComponent<Tile>();
            if (tile != null)
            {
                clickedTile = tile;
                break;
            }
        }
        
        if (clickedTile != null)
        {
            Debug.Log($"🖱️ Tile clicked: {clickedTile.name}");
            
            // Only place plant if one is selected
            if (selectedPlantPrefab != null && LobbyManager.Instance?.SelectedRole == PlayerRole.Plant)
            {
                TryPlaceOnTile(clickedTile);
            }            
            // Consume the click so UI doesn't process it
            return;
        }
        
        // No tile clicked - allow UI to process (buttons, etc.)
        Debug.Log("No tile clicked, allowing UI to process");
    }

    public void SelectPlant(GameObject prefab, int cost, SeedPacket seedPacket = null)
    {
        selectedPlantPrefab = prefab;
        selectedCost = cost;
        selectedSeedPacket = seedPacket;
        
        Debug.Log($"Plant selected: {prefab.name}, Cost: {cost}");
    }

    public void ClearSelection()
    {
        selectedPlantPrefab = null;
        selectedSeedPacket = null;
    }

    public void AddSun(int amount)
    {
        currentSun += amount;
        UpdateSunCounter();
    }

    public void SpendSun(int amount)
    {
        currentSun -= amount;
        if (currentSun < 0) currentSun = 0;
        UpdateSunCounter();
    }

    public void TryPlaceOnTile(Tile tile)
    {
        Debug.Log($"TryPlaceOnTile called - tile: {tile?.name}, selectedPlantPrefab: {selectedPlantPrefab?.name}");
        
        if (tile == null || selectedPlantPrefab == null)
        {
            Debug.LogWarning($"Early return - tile null: {tile == null}, prefab null: {selectedPlantPrefab == null}");
            return;
        }
        
        Debug.Log($"Attempting to place: {selectedPlantPrefab.name}, Tile occupied: {tile.IsOccupied}");
        
        if (tile.IsOccupied && FusionManager.Instance != null)
        {
            GameObject existingPlant = tile.GetOccupyingPlant();
            
            if (FusionManager.Instance.TryFusion(tile, existingPlant, selectedPlantPrefab, currentSun))
            {
                if (existingPlant != null)
                {
                    if (existingPlant.TryGetComponent<NetworkObject>(out NetworkObject netObj))
                    {
                        if (netObj.IsSpawned) netObj.Despawn();
                    }
                    Destroy(existingPlant);
                }
                SpendSun(selectedCost);
                selectedSeedPacket?.StartCooldown();
                ClearSelection();
                return;
            }
        }
        
        if (tile.IsOccupied) return;
        if (currentSun < selectedCost) return;

        if (LobbyManager.Instance == null || LobbyManager.Instance.SelectedRole != PlayerRole.Plant)
        {
            Debug.LogWarning("Only Plant player can place plants!");
            return;
        }
        
        Vector3 position = tile.PlantWorldPosition;
        
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SpawnPlantAtPosition(position, selectedPlantPrefab.name);
            
            SpendSun(selectedCost);
            selectedSeedPacket?.StartCooldown();
            ClearSelection();
        }
        else
        {
            Debug.LogError("NetworkGameManager not found!");
        }
    }

    public void OnPlantSpawned(GameObject plant, Tile tile)
    {
        if (tile.TryOccupy(plant))
        {
            Debug.Log($"Plant {plant.name} placed on {tile.name}");
        }
    }

    private void UpdateSunCounter()
    {
        if (countText != null)
            countText.text = currentSun.ToString();
    }
}