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
        
        string existingName = existingPlant.name.Replace("(Clone)", "").Trim();
        string baseName = basePlant.name.Replace("(Clone)", "").Trim();
        
        // Use exact match to prevent issues where "Mega Gatling Pea" contains "Gatling Pea"
        bool baseMatches = existingName.Equals(baseName);
        bool addedMatches = plantToPlace.name.Contains(addedPlant.name);
        
        return baseMatches && addedMatches;
    }
}