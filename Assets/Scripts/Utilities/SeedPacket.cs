using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class SeedPacket : MonoBehaviour
{
    public GameObject plantPrefab;

    public Image icon;
    public Button button;
    public Image cooldownOverlay;
    public TextMeshProUGUI costText;

    private bool onCooldown;
    private int sunCost;
    private float cooldown;

    void Start()
    {
        // Add button click listener
        if (button != null)
            button.onClick.AddListener(OnClicked);

        // Fetch sun cost, cooldown, and icon dynamically from the plant prefab
        if (plantPrefab != null)
        {
            var plantBase = plantPrefab.GetComponent<PlantBase>();
            if (plantBase != null)
            {
                // prefer explicit seed icon from PlantBase
                if (plantBase.packetImage != null)
                    icon.sprite = plantBase.packetImage;

                sunCost = plantBase.sunCost;
                cooldown = plantBase.cooldown;
            }

            // fallback: use sprite renderer on prefab if no packetImage assigned
            if (icon.sprite == null)
            {
                var sr = plantPrefab.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) icon.sprite = sr.sprite;
            }

            if (costText != null)
                costText.text = sunCost.ToString();
        }
    }

    void Update()
    {
        // Check if there’s enough sun to plant
        if (PlantManager.Instance != null)
        {
            if (PlantManager.Instance.currentSun < sunCost)
            {
                if (ColorUtility.TryParseHtmlString("#EF696E", out Color redColor))
                    costText.color = redColor;
                costText.outlineColor = Color.black;
            }
            else
            {
                costText.color = Color.white;
                costText.outlineColor = Color.black;
            }
        }
    }

    void OnClicked()
    {
        // Prevent selection if on cooldown
        if (onCooldown) return;

        // Pass the plant prefab, sun cost, and this seed packet to PlantManager
        PlantManager.Instance?.SelectPlant(plantPrefab, sunCost, this);
    }

    public void StartCooldown()
    {
        // Start cooldown if applicable
        if (onCooldown || cooldown <= 0f) return;
        StartCoroutine(CooldownRoutine());
    }

    IEnumerator CooldownRoutine()
    {
        onCooldown = true;

        // Disable button interaction during cooldown
        if (button != null) button.interactable = false;

        float remaining = cooldown;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;

            // Update cooldown overlay fill amount
            if (cooldownOverlay != null)
                cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / cooldown);

            yield return null;
        }

        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (button != null) button.interactable = true;

        onCooldown = false;
    }
}