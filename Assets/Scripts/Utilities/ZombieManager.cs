using UnityEngine;
using TMPro;

public class ZombieManager : MonoBehaviour
{
    public static ZombieManager Instance { get; private set; }

    [Header("Resources")]
    public int currentBrains = 0;

    [Header("UI")]
    public TextMeshProUGUI brainCounterText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateBrainUI();
    }

    public void AddBrains(int amount)
    {
        currentBrains += amount;
        UpdateBrainUI();
    }

    public void SpendBrains(int amount)
    {
        currentBrains -= amount;
        if (currentBrains < 0) currentBrains = 0;
        UpdateBrainUI();
    }

    private void UpdateBrainUI()
    {
        if (brainCounterText != null)
            brainCounterText.text = currentBrains.ToString();
    }

    // Hàm này sẽ được gọi từ Brain khi collect xong
    public void OnBrainCollected(int value)
    {
        AddBrains(value);
    }
}
