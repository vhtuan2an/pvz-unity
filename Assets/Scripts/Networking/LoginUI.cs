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
    [SerializeField] private Button backToLoginButton;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    private void Start()
    {
        // Setup button listeners
        loginButton.onClick.AddListener(OnLoginButtonClicked);
        showSignupButton.onClick.AddListener(ShowSignupPanel);
        
        signupButton.onClick.AddListener(OnSignupButtonClicked);
        backToLoginButton.onClick.AddListener(ShowLoginPanel);

        // Show login panel by default
        ShowLoginPanel();
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
            ShowFeedback("Please enter both username and password", true);
            return;
        }

        try
        {
            await UnityAuthManager.Instance.SignInWithUsernamePasswordAsync(username, password);
            ShowFeedback("Login successful!", false);
        }
        catch (Exception ex)
        {
            ShowFeedback($"Login failed: {ex.Message}", true);
        }
    }

    private async void OnSignupButtonClicked()
    {
        string username = signupUsernameInput.text.Trim();
        string password = signupPasswordInput.text;

        // Validation
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Please enter both username and password", true);
            return;
        }

        if (password.Length < 8)
        {
            ShowFeedback("Password must be at least 8 characters", true);
            return;
        }

        try
        {
            await UnityAuthManager.Instance.SignUpWithUsernamePasswordAsync(username, password);
            ShowFeedback("Signup successful!", false);
        }
        catch (Exception ex)
        {
            ShowFeedback($"Signup failed: {ex.Message}", true);
        }
    }

    private void ShowFeedback(string message, bool isError)
    {
        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.green;
        feedbackText.gameObject.SetActive(true);
    }

    private void ClearFeedback()
    {
        feedbackText.gameObject.SetActive(false);
    }
}