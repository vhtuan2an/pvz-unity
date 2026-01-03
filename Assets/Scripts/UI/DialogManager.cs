using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance { get; private set; }

    [Header("Dialog Panel")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button okButton;
    [SerializeField] private TMP_Text okButtonText;

    private Action onOkCallback;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (okButton != null)
        {
            okButton.onClick.AddListener(OnOkButtonClicked);
        }

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Show dialog with message
    /// </summary>
    public void ShowDialog(string message, string buttonText = "OK", Action onOk = null)
    {
        if (dialogPanel == null) return;

        if (messageText != null) messageText.text = message;
        if (okButtonText != null) okButtonText.text = buttonText;

        onOkCallback = onOk;
        dialogPanel.SetActive(true);
    }

    /// <summary>
    /// Show error dialog
    /// </summary>
    public void ShowError(string message, Action onOk = null)
    {
        ShowDialog(message, "OK", onOk);
    }

    /// <summary>
    /// Show info dialog
    /// </summary>
    public void ShowInfo(string message, Action onOk = null)
    {
        ShowDialog(message, "OK", onOk);
    }

    private void OnOkButtonClicked()
    {
        dialogPanel.SetActive(false);
        onOkCallback?.Invoke();
        onOkCallback = null;
    }

    public void HideDialog()
    {
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        onOkCallback = null;
    }
}