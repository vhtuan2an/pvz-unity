using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BossHealthBar : MonoBehaviour
{
    public static BossHealthBar Instance { get; private set; }

    [Header("UI References")]
    public GameObject container;
    public Slider healthSlider;
    public TMP_Text bossNameText;
    public Image headIcon;

    [Header("Settings")]
    public float smoothSpeed = 5f;
    public Color barColor = new Color(1f, 0.2f, 0.2f); // Boss Red

    private float targetFill = 1f;
    private bool isVisible = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // If user hasn't set up UI in Inspector, build it at runtime
        if (container == null)
        {
            CreateDefaultUI();
        }

        if (container != null) container.SetActive(false);
    }

#if UNITY_EDITOR
    [MenuItem("GameObject/UI/Boss Health Bar", false, 10)]
    public static void CreateBossHealthBar(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("Boss Health Bar");
        BossHealthBar bhb = go.AddComponent<BossHealthBar>();
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
        bhb.CreateDefaultUI();
    }
#endif

    private void CreateDefaultUI()
    {
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null) canvasObj = new GameObject("BossHealthCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        
        container = new GameObject("BossHealthBarContainer", typeof(RectTransform), typeof(Image));
        container.transform.SetParent(canvasObj.transform, false);
        
        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0); 
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-20, 20); 
        rect.sizeDelta = new Vector2(304, 44); 

        container.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        GameObject bg = new GameObject("Background", typeof(Image));
        bg.transform.SetParent(container.transform, false);
        bg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        bg.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 40);

        GameObject sliderObj = new GameObject("HealthSlider", typeof(Slider));
        sliderObj.transform.SetParent(bg.transform, false);
        healthSlider = sliderObj.GetComponent<Slider>();
        
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(280, 20);
        sliderRect.anchoredPosition = new Vector2(0, 0);

        healthSlider.direction = Slider.Direction.LeftToRight;
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        fillArea.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0); 
        fillArea.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        fillArea.GetComponent<RectTransform>().anchorMax = Vector2.one;
        
        GameObject fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = barColor;
        
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.sizeDelta = Vector2.zero;
        
        healthSlider.fillRect = fillRect;
        healthSlider.interactable = false;
        healthSlider.transition = Selectable.Transition.None;

        GameObject textObj = new GameObject("BossName", typeof(TextMeshProUGUI));
        textObj.transform.SetParent(container.transform, false);
        bossNameText = textObj.GetComponent<TextMeshProUGUI>();
        bossNameText.fontSize = 18;
        bossNameText.fontStyle = FontStyles.Bold;
        bossNameText.alignment = TextAlignmentOptions.Right;
        bossNameText.text = "BOSS: YOUR MOM";
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(300, 30);
        textRect.anchoredPosition = new Vector2(0, 35);

        GameObject headObj = new GameObject("HeadImage", typeof(Image));
        headObj.transform.SetParent(container.transform, false);
        headIcon = headObj.GetComponent<Image>();
        headIcon.color = new Color(1, 1, 1, 0.5f);
        
        RectTransform headRect = headObj.GetComponent<RectTransform>();
        headRect.anchorMin = new Vector2(0, 0.5f);
        headRect.anchorMax = new Vector2(0, 0.5f);
        headRect.pivot = new Vector2(0.5f, 0.5f);
        headRect.sizeDelta = new Vector2(60, 60);
        headRect.anchoredPosition = new Vector2(-10, 0);
    }

    private void Update()
    {
        // STRICT VISIBILITY RULE: Only show if State is Playing or Paused
        bool shouldBeVisible = false;
        if (GameStateManager.Instance != null)
        {
            GameStateManager.GameState state = GameStateManager.Instance.CurrentState.Value;
            shouldBeVisible = (state == GameStateManager.GameState.Playing || state == GameStateManager.GameState.Paused);
        }

        // Additional check: Don't show if boss is dead or missing
        if (YourMomZombie.Instance == null || YourMomZombie.Instance.isDead)
        {
            shouldBeVisible = false;
        }

        if (isVisible != shouldBeVisible)
        {
            SetVisible(shouldBeVisible);
        }

        if (isVisible)
        {
            UpdateHealth();
        }
    }

    private void UpdateHealth()
    {
        if (YourMomZombie.Instance == null) return;

        float currentFill = YourMomZombie.Instance.GetHealthPercentage();
        targetFill = Mathf.Lerp(targetFill, currentFill, Time.deltaTime * smoothSpeed);
        
        if (healthSlider != null)
        {
            healthSlider.value = targetFill;
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (container != null)
        {
            container.SetActive(visible);
        }

        if (visible && bossNameText != null)
        {
            bossNameText.text = "YOUR MOM";
        }
    }
}
