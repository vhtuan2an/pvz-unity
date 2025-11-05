using UnityEngine;
using TMPro;
public class PlantManager : MonoBehaviour
{
    public static PlantManager Instance { get; private set; }

    [Header("Grid")]
    public Transform plantsParent; // Parent for spawned plants
    public int currentSun = 100; // Starting sun amount

    [Header("UI")]
    public TextMeshProUGUI countText;

    private GameObject selectedPlantPrefab;
    private int selectedCost;
    private SeedPacket selectedSeedPacket;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateSunCounter(); // initialize UI
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

        GameObject plant = Instantiate(selectedPlantPrefab, tile.PlantWorldPosition, Quaternion.identity, plantsParent);
        if (!tile.TryOccupy(plant))
        {
            Destroy(plant);
            return;
        }

        SpendSun(selectedCost);
        selectedSeedPacket?.StartCooldown();

        // Automatically clear selection after placement
        ClearSelection();
    }

    private void UpdateSunCounter()
    {
        if (countText != null)
            countText.text = currentSun.ToString();
    }
}