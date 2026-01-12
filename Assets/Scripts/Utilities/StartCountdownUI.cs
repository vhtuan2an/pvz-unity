using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StartCountdownUI : MonoBehaviour
{
    public static StartCountdownUI Instance { get; private set; }

    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_FontAsset customFont;

    // Create UI dynamically if not assigned in inspector
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
        if (countdownPanel != null)
            countdownPanel.SetActive(false);
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        }
        else
        {
            StartCoroutine(WaitForManager());
        }
    }

    private IEnumerator WaitForManager()
    {
        while (GameStateManager.Instance == null) yield return null;
        GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameStateManager.GameState newState)
    {
        if (newState == GameStateManager.GameState.Countdown)
        {
            StartCountdown();
        }
    }

    public void UpdateCountdown(string text)
    {
        EnsureUI();
        if (countdownPanel != null) countdownPanel.SetActive(true);
        if (countdownText != null)
        {
            countdownText.text = text;
            countdownText.fontSize = 90;
            countdownText.fontStyle = FontStyles.Bold;
            countdownText.color = Color.red;
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.enableWordWrapping = true;
            if (customFont != null && countdownText.font != customFont)
            {
                countdownText.font = customFont;
            }
            Material mat = countdownText.fontMaterial;
            if (mat != null)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            }
        }
    }

    public void ShowStartMessage()
    {
        string startText = "START!";

        if (LobbyManager.Instance != null)
        {
            if (LobbyManager.Instance.SelectedRole == PlayerRole.Plant)
                startText = "PLANT!";
            else if (LobbyManager.Instance.SelectedRole == PlayerRole.Zombie)
                startText = "ZOMBIE!";
        }

        UpdateCountdown(startText);
    }

    public void Hide()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        if (dynamicPanel != null)
        {
            Destroy(dynamicPanel);
            dynamicPanel = null;
        }
    }

    private void EnsureUI()
    {
        if (countdownPanel != null && countdownText != null) return;
        CreateDynamicCountdownUI();
    }

    private void CreateDynamicCountdownUI()
    {
        if (dynamicPanel != null) return;
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("CountdownCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // On top
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create dark overlay panel
        dynamicPanel = new GameObject("CountdownPanel");
        dynamicPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = dynamicPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = dynamicPanel.AddComponent<Image>();
        panelImage.color = Color.clear;

        // Create text
        GameObject textObj = new GameObject("CountdownText");
        textObj.transform.SetParent(dynamicPanel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(50, 50);
        textRect.offsetMax = new Vector2(-50, -50);

        TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.fontSize = 90;
        tmpText.fontStyle = FontStyles.Bold;
        tmpText.color = Color.red;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.enableWordWrapping = true;
        
        if (customFont != null)
        {
            tmpText.font = customFont;
        }

        countdownPanel = dynamicPanel;
        countdownText = tmpText;
    }

    public void StartCountdown(int startNumber = 3)
    {
        StartCoroutine(CountdownRoutine());
    }

    public void StartResumeCountdown()
    {
        StartCoroutine(ResumeRoutine());
    }



    private IEnumerator ResumeRoutine()
    {
        string[] sequence = { "3...", "2...", "1..." };
        NetworkGameManager.Instance.PlaySoundClientRpc("countdown");
        foreach (var msg in sequence)
        {
            UpdateCountdown(msg);
            yield return new WaitForSecondsRealtime(1f);
        }
        
        Hide();
    }

    private IEnumerator CountdownRoutine()
    {   
        
        string[] sequence = { "Ready...", "Set..." };
        NetworkGameManager.Instance.PlaySoundClientRpc("countdown");
        foreach (var msg in sequence)
        {
            UpdateCountdown(msg);
            yield return new WaitForSeconds(1f);
        }

        ShowStartMessage();
        yield return new WaitForSeconds(1f);

        Hide();
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ReportCountdownFinishedServerRpc();
        }
    }
}
