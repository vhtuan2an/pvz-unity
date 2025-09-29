using System.Collections;
using PlayFab;
using PlayFab.MultiplayerModels;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class MatchMaking : MonoBehaviour
{
    [SerializeField] private GameObject playBtn;
    [SerializeField] private GameObject cancelBtn;
    [SerializeField] private TMP_Text statusText;

    private const string queueName = "pvz-unity";
    private string ticketId;
    private Coroutine pollingCoroutine;

    public void StartMatchmaking()
    {
        playBtn.SetActive(false);
        cancelBtn.SetActive(true);
        statusText.text = "Finding Match...";
        statusText.gameObject.SetActive(true);
        // Gọi API PlayFab để bắt đầu tìm trận đấu

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
                        DataObject = new { } // Ví dụ về kỹ năng
                    }
                },
                GiveUpAfterSeconds = 60,
                QueueName = queueName
            },
            onMatchmakingTicketCreated,
            onMatchmakingError
        );
    }

    private void onMatchmakingTicketCreated(CreateMatchmakingTicketResult result)
    {
        Debug.Log("Matchmaking ticket created: " + result.TicketId);
        statusText.text = "Matchmaking in progress...";
        ticketId = result.TicketId;
        pollingCoroutine = StartCoroutine(PollTicket());
    }

    private void onMatchmakingError(PlayFabError error)
    {
        Debug.LogError("Error creating matchmaking ticket: " + error.GenerateErrorReport());
        statusText.text = "Error starting matchmaking.";
        playBtn.SetActive(true);
        cancelBtn.SetActive(false);
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
        statusText.text = $"{result.Members[0].Entity.Id} vs {result.Members[1].Entity.Id}";
    }
}
