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
        if (tile == null || selectedPlantPrefab == null) return;
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