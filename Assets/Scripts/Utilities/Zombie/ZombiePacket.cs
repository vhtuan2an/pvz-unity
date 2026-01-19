using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ZombiePacket : MonoBehaviour
{
    public GameObject zombiePrefab;

    public Image icon;
    public Button button;
    public Image cooldownOverlay;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI cooldownText;

    private bool onCooldown;
    private int brainCost;
    private float cooldown;

    void Start()
    {
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);
        if (button != null)
            button.onClick.AddListener(OnClicked);

        if (zombiePrefab != null)
        {
            RefreshUI();
        }

        // Register with Manager
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.RegisterZombiePacket(this);
        }
    }

    public void AssignZombie(GameObject prefab)
    {
        zombiePrefab = prefab;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (zombiePrefab != null)
        {
            var zombieBase = zombiePrefab.GetComponent<ZombieBase>();
            if (zombieBase != null)
            {
                brainCost = zombieBase.GetBrainCost();
                cooldown = zombieBase.cooldown;

                if (zombieBase.packetImage != null)
                    icon.sprite = zombieBase.packetImage;
            }

            if (icon.sprite == null)
            {
                var sr = zombiePrefab.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) icon.sprite = sr.sprite;
            }

            if (costText != null)
                costText.text = brainCost.ToString();
        }
    }

    void Update()
    {
        if (ZombieManager.Instance == null)
            return;

        if (ZombieManager.Instance.currentBrains.Value < brainCost)
        {
            if (ColorUtility.TryParseHtmlString("#EF696E", out Color redColor))
                costText.color = redColor;
        }
        else
        {
            costText.color = Color.white;
        }
    }

    void OnClicked()
    {
        // 1. Check Resources
        if (ZombieManager.Instance != null && ZombieManager.Instance.currentBrains.Value < brainCost)
        {
            SoundManager.Instance.PlaySound("oncooldown");
            return;
        }

        // 2. Check Cooldown
        if (onCooldown) 
        {
            SoundManager.Instance.PlaySound("oncooldown");
            return;
        }

        ZombieManager.Instance?.SelectZombie(zombiePrefab, brainCost, this);
    }

    public void StartCooldown()
    {
        if (onCooldown || cooldown <= 0f) return;
        StartCoroutine(CooldownRoutine());
    }
    
    public void SetDimmed(bool dimmed)
    {
        if (icon != null)
        {
            icon.color = dimmed ? Color.gray : Color.white;
        }
    }

    IEnumerator CooldownRoutine()
    {
        onCooldown = true;

        if (button != null)
            button.interactable = false;

        float remaining = cooldown;

        while (remaining > 0f)
        {
            float multiplier = (ZombieManager.Instance != null) ? ZombieManager.Instance.GlobalCooldownMultiplier : 1f;
            remaining -= Time.deltaTime / multiplier;

            if (cooldownOverlay != null)
                cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / cooldown);

            // Update cooldown text
            if (cooldownText != null)
            {
                if (!cooldownText.gameObject.activeSelf) cooldownText.gameObject.SetActive(true);
                cooldownText.text = remaining.ToString("F1");
            }

            yield return null;
        }

        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);
        if (button != null) button.interactable = true;

        onCooldown = false;
    }
}
