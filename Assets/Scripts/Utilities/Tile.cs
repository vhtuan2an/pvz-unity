using UnityEngine;

public class Tile : MonoBehaviour
{
    public bool IsOccupied { get; private set; }
    public GameObject Occupant { get; private set; }

    public Vector3 plantOffset = Vector3.zero;
    public Vector3 PlantWorldPosition => transform.position + plantOffset;

    public bool TryOccupy(GameObject occupant)
    {
        if (IsOccupied || occupant == null) return false;
        IsOccupied = true;
        Occupant = occupant;
        Debug.Log($"Tile occupied: {name}, IsOccupied = {IsOccupied}");
        return true;
    }

    public void Clear()
    {
        IsOccupied = false;
        Occupant = null;
        Debug.Log($"Tile cleared: {name}, IsOccupied = {IsOccupied}");
    }

    public GameObject GetOccupyingPlant()
    {
        return Occupant;
    }

    void OnMouseDown()
    {
        Debug.Log($"🖱️ Tile clicked: {name}, IsOccupied: {IsOccupied}, Occupant: {(Occupant != null ? Occupant.name : "null")}");
        PlantManager.Instance?.TryPlaceOnTile(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 1f, 0f));
    }
}