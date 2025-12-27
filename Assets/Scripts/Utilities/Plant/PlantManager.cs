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
    [Header("UI")]
    public TextMeshProUGUI countText;
    public UnityEngine.UI.Image shovelImage;
    [Header("Shovel Settings")]
    public Sprite shovelCursorSprite;
    public float shovelCursorScale = 1.0f;
    private GameObject shovelCursorObject;
    public Color shovelActiveColor = Color.red;
    public Color shovelInactiveColor = Color.white;

    private GameObject selectedPlantPrefab;
    private int selectedCost;
    private SeedPacket selectedSeedPacket;
    private System.Collections.Generic.List<SeedPacket> allSeedPackets = new System.Collections.Generic.List<SeedPacket>();
    
    private bool isShovelActive = false;

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
        CreateShovelCursor();
        CreatePreviewObject();
    }

    private void CreateShovelCursor()
    {
        shovelCursorObject = new GameObject("ShovelCursor");
        var image = shovelCursorObject.AddComponent<UnityEngine.UI.Image>();
        
        // Use custom sprite if assigned, otherwise use the shovel icon sprite
        if (shovelCursorSprite != null)
            image.sprite = shovelCursorSprite;
        else if (shovelImage != null)
            image.sprite = shovelImage.sprite;
            
        image.raycastTarget = false;
        
        // Find a suitable parent canvas
        Transform parentTransform = null;
        if (shovelImage != null)
        {
            parentTransform = shovelImage.canvas != null ? shovelImage.canvas.transform : shovelImage.transform.parent;
        }
        
        if (parentTransform == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null) parentTransform = canvas.transform;
        }

        if (parentTransform != null)
        {
            shovelCursorObject.transform.SetParent(parentTransform, false);
        }
        else
        {
            Debug.LogWarning("Could not find a Canvas for Shovel Cursor!");
        }
        
        // Apply scale
        shovelCursorObject.transform.localScale = Vector3.one * shovelCursorScale;
        
        // Disable initially
        shovelCursorObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleWorldClick();
        }
        
        if (isShovelActive && shovelCursorObject != null)
        {
            shovelCursorObject.transform.position = Input.mousePosition;
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
        if (isShovelActive)
        {
            HandleShovelPreview();
            return;
        }

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

        // Check for fusion possibility
        bool isFusionPreview = false;
        
        if (tile.IsOccupied && FusionManager.Instance != null && selectedPlantPrefab != null)
        {
            GameObject existingPlant = tile.GetOccupyingPlant();
            
            // Check if existing plant is still valid (not destroyed)
            if (existingPlant != null)
            {
                // Check for Wallnut restoration
                bool isWallnutRestoration = existingPlant.name.Contains("Wallnut") && 
                                             selectedPlantPrefab.name.Contains("Wallnut");
                
                if (isWallnutRestoration)
                {
                    Wallnut wallnut = existingPlant.GetComponent<Wallnut>();
                    if (wallnut != null && wallnut.CurrentHealth < wallnut.MaxHealth)
                    {
                        isFusionPreview = true;
                        SetPreviewSprite(selectedPlantPrefab);
                    }
                }
                else
                {
                    // Check regular fusion recipes
                    FusionRecipe recipe = FusionManager.Instance.GetFusionRecipe(existingPlant, selectedPlantPrefab);
                    
                    if (recipe != null && recipe.resultFusion != null)
                    {
                        isFusionPreview = true;
                        SetPreviewSprite(recipe.resultFusion);
                    }
                }
            }
        }
        
        // If not showing fusion preview, use normal selected plant
        if (!isFusionPreview && previewRenderer.sprite != GetSpriteFromPrefab(selectedPlantPrefab))
        {
            SetPreviewSprite(selectedPlantPrefab);
        }

        // Apply position, scale, and offset
        previewObject.transform.position = tile.PlantWorldPosition + currentPivotOffset;
        previewObject.transform.localScale = currentScale;

        // Determine if placement is valid
        bool canPlace = isFusionPreview ? (currentSun >= selectedCost) : (!tile.IsOccupied && currentSun >= selectedCost);
        
        // Set color based on validity
        Color previewColor = canPlace ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.3f, 0.3f, 0.6f);
        previewRenderer.color = previewColor;
    }

    // Helper to get sprite from prefab without changing state
    private Sprite GetSpriteFromPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        
        SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
        if (renderer != null) return renderer.sprite;
        
        return null;
    }

    private void HandleShovelPreview()
    {
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

        if (hoveredTile != null && hoveredTile.IsOccupied)
        {
            GameObject plant = hoveredTile.GetOccupyingPlant();
            if (plant != null)
            {
                if (!previewObject.activeSelf) previewObject.SetActive(true);
                
                // Set sprite to plant's sprite
                SpriteRenderer plantRenderer = plant.GetComponent<SpriteRenderer>();
                if (plantRenderer != null)
                {
                    previewRenderer.sprite = plantRenderer.sprite;
                }
                
                // Red overlay for shovel
                previewRenderer.color = new Color(1f, 0.3f, 0.3f, 0.6f);
                
                // Calculate and apply pivot offset
                Vector3 pivotOffset = Vector3.zero;
                PlantBase plantBase = plant.GetComponent<PlantBase>();
                if (plantBase != null)
                {
                    var pivotField = typeof(PlantBase).GetField("pivotOffset", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (pivotField != null)
                    {
                        pivotOffset = (Vector3)pivotField.GetValue(plantBase);
                    }
                }

                previewObject.transform.position = hoveredTile.PlantWorldPosition + pivotOffset;
                previewObject.transform.localScale = plant.transform.localScale;
                return;
            }
        }
        
        // If not hovering valid plant, hide preview
        HidePreview();
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
            
            // Only place plant if one is selected or shovel is active
            if ((selectedPlantPrefab != null || isShovelActive) && LobbyManager.Instance?.SelectedRole == PlayerRole.Plant)
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
        // Deactivate shovel if active (Mutual Exclusivity)
        if (isShovelActive)
        {
            isShovelActive = false;
            if (shovelCursorObject != null) shovelCursorObject.SetActive(false);
            UpdateShovelUI(); // Reset UI color
            HidePreview(); // Clear potential shovel preview
        }

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

    public void ToggleShovel()
    {
        isShovelActive = !isShovelActive;
        
        if (isShovelActive)
        {
            ClearSelection(false);
            if (shovelCursorObject != null) shovelCursorObject.SetActive(true);
            Debug.Log("Shovel Mode: ACTIVATED");
        }
        else
        {
            if (shovelCursorObject != null) shovelCursorObject.SetActive(false);
            HidePreview(); // Ensure red overlay is gone
            Debug.Log("Shovel Mode: DEACTIVATED");
        }

        UpdateShovelUI();
    }

    private void UpdateShovelUI()
    {
        if (shovelImage != null)
        {
            shovelImage.color = isShovelActive ? shovelActiveColor : shovelInactiveColor;
        }
    }

    public void ClearSelection()
    {
        ClearSelection(true);
    }

    public void ClearSelection(bool clearShovel)
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
        
        if (clearShovel)
        {
            isShovelActive = false;
            if (shovelCursorObject != null) shovelCursorObject.SetActive(false);
            UpdateShovelUI();
        }
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
        Debug.Log($"TryPlaceOnTile called - tile: {tile?.name}, isShovel: {isShovelActive}");
        
        if (tile == null) return;

        if (isShovelActive)
        {
            if (tile.IsOccupied)
            {
                GameObject plant = tile.GetOccupyingPlant();
                if (plant != null && NetworkGameManager.Instance != null)
                {
                    if (plant.TryGetComponent<NetworkObject>(out NetworkObject netObj))
                    {
                        NetworkGameManager.Instance.DespawnPlantByNetworkId(netObj.NetworkObjectId);
                        NetworkGameManager.Instance.PlaySoundClientRpc("plant_shovel"); // Optional sound
                        Debug.Log($"Shoveled plant on {tile.name}");
                        ToggleShovel(); 
                    }
                }
            }
            return;
        }

        if (selectedPlantPrefab == null)
        {
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
                    if (existingPlant.TryGetComponent<NetworkObject>(out NetworkObject netObj) && netObj.IsSpawned)
                    {
                        NetworkGameManager.Instance?.DespawnPlantByNetworkId(netObj.NetworkObjectId);
                    }
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
        // If tile thinks it's occupied but the old plant is destroyed, clear it first
        if (tile.IsOccupied)
        {
            GameObject oldPlant = tile.GetOccupyingPlant();
            if (oldPlant == null || oldPlant == plant)
            {
                // Old plant was destroyed or it's the same plant, force clear
                tile.Clear();
            }
        }
        
        if (tile.TryOccupy(plant))
        {
            Debug.Log($"Plant {plant.name} placed on {tile.name}");
        }
        else
        {
            Debug.LogWarning($"Failed to occupy tile {tile.name} with {plant.name}");
        }
    }

    private void UpdateSunCounter()
    {
        if (countText != null)
            countText.text = currentSun.ToString();
    }
}