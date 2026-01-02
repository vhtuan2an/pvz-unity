using UnityEngine;
using System.Collections;

public class CameraIntroController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float plantXOffset = -4f;
    [SerializeField] private float zombieXOffset = 6f;
    [SerializeField] private float panDuration = 2.5f;
    [SerializeField] private AnimationCurve panCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector3 targetPosition; // Lawn
    private Quaternion targetRotation;
    
    // Selection state transforms
    private Vector3 startPosition;
    private Quaternion startRotation;
    
    private bool hasStartedPan = false;

    private void Awake()
    {
        // Assume current position in scene is the gameplay Lawn position
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    private void Start()
    {
        // Subscribe to state changes
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            
            // Initial check if joined late
            if (GameStateManager.Instance.CurrentState.Value == GameStateManager.GameState.Selection ||
                GameStateManager.Instance.CurrentState.Value == GameStateManager.GameState.Waiting)
            {
                 CalculateStartPosition();
                 MoveToStartPosition();
            }
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
        
        CalculateStartPosition();
        if (GameStateManager.Instance.CurrentState.Value == GameStateManager.GameState.Selection ||
            GameStateManager.Instance.CurrentState.Value == GameStateManager.GameState.Waiting)
        {
            MoveToStartPosition();
        }
    }

    private void CalculateStartPosition()
    {
        float xOffset = 0f;
        
        if (LobbyManager.Instance != null)
        {
             PlayerRole role = LobbyManager.Instance.SelectedRole;
             if (role == PlayerRole.Plant) xOffset = plantXOffset;
             else if (role == PlayerRole.Zombie) xOffset = zombieXOffset;
             else xOffset = 0f;
             
             Debug.Log($"CameraIntro: Calculated start pos for {role} with offset {xOffset}");
        }

        startPosition = targetPosition + new Vector3(xOffset, 0, 0);
        startRotation = targetRotation; 
    }

    private void MoveToStartPosition()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
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
        if (newState == GameStateManager.GameState.Selection || newState == GameStateManager.GameState.Waiting)
        {
            CalculateStartPosition();
            MoveToStartPosition();
            hasStartedPan = false;
        }
        else if (newState == GameStateManager.GameState.Intro && !hasStartedPan)
        {
            StartCoroutine(PanToLawnRoutine());
        }
    }

    private IEnumerator PanToLawnRoutine()
    {
        hasStartedPan = true;
        float elapsed = 0f;
        
        Debug.Log("Camera Pan Started...");

        while (elapsed < panDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / panDuration;
            float smoothT = panCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);

            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        
        Debug.Log("Camera Pan Finished!");

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ReportIntroFinishedServerRpc();
        }
    }
}
