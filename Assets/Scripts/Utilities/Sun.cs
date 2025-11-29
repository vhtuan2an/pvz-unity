using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Sun : MonoBehaviour
{
    [Header("Sun Settings")]
    public int sunValue = 50;
    public float lifetime = 8f;
    public float rotationSpeed = 50f;

    [Header("Collection")]
    public Vector2 collectTarget = new Vector2(-8.9f, 3.7f);
    public float collectSpeed = 25f;
    public float collectArrivalThreshold = 0.05f;
    
    [Header("Hover Collection")]
    public float hoverCollectRadius = 1.5f;

    bool isCollected = false;
    Collider2D col2d;
    Rigidbody2D rb2d;
    Coroutine autoDestroyRoutine;

    void Awake()
    {
        col2d = GetComponent<Collider2D>();
        rb2d = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        autoDestroyRoutine = StartCoroutine(AutoDestroyCoroutine());
    }

    void Update()
    {
        if (!isCollected)
        {
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
            
            // Check for hover-based collection
            CheckHoverCollection();
        }
    }

    void CheckHoverCollection()
    {
        // Get mouse position in world coordinates
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f; // Ensure same Z plane

        // Check distance between sun and mouse
        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        
        if (distance <= hoverCollectRadius)
        {
            StartCollection();
        }
    }

    // Keep OnMouseDown as fallback for direct clicks
    void OnMouseDown()
    {
        if (isCollected) return;
        StartCollection();
    }

    void StartCollection()
    {
        if (isCollected) return; // Prevent double collection
        
        if (autoDestroyRoutine != null) StopCoroutine(autoDestroyRoutine);
        StartCoroutine(CollectRoutine());
    }

    // Convert a RectTransform (UI) position into a world position suitable for the Sun to move to.
    Vector3 GetWorldPositionFromUI(RectTransform uiRect)
    {
        if (uiRect == null)
            return new Vector3(collectTarget.x, collectTarget.y, 0f);

        Canvas canvas = uiRect.GetComponentInParent<Canvas>();
        Camera uiCam = null;
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                uiCam = canvas.worldCamera;
            else if (canvas.renderMode == RenderMode.WorldSpace)
                return uiRect.position; // already world space
        }

        // get screen point of the UI element
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam, uiRect.position);

        // choose camera for ScreenToWorldPoint
        Camera worldCam = uiCam != null ? uiCam : Camera.main;
        if (worldCam == null)
            return new Vector3(collectTarget.x, collectTarget.y, 0f);

        float camZ = Mathf.Abs(worldCam.transform.position.z);
        Vector3 worldPoint = worldCam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, camZ));
        worldPoint.z = 0f;
        return worldPoint;
    }

    IEnumerator CollectRoutine()
    {
        isCollected = true;

        if (col2d != null) col2d.enabled = false;

        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
            rb2d.gravityScale = 0f;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }

        // try to find the UI CountText RectTransform from PlantManager
        RectTransform uiTargetRect = null;
        if (PlantManager.Instance != null && PlantManager.Instance.countText != null)
            uiTargetRect = PlantManager.Instance.countText.GetComponent<RectTransform>();
        else
        {
            // fallback: try to find GameObject named "SunCounter" or "CountText"
            var go = GameObject.Find("CountText") ?? GameObject.Find("SunCounter");
            if (go != null) uiTargetRect = go.GetComponent<RectTransform>();
        }

        // move toward dynamic UI position (recompute every frame to handle camera/UI movement)
        while (gameObject != null)
        {
            Vector3 targetWorld = GetWorldPositionFromUI(uiTargetRect);
            if (Vector3.Distance(transform.position, targetWorld) <= collectArrivalThreshold)
                break;

            transform.position = Vector3.MoveTowards(transform.position, targetWorld, collectSpeed * Time.deltaTime);
            yield return null;
        }

        PlantManager.Instance?.AddSun(sunValue);
        Destroy(gameObject);
    }

    IEnumerator AutoDestroyCoroutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isCollected) Destroy(gameObject);
    }

    // Visualize the hover collection radius in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Semi-transparent yellow
        Gizmos.DrawWireSphere(transform.position, hoverCollectRadius);
    }
}