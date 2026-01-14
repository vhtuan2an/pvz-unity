using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Displays game over screen when zombies win.
/// Attach to a Canvas GameObject in your scene.
/// </summary>
public class ZombieWinUI : MonoBehaviour
{
    public static ZombieWinUI Instance { get; private set; }

    [Header("Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;

    // Fallback: Create UI dynamically if not assigned in inspector
    private GameObject dynamicPanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Hide game over UI at start
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Shows the zombie win game over screen.
    /// </summary>
    public void ShowZombieWin()
    {
        Debug.Log("🧟 ZombieWinUI: Showing zombie win screen!");

        if (gameOverPanel != null && gameOverText != null)
        {
            // Use assigned UI elements
            gameOverText.text = "2AN DIES MID!";
            gameOverPanel.SetActive(true);
        }
        else
        {
            // Fallback: Create UI dynamically
            CreateDynamicZombieWinUI("2AN DIES MID!");
        }
    }

    /// <summary>
    /// Creates a dynamic game over UI if no panel is assigned.
    /// </summary>
    private void CreateDynamicZombieWinUI(string message)
    {
        // Clean up any existing dynamic panel
        if (dynamicPanel != null)
        {
            Destroy(dynamicPanel);
        }

        // Find or create Canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("GameOverCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Ensure it's on top
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create dark overlay panel
        dynamicPanel = new GameObject("GameOverPanel");
        dynamicPanel.transform.SetParent(canvas.transform, false);
        
        RectTransform panelRect = dynamicPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = dynamicPanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.85f); // Dark semi-transparent background

        // Create text
        GameObject textObj = new GameObject("GameOverText");
        textObj.transform.SetParent(dynamicPanel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(50, 50);
        textRect.offsetMax = new Vector2(-50, -50);

        TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = message;
        tmpText.fontSize = 72;
        tmpText.fontStyle = FontStyles.Bold;
        tmpText.color = Color.red;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.enableWordWrapping = true;

        Debug.Log("🧟 ZombieWinUI: Dynamic UI created successfully!");
    }

    /// <summary>
    /// Hides the game over panel.
    /// </summary>
    public void Hide()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (dynamicPanel != null)
        {
            Destroy(dynamicPanel);
            dynamicPanel = null;
        }
    }
}
