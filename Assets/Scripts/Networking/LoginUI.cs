using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class LoginUI : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signupPanel;

    [Header("Login Panel")]
    [SerializeField] private TMP_InputField loginUsernameInput;
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button showSignupButton;

    [Header("Signup Panel")]
    [SerializeField] private TMP_InputField signupUsernameInput;
    [SerializeField] private TMP_InputField signupPasswordInput;
    [SerializeField] private Button signupButton;
    // [SerializeField] private Button backToLoginButton;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    private void Start()
    {
        loginButton.onClick.AddListener(OnLoginButtonClicked);
        showSignupButton.onClick.AddListener(ShowSignupPanel);
        signupButton.onClick.AddListener(OnSignupButtonClicked);

        // Play background music for login scene
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayLoop("LoginBGM", "background music/Login Scene");
        }
    }

    private void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        signupPanel.SetActive(false);
        ClearFeedback();
    }

    private void ShowSignupPanel()
    {
        loginPanel.SetActive(false);
        signupPanel.SetActive(true);
        ClearFeedback();
    }

    private async void OnLoginButtonClicked()
    {
        string username = loginUsernameInput.text.Trim();
        string password = loginPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowErrorDialog("Please enter username and password.");
            return;
        }

        SetButtonsInteractable(false);
        ShowFeedback("Logging in...", false);

        try
        {
            await UnityAuthManager.Instance.SignInWithUsernamePasswordAsync(username, password);
        }
        catch (Exception ex)
        {
            ClearFeedback();
            ShowErrorDialog(ex.Message);
        }
        finally
        {
            SetButtonsInteractable(true);
        }
    }

    private async void OnSignupButtonClicked()
    {
        string username = signupUsernameInput.text.Trim();
        string password = signupPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowErrorDialog("Please enter username and password.");
            return;
        }

        if (password.Length < 8)
        {
            ShowErrorDialog("Password must be at least 8 characters.");
            return;
        }

        SetButtonsInteractable(false);
        ShowFeedback("Signing up...", false);

        try
        {
            await UnityAuthManager.Instance.SignUpWithUsernamePasswordAsync(username, password);
        }
        catch (Exception ex)
        {
            ClearFeedback();
            ShowErrorDialog(ex.Message);
        }
        finally
        {
            SetButtonsInteractable(true);
        }
    }

    private void ShowErrorDialog(string message)
    {
        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.ShowError(message);
        }
        else
        {
            ShowFeedback(message, true);
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginButton != null) loginButton.interactable = interactable;
        if (signupButton != null) signupButton.interactable = interactable;
        if (showSignupButton != null) showSignupButton.interactable = interactable;
        // if (backToLoginButton != null) backToLoginButton.interactable = interactable;
    }

    private void ShowFeedback(string message, bool isError)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.white;
        feedbackText.gameObject.SetActive(true);
    }

    private void ClearFeedback()
    {
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }
}