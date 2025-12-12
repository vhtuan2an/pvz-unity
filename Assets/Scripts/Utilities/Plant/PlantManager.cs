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
    private System.Collections.Generic.List<SeedPacket> allSeedPackets = new System.Collections.Generic.List<SeedPacket>();

    // Preview
    private GameObject previewObject;
    private SpriteRenderer previewRenderer;
    private Tile currentHoveredTile;
    private Vector3 currentPivotOffset;
    private Vector3 currentScale;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateSunCounter();
        CreatePreviewObject();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleWorldClick();
        }
        UpdatePreview();
    }

    private void CreatePreviewObject()
    {
        previewObject = new GameObject("PlantPreview");
        previewRenderer = previewObject.AddComponent<SpriteRenderer>();
        previewRenderer.sortingOrder = 100;
        previewObject.SetActive(false);
    }

    private void UpdatePreview()
    {
        if (selectedPlantPrefab == null)
        {
            HidePreview();
            return;
        }

        // Raycast to find tile under mouse
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.RaycastAll(ray.origin, ray.direction, Mathf.Infinity);
        Tile hoveredTile = null;
        foreach (var hit in hits)
        {
            Tile tile = hit.collider.GetComponent<Tile>();
            if (tile != null)
            {
                hoveredTile = tile;
                break;
            }
        }

        if (hoveredTile != null)
        {
            currentHoveredTile = hoveredTile;
            ShowPreview(hoveredTile);
        }
        else
        {
            currentHoveredTile = null;
            HidePreview();
        }
    }

    private void ShowPreview(Tile tile)
    {
        if (!previewObject.activeSelf)
        {
            previewObject.SetActive(true);
        }

        // ⭐ Apply position, scale, and offset
        previewObject.transform.position = tile.PlantWorldPosition + currentPivotOffset;
        previewObject.transform.localScale = currentScale; // ⭐ Apply scale

        // Determine if placement is valid
        bool canPlace = !tile.IsOccupied && currentSun >= selectedCost;
        
        // Set color based on validity
        Color previewColor = canPlace ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.3f, 0.3f, 0.6f);
        previewRenderer.color = previewColor;
    }

    private void HidePreview()
    {
        if (previewObject != null)
        {
            previewObject.SetActive(false);
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
            Debug.Log($"Tile clicked: {clickedTile.name}");
            
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

    // Register seed packet for dimming system
    public void RegisterSeedPacket(SeedPacket packet)
    {
        if (!allSeedPackets.Contains(packet))
            allSeedPackets.Add(packet);
    }

    public void SelectPlant(GameObject prefab, int cost, SeedPacket seedPacket = null)
    {
        selectedPlantPrefab = prefab;
        selectedCost = cost;
        selectedSeedPacket = seedPacket;
        
        SetPreviewSprite(prefab);
        
        // Dim all other seed packets
        foreach (var packet in allSeedPackets)
        {
            packet.SetDimmed(packet != seedPacket);
        }
        
        Debug.Log($"Plant selected: {prefab.name}, Cost: {cost}");
    }

    // Extract first frame sprite from plant prefab
    private void SetPreviewSprite(GameObject prefab)
    {
        if (previewRenderer == null) return;

        // Reset pivot offset and scale
        currentPivotOffset = Vector3.zero;
        currentScale = Vector3.one;

        // Get scale from prefab's Transform
        currentScale = prefab.transform.localScale;
        Debug.Log($"Preview scale for {prefab.name}: {currentScale}");

        // Get pivot offset from PlantBase
        PlantBase plantBase = prefab.GetComponent<PlantBase>();
        if (plantBase != null)
        {
            // Access pivotOffset via reflection (protected field)
            var pivotField = typeof(PlantBase).GetField("pivotOffset", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pivotField != null)
            {
                currentPivotOffset = (Vector3)pivotField.GetValue(plantBase);
                Debug.Log($"Preview pivot offset for {prefab.name}: {currentPivotOffset}");
            }
        }

        // Try to get sprite from SpriteRenderer
        SpriteRenderer plantRenderer = prefab.GetComponent<SpriteRenderer>();
        if (plantRenderer != null && plantRenderer.sprite != null)
        {
            previewRenderer.sprite = plantRenderer.sprite;
            return;
        }

        // Try to get first frame from Animator
        Animator animator = prefab.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            // Instantiate temporarily to get first frame
            GameObject tempPlant = Instantiate(prefab);
            tempPlant.SetActive(true);
            
            Animator tempAnimator = tempPlant.GetComponent<Animator>();
            SpriteRenderer tempRenderer = tempPlant.GetComponent<SpriteRenderer>();
            
            if (tempAnimator != null && tempRenderer != null)
            {
                tempAnimator.Update(0f); // Force update to first frame
                previewRenderer.sprite = tempRenderer.sprite;
            }
            
            Destroy(tempPlant);
        }
    }

    public void ClearSelection()
    {
        HidePreview();
        currentPivotOffset = Vector3.zero;
        currentScale = Vector3.one;
        // Undim all seed packets
        foreach (var packet in allSeedPackets)
        {
            packet.SetDimmed(false);
        }
        
        selectedPlantPrefab = null;
        selectedSeedPacket = null;
    }

    // Called when cooldown ends
    public void RefreshDimming()
    {
        if (selectedSeedPacket != null)
        {
            // Reapply dimming to all other packets
            foreach (var packet in allSeedPackets)
            {
                packet.SetDimmed(packet != selectedSeedPacket);
            }
        }
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
            
            // Don't destroy if Wallnut first-aid
            bool isWallnutRestoration = existingPlant.name.Contains("Wallnut") && 
                                     selectedPlantPrefab.name.Contains("Wallnut");
            
            if (FusionManager.Instance.TryFusion(tile, existingPlant, selectedPlantPrefab, currentSun))
            {
                if (!isWallnutRestoration && existingPlant != null)
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