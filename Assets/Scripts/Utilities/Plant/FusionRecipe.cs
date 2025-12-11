using UnityEngine;

[CreateAssetMenu(fileName = "FusionRecipe", menuName = "PvZ/Fusion Recipe")]
public class FusionRecipe : ScriptableObject
{
    [Header("Fusion Components")]
    public GameObject basePlant;
    public GameObject addedPlant;
    
    [Header("Fusion Result")]
    public GameObject resultFusion;
    
    public bool CanFuse(GameObject existingPlant, GameObject plantToPlace)
    {
        if (existingPlant == null || plantToPlace == null) return false;
        
        bool baseMatches = existingPlant.name.Contains(basePlant.name.Replace("(Clone)", ""));
        bool addedMatches = plantToPlace.name.Contains(addedPlant.name);
        
        return baseMatches && addedMatches;
    }
}