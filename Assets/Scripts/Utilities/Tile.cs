using UnityEngine;
using UnityEngine.EventSystems;

public class Tile : MonoBehaviour
{
    private bool _isOccupied;
    public bool IsOccupied 
    { 
        get 
        {
            // Also check if occupant was destroyed
            if (_isOccupied && Occupant == null)
            {
                Clear();
                return false;
            }
            return _isOccupied;
        }
        private set => _isOccupied = value;
    }
    public GameObject Occupant { get; private set; }

    public Vector3 plantOffset = Vector3.zero;
    public Vector3 PlantWorldPosition => transform.position + plantOffset;

    void OnMouseDown()
    {
        // Block if mouse is over UI (e.g. Buttons, Panels)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Debug.Log($"Tile clicked: {name}, IsOccupied: {IsOccupied}, Occupant: {(Occupant != null ? Occupant.name : "null")}");

        // Prevent interaction if game is not playing
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsGameStarted)
        {
            return;
        }
        
        // Route to correct manager based on player role
        if (LobbyManager.Instance == null) return;
        
        if (LobbyManager.Instance.SelectedRole == PlayerRole.Plant)
        {
            PlantManager.Instance?.TryPlaceOnTile(this);
        }
        else if (LobbyManager.Instance.SelectedRole == PlayerRole.Zombie)
        {
            ZombieManager.Instance?.TrySpawnZombieOnLane(this.transform);
        }
    }

    public bool TryOccupy(GameObject occupant)
    {
        if (IsOccupied) return false;
        _isOccupied = true;
        Occupant = occupant;
        Debug.Log($"Tile '{name}' occupied by {occupant.name}");
        return true;
    }

    public void Clear()
    {
        _isOccupied = false;
        Occupant = null;
        Debug.Log($"Tile '{name}' cleared");
    }

    public GameObject GetOccupyingPlant()
    {
        return Occupant;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(PlantWorldPosition, 0.15f);
    }
}