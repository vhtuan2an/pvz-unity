using System.Collections;
using PlayFab;
using PlayFab.MultiplayerModels;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PlayerRole
{
    Plant,
    Zombie
}

public class MatchMaking : MonoBehaviour
{
    [SerializeField] private GameObject playBtn;
    [SerializeField] private GameObject cancelBtn;
    [SerializeField] private TMP_Text statusText;
    
    // UI elements for role selection
    [SerializeField] private GameObject roleSelectionPanel;
    [SerializeField] private Button plantBtn;
    [SerializeField] private Button zombieBtn;
    [SerializeField] private TMP_Text selectedRoleText;

    private const string queueName = "pvz-unity";
    private string ticketId;
    private Coroutine pollingCoroutine;
    private PlayerRole selectedRole;

    private void Start()
    {
        // Setup role selection buttons
        plantBtn.onClick.AddListener(() => SelectRole(PlayerRole.Plant));
        zombieBtn.onClick.AddListener(() => SelectRole(PlayerRole.Zombie));
        
        // Initially show role selection
        roleSelectionPanel.SetActive(true);
        playBtn.SetActive(false);
    }

    private void SelectRole(PlayerRole role)
    {
        selectedRole = role;
        selectedRoleText.text = $"Selected: {role}";
        
        // Show play button after role selection
        playBtn.SetActive(true);
        roleSelectionPanel.SetActive(false);
    }

    public void StartMatchmaking()
    {
        playBtn.SetActive(false);
        cancelBtn.SetActive(true);
        statusText.text = "Finding Match...";
        statusText.gameObject.SetActive(true);

        // Create matchmaking ticket with role-based attributes
        PlayFabMultiplayerAPI.CreateMatchmakingTicket(
            new CreateMatchmakingTicketRequest
            {
                Creator = new MatchmakingPlayer
                {
                    Entity = new EntityKey
                    {
                        Id = PlayFabLogin.EntityId, 
                        Type = "title_player_account"
                    },
                    Attributes = new MatchmakingPlayerAttributes
                    {
                        DataObject = new { 
                            role = selectedRole.ToString().ToLower(),
                            skill = 1000 // Example skill rating
                        }
                    }
                },
                GiveUpAfterSeconds = 60,
                QueueName = queueName
            },
            onMatchmakingTicketCreated,
            onMatchmakingError
        );
    }

    public void CancelMatchmaking()
    {
        if (!string.IsNullOrEmpty(ticketId))
        {
            PlayFabMultiplayerAPI.CancelMatchmakingTicket(
                new CancelMatchmakingTicketRequest
                {
                    QueueName = queueName,
                    TicketId = ticketId
                },
                result => {
                    Debug.Log("Matchmaking cancelled successfully");
                    ResetUI();
                },
                error => {
                    Debug.LogError("Error cancelling matchmaking: " + error.GenerateErrorReport());
                    ResetUI();
                }
            );
        }
        else
        {
            ResetUI();
        }
    }

    private void ResetUI()
    {
        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
        
        ticketId = "";
        playBtn.SetActive(true);
        cancelBtn.SetActive(false);
        statusText.gameObject.SetActive(false);
    }

    private void onMatchmakingTicketCreated(CreateMatchmakingTicketResult result)
    {
        Debug.Log("Matchmaking ticket created: " + result.TicketId);
        statusText.text = $"Finding {(selectedRole == PlayerRole.Plant ? "Zombie" : "Plant")} opponent...";
        ticketId = result.TicketId;
        pollingCoroutine = StartCoroutine(PollTicket());
    }

    private void onMatchmakingError(PlayFabError error)
    {
        Debug.LogError("Error creating matchmaking ticket: " + error.GenerateErrorReport());
        statusText.text = "Error starting matchmaking.";
        ResetUI();
    }
    
    private IEnumerator PollTicket()
    {
        while (true)
        {
            PlayFabMultiplayerAPI.GetMatchmakingTicket(
                new GetMatchmakingTicketRequest
                {
                    TicketId = ticketId,
                    QueueName = queueName
                },
                onGetMatchmakingTicket,
                onMatchmakingError
            );
            yield return new WaitForSeconds(3);
        }
    }
    
    private void onGetMatchmakingTicket(GetMatchmakingTicketResult result)
    {
        statusText.text = "Matchmaking Status: " + result.Status;
        switch (result.Status)
        {
            case "Matched":
                StopCoroutine(pollingCoroutine);
                StartMatch(result.MatchId);
                break;

            case "Canceled":
                ResetUI();
                break;
        }
    }
    
    private void StartMatch(string matchId)
    {
        statusText.text = "Match found! Joining...";

        PlayFabMultiplayerAPI.GetMatch(
            new GetMatchRequest
            {
                MatchId = matchId,
                QueueName = queueName,
            },
            onGetMatch,
            onMatchmakingError
        );
    }
    
    private void onGetMatch(GetMatchResult result)
    {
        var opponent = result.Members.Find(m => m.Entity.Id != PlayFabLogin.EntityId);
        if (opponent != null)
        {
            // Get opponent's display name
            string opponentName = "Unknown Player";

            // Try to get display name from opponent's attributes
            if (opponent.Attributes?.DataObject != null)
            {
                // Cast to dictionary to use ContainsKey
                if (opponent.Attributes.DataObject is System.Collections.Generic.Dictionary<string, object> attributes)
                {
                    if (attributes.ContainsKey("displayName"))
                    {
                        opponentName = attributes["displayName"].ToString();
                    }
                }
            }

            // If no display name in attributes, use Entity ID as fallback
            if (opponentName == "Unknown Player")
            {
                opponentName = opponent.Entity.Id.Substring(0, 8) + "..."; // Show first 8 chars of ID
            }

            statusText.text = $"You ({selectedRole}) vs {opponentName}";

            // Store match information for game session
            PlayerPrefs.SetString("PlayerRole", selectedRole.ToString());
            PlayerPrefs.SetString("MatchId", result.MatchId);
            PlayerPrefs.SetString("OpponentName", opponentName);
            
            // Load game scene or start game logic
            SceneManager.LoadScene("GameScene");
        }
    }
}
