using UnityEngine;
using System.Collections.Generic;

public class FusionManager : MonoBehaviour
{
    public static FusionManager Instance { get; private set; }
    
    [Header("Fusion Recipes")]
    [SerializeField] private List<FusionRecipe> fusionRecipes = new List<FusionRecipe>();
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    // Check if fusion is possible
    public FusionRecipe GetFusionRecipe(GameObject existingPlant, GameObject plantToPlace)
    {
        foreach (var recipe in fusionRecipes)
        {
            if (recipe.CanFuse(existingPlant, plantToPlace))
            {
                return recipe;
            }
        }
        return null;
    }
    
    // Perform fusion
    public bool TryFusion(Tile tile, GameObject existingPlant, GameObject plantToPlace, int availableSun)
    {
        FusionRecipe recipe = GetFusionRecipe(existingPlant, plantToPlace);
        
        if (recipe == null)
        {
            Debug.Log("No fusion recipe found");
            return false;
        }
        
        Debug.Log($"Fusion: {recipe.basePlant.name} + {recipe.addedPlant.name} → {recipe.resultFusion.name}");
        
        // Spawn result
        Vector3 position = tile.PlantWorldPosition;
        
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SpawnPlantAtPosition(position, recipe.resultFusion.name);
            return true;
        }
        
        return false;
    }
}