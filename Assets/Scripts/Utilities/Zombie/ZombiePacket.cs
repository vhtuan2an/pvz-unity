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

    private bool onCooldown;
    private int brainCost;
    private float cooldown;

    void Start()
    {
        cooldownOverlay.fillAmount = 0f;
        if (button != null)
            button.onClick.AddListener(OnClicked);

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

        if (ZombieManager.Instance.currentBrains < brainCost)
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

    void OnClicked()
    {
        if (onCooldown) return;
        ZombieManager.Instance?.SelectZombie(zombiePrefab, brainCost, this);
    }

    public void StartCooldown()
    {
        if (onCooldown || cooldown <= 0f) return;
        StartCoroutine(CooldownRoutine());
    }

    IEnumerator CooldownRoutine()
    {
        onCooldown = true;

        if (button != null)
            button.interactable = false;

        float remaining = cooldown;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;

            if (cooldownOverlay != null)
                cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / cooldown);

            yield return null;
        }

        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (button != null) button.interactable = true;

        onCooldown = false;
    }
}
