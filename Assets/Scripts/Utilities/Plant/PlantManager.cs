using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;

public class PlantManager : MonoBehaviour
{
    public static PlantManager Instance { get; private set; }

    [Header("Grid")]
    public Transform plantsParent;
    public int currentSun = 100;

    [Header("UI")]
    public TextMeshProUGUI countText;

    [Header("Fusion")]
    [SerializeField] private GameObject repeaterPrefab;

    private GameObject selectedPlantPrefab;
    private int selectedCost;
    private SeedPacket selectedSeedPacket;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateSunCounter();
    }

    public void SelectPlant(GameObject prefab, int cost, SeedPacket seedPacket = null)
    {
        selectedPlantPrefab = prefab;
        selectedCost = cost;
        selectedSeedPacket = seedPacket;
        Debug.Log($"🌱 Plant selected: {prefab.name}, Cost: {cost}");
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
        Debug.Log($"➡️ TryPlaceOnTile called - tile: {tile?.name}, selectedPlantPrefab: {selectedPlantPrefab?.name}");
        
        if (tile == null || selectedPlantPrefab == null)
        {
            Debug.LogWarning($"⚠️ Early return - tile null: {tile == null}, prefab null: {selectedPlantPrefab == null}");
            return;
        }
        
        Debug.Log($"Attempting to place: {selectedPlantPrefab.name}, Tile occupied: {tile.IsOccupied}");
        
        // Check for fusion: If tile has Peashooter and we're placing another Peashooter
        if (tile.IsOccupied)
        {
            GameObject existingPlant = tile.GetOccupyingPlant();
            Debug.Log($"Existing plant: {(existingPlant != null ? existingPlant.name : "null")}");
            
            // Check if both are Peashooters (handles "(Clone)" suffix)
            bool isPlacingPeashooter = selectedPlantPrefab.GetComponent<Peashooter>() != null;
            bool hasExistingPeashooter = existingPlant != null && existingPlant.GetComponent<Peashooter>() != null;
            
            Debug.Log($"Is placing Peashooter: {isPlacingPeashooter}, Has existing Peashooter: {hasExistingPeashooter}");
            
            if (isPlacingPeashooter && hasExistingPeashooter)
            {
                Debug.Log("🔥 Fusion condition met!");
                TryFusionToRepeater(tile, existingPlant);
                return;
            }
        }
        
        if (tile.IsOccupied) return;
        if (currentSun < selectedCost) return;

        // Kiểm tra role
        if (LobbyManager.Instance == null || LobbyManager.Instance.SelectedRole != PlayerRole.Plant)
        {
            Debug.LogWarning("Only Plant player can place plants!");
            return;
        }

        // Spawn qua NetworkGameManager thay vì Instantiate
        Vector3 position = tile.PlantWorldPosition;
        
        if (NetworkGameManager.Instance != null)
        {
            // Gọi hàm spawn từ NetworkGameManager
            NetworkGameManager.Instance.SpawnPlantAtPosition(position, selectedPlantPrefab.name);
            
            // Occupy tile (sẽ được sync sau khi server confirm spawn)
            // tile.TryOccupy(...) sẽ được gọi trong callback
            
            SpendSun(selectedCost);
            selectedSeedPacket?.StartCooldown();
            ClearSelection();
        }
        else
        {
            Debug.LogError("NetworkGameManager not found!");
        }
    }

    private void TryFusionToRepeater(Tile tile, GameObject existingPeashooter)
    {
        if (currentSun < selectedCost)
        {
            Debug.LogWarning($"⚠️ Not enough sun for fusion! Current: {currentSun}, Cost: {selectedCost}");
            return;
        }
        
        if (repeaterPrefab == null)
        {
            Debug.LogError("⚠️ Repeater prefab not assigned in PlantManager Inspector!");
            return;
        }

        Debug.Log("🔥 Fusing 2 Peashooters into Repeater!");

        if (NetworkGameManager.Instance != null)
        {
            // Clear the tile first
            tile.Clear();
            
            // Remove existing peashooter via server
            NetworkObject netObj = existingPeashooter.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                // Use NetworkGameManager to properly despawn via ServerRpc
                NetworkGameManager.Instance.DespawnPlantByNetworkId(netObj.NetworkObjectId);
            }
            else
            {
                Destroy(existingPeashooter);
            }

            // Spawn Repeater at same position
            Vector3 position = tile.PlantWorldPosition;
            Debug.Log($"📍 Spawning Repeater at {position}");
            NetworkGameManager.Instance.SpawnPlantAtPosition(position, "Repeater");

            SpendSun(selectedCost);
            selectedSeedPacket?.StartCooldown();
            ClearSelection();
            
            Debug.Log("✅ Fusion complete - Repeater spawn requested!");
        }
        else
        {
            Debug.LogError("NetworkGameManager not found!");
        }
    }

    // Hàm này sẽ được gọi từ NetworkGameManager sau khi spawn thành công
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