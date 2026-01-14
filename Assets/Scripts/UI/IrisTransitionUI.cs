using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro
using System.Collections;
using Unity.Netcode;

public class IrisTransitionUI : MonoBehaviour
{
    public static IrisTransitionUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Image irisImage; // Assign Image using IrisHole material
    [SerializeField] private TextMeshProUGUI winMessageText;
    [SerializeField] private GameObject contentParent; // Parent to toggle generic visibility

    [Header("Win Screens")]
    [SerializeField] private GameObject plantWinScreen;
    [SerializeField] private GameObject zombieWinScreen;

    [Header("Game HUD")]
    [SerializeField] private GameObject[] HUDsHidden; // Drag PlantPanel and ZombiePanel here

    [Header("Settings")]
    [SerializeField] private float transitionDuration = 2.0f;
    [SerializeField] private float holdDuration = 1.0f;
    [SerializeField] private float initialRadius = 1.5f;
    [SerializeField] private float targetRadius = 0f; // How small does it get? (0 = full black)

    private Material irisMaterial;
    private Camera mainCamera;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Ensure UI starts hidden
        if (contentParent) contentParent.SetActive(false);
        if (plantWinScreen) plantWinScreen.SetActive(false);
        if (zombieWinScreen) zombieWinScreen.SetActive(false);

        // Hide the Black Overlay initially so it doesn't block the screen
        if (irisImage != null)
        {
            irisImage.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
        EnsureMaterial();
    }

    private void EnsureMaterial()
    {
        if (irisMaterial == null && irisImage != null)
        {
            irisMaterial = Instantiate(irisImage.material);
            irisImage.material = irisMaterial;
        }
    }

    public void PlayTransition(Vector3 targetWorldPos, PlayerRole winner)
    {
        if (contentParent) contentParent.SetActive(true);
        if (winMessageText) winMessageText.text = "";
        if (plantWinScreen) plantWinScreen.SetActive(false);
        if (zombieWinScreen) zombieWinScreen.SetActive(false);

        // Hide Game HUDs
        if (HUDsHidden != null)
        {
            foreach (var hud in HUDsHidden)
            {
                if (hud) hud.SetActive(false);
            }
        }
        
        // Show Overlay
        if (irisImage != null) irisImage.gameObject.SetActive(true);

        EnsureMaterial();
        StartCoroutine(IrisRoutine(targetWorldPos, winner));
    }

    public void PlayTransitionCentered(PlayerRole winner)
    {
        if (mainCamera != null)
        {
            PlayTransition(mainCamera.transform.position + mainCamera.transform.forward * 5f, winner);
        }
        else
        {
             PlayTransition(Vector3.zero, winner);
        }
    }

    // Flag to wait for input before returning to lobby
    private bool isWaitInput = false;

    private void Update()
    {
        if (isWaitInput && Input.anyKeyDown)
        {
            isWaitInput = false; // Prevent multiple calls
            Debug.Log("Victory Input Detected: Returning to Lobby...");
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.QuitGameServerRpc();
            }
        }
    }

    private IEnumerator IrisRoutine(Vector3 targetWorldPos, PlayerRole winner)
    {
        isWaitInput = false;

        // PHASE 1: Close Iris on Target (Focus on Winner)
        float timer = 0f;
        SetIrisWorld(targetWorldPos, initialRadius);

        // Animate Closing
        while (timer < transitionDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / transitionDuration;
            // Ease Out
            float currentRadius = Mathf.Lerp(initialRadius, targetRadius, t);
            SetIrisWorld(targetWorldPos, currentRadius);
            yield return null;
        }

        // Fully Closed (or at Target Radius)
        SetIrisWorld(targetWorldPos, targetRadius);
        
        // Wait briefly
        yield return new WaitForSecondsRealtime(holdDuration);

        // PHASE 2: Reveal Victory Screen (Iris Open from Center)
        
        // 1. Enable Visuals (Behind the black iris)
        if (winner == PlayerRole.Zombie && zombieWinScreen != null) zombieWinScreen.SetActive(true);
        if (winner == PlayerRole.Plant && plantWinScreen != null) plantWinScreen.SetActive(true);
        if (winMessageText != null) winMessageText.text = winner == PlayerRole.Zombie ? "ZOMBIES WIN!" : "PLANTS WIN!";
        if (NetworkGameManager.Instance != null)
        {
            if (winner == PlayerRole.Zombie)
                NetworkGameManager.Instance.PlaySoundClientRpc("zombie_win");
            else
                NetworkGameManager.Instance.PlaySoundClientRpc("plant_win");
        }

        // 2. Center Iris
        // We set the center to 0.5, 0.5 (Screen Center)
        Vector2 centerUV = new Vector2(0.5f, 0.5f);
        timer = 0f;

        // Animate Opening
        while (timer < transitionDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / transitionDuration;
            float currentRadius = Mathf.Lerp(targetRadius, initialRadius, t); // Expanding from Target
            
            SetIrisUV(centerUV, currentRadius);
            yield return null;
        }

        // Fully Open
        SetIrisUV(centerUV, initialRadius);
        Debug.Log($"Iris Sequence Complete. Winner: {winner}");
        
        // Enable Input
        isWaitInput = true;
    }

    // Helper: Sets Iris based on World Position
    private void SetIrisWorld(Vector3 worldTarget, float radius)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        
        Vector3 screenPos = mainCamera.WorldToViewportPoint(worldTarget);
        SetIrisUV(new Vector2(screenPos.x, screenPos.y), radius);
    }

    // Helper: Sets Iris based on UV (0-1) Position
    private void SetIrisUV(Vector2 uvCenter, float radius)
    {
        if (irisMaterial == null) return;

        // Aspect Ratio
        float aspect = (float)Screen.width / Screen.height;
        irisMaterial.SetFloat("_Aspect", aspect);

        // Set Center & Radius
        irisMaterial.SetVector("_Center", new Vector4(uvCenter.x, uvCenter.y, 0, 0));
        irisMaterial.SetFloat("_Radius", radius);
    }
}
