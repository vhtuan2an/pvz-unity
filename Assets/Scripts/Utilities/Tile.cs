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
        Vector3 tilePos = transform.position;
        float tileBottomY = tilePos.y - 0.5f;

        float spriteWorldHeight = 1f;
        var sr = occupant.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            spriteWorldHeight = sr.bounds.size.y;

        Vector3 targetWorldPos = new Vector3(
            tilePos.x + plantOffset.x,
            tileBottomY + (spriteWorldHeight * 0.5f) + plantOffset.y,
            tilePos.z + plantOffset.z
        );
occupant.transform.position = targetWorldPos;
        occupant.transform.SetParent(transform, true); // keep world position stable after parenting
        occupant.transform.localRotation = Quaternion.identity;

        Debug.Log($"Tile occupied: {name}, IsOccupied = {IsOccupied}");
        return true;
    }

    public void Clear()
    {
        IsOccupied = false;
        Occupant = null;
        Debug.Log($"Tile cleared: {name}, IsOccupied = {IsOccupied}");
    }

    void OnMouseDown()
    {
        PlantManager.Instance?.TryPlaceOnTile(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 1f, 0f));
    }
}