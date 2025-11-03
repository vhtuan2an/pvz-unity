using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text ownerText;
    [SerializeField] private TMP_Text ownerRoleText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Button joinButton;

    private string lobbyId;
    private System.Action<string> onJoin;

    public void Setup(string id, string displayName, string owner, string ownerRole, int players, int maxPlayers, System.Action<string> onJoinCallback)
    {
        lobbyId = id;
        lobbyNameText.text = displayName;
        ownerText.text = owner;
        ownerRoleText.text = ownerRole;
        countText.text = $"{players}/{maxPlayers}";
        onJoin = onJoinCallback;
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => onJoin?.Invoke(lobbyId));
    }
}