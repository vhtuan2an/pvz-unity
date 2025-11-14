using UnityEngine;

public class TestModeManager : MonoBehaviour
{
    public static TestModeManager Instance { get; private set; }
    
    [Header("Test Mode Settings")]
    [SerializeField] private bool enableTestMode = true; // ✅ Bật/tắt test mode
    
    public bool IsTestMode => enableTestMode;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        #if !UNITY_EDITOR
        // ✅ Tự động tắt test mode trong build
        enableTestMode = false;
        #endif
    }
}