using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text ownerText;
    [SerializeField] private TMP_Text ownerRoleText;
    [SerializeField] private Button joinButton;

    private string lobbyId;
    private System.Action<string> onJoin;

    public void Setup(string id, string displayName, string owner, string ownerRole, System.Action<string> onJoinCallback)
    {
        lobbyId = id;
        lobbyNameText.text = $"ROOM ID: {displayName}"; // Updated format
        ownerText.text = owner; // Owner name remains as is or can be hidden if not in design
        ownerRoleText.text = $"ROLE: {ownerRole}"; // Updated format
        onJoin = onJoinCallback;
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => onJoin?.Invoke(lobbyId));
    }
    [ContextMenu("Fix Layout")]
    public void FixLayout()
    {
        // 1. Adjust Background (Self)
        var rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(400, 150); // Set a reasonable default size for the board
        }

        // 2. Hide Owner Name if not needed (as per target design) or move it
        if (ownerText != null)
        {
            ownerText.gameObject.SetActive(false); // Hide it to match the clean target design
        }

        // 3. Adjust Text Elements to prevent squashing
        // Assuming Texts are children. We should ensure they have enough width.
        AdjustTextLayout(lobbyNameText, -50, 30);
        AdjustTextLayout(ownerRoleText, -50, -30);
    }

    private void AdjustTextLayout(TMP_Text textComp, float xOffset, float yOffset)
    {
        if (textComp != null)
        {
            textComp.enableAutoSizing = true;
            textComp.fontSizeMin = 18;
            textComp.fontSizeMax = 32;
            textComp.alignment = TextAlignmentOptions.Left;
            
            var rt = textComp.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(250, 40); // Give plenty of width
            rt.anchoredPosition = new Vector2(xOffset, yOffset); // Position relative to center
        }
    }
}