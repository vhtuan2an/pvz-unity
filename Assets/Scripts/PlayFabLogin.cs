using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayFabLogin : MonoBehaviour
{
    [SerializeField] private GameObject signInDisplay = default;
    [SerializeField] private TMP_InputField usernameInputField = default;
    [SerializeField] private TMP_InputField emailInputField = default;
    [SerializeField] private TMP_InputField passwordInputField = default;

    public static string SessionTicket;
    public static string EntityId;
    public static string PlayFabId;

    public void CreateAccount()
    {
        PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
        {
            Username = usernameInputField.text,
            Email = emailInputField.text,
            Password = passwordInputField.text
        }, result =>
        {
            SessionTicket = result.SessionTicket;
            EntityId = result.EntityToken.Entity.Id;
            PlayFabId = result.PlayFabId;
            signInDisplay.SetActive(false);
            SceneManager.LoadScene("LobbyScene");
        }, error =>
        {
            Debug.LogError(error.GenerateErrorReport());
        });
    }

    public void SignIn()
    {
        PlayFabClientAPI.LoginWithPlayFab(new LoginWithPlayFabRequest
        {
            Username = usernameInputField.text,
            Password = passwordInputField.text
        }, result =>
        {
            SessionTicket = result.SessionTicket;
            EntityId = result.EntityToken.Entity.Id;
            PlayFabId = result.PlayFabId;
            signInDisplay.SetActive(false);
            SceneManager.LoadScene("LobbyScene");
        }, error =>
        {
            Debug.LogError(error.GenerateErrorReport());
        });
    }

    // Thêm method này vào class MatchMaking để lấy display name
private void GetPlayerProfile()
{
    PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest()
    {
        PlayFabId = PlayFabLogin.PlayFabId,
        ProfileConstraints = new PlayerProfileViewConstraints()
        {
            ShowDisplayName = true
        }
    },
    result => {
        string displayName = result.PlayerProfile.DisplayName ?? "Player";
        // Use this displayName in matchmaking
    },
    error => {
        Debug.LogError("Error getting player profile: " + error.GenerateErrorReport());
    });
}
}