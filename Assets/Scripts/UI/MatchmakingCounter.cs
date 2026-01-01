using UnityEngine;
using TMPro;
using System;

public class MatchmakingCounter : MonoBehaviour
{
    [Header("Display Settings")]
    [SerializeField] private string displayFormat = "{0:00}:{1:00}";
    
    [SerializeField] private string prefixText = "";
    
    [SerializeField] private string suffixText = "";

    [Header("Auto Settings")]
    [SerializeField] private bool startOnEnable = true;
    
    [SerializeField] private bool resetOnStart = true;

    private TMP_Text counterText;
    private float elapsedTime = 0f;
    private bool isCounting = false;

    private void Awake()
    {
        counterText = GetComponent<TMP_Text>();
        if (counterText == null)
        {
            Debug.LogError("MatchmakingCounter: Không tìm thấy TMP_Text component trên GameObject này!");
        }
    }

    private void OnEnable()
    {
        if (startOnEnable)
        {
            StartCounting();
        }
    }

    private void OnDisable()
    {
        StopCounting();
    }

    private void Update()
    {
        if (isCounting)
        {
            elapsedTime += Time.deltaTime;
            UpdateDisplay();
        }
    }

    public void StartCounting()
    {
        if (resetOnStart)
        {
            elapsedTime = 0f;
        }
        isCounting = true;
        UpdateDisplay();
    }

    public void StopCounting()
    {
        isCounting = false;
    }
    
    public void ResetCounter()
    {
        elapsedTime = 0f;
        UpdateDisplay();
    }

    public float GetElapsedTime() => elapsedTime;
    public TimeSpan GetElapsedTimeSpan() => TimeSpan.FromSeconds(elapsedTime);

    public bool IsCounting => isCounting;
    public void SetDisplayFormat(string format)
    {
        displayFormat = format;
        UpdateDisplay();
    }

    public void SetPrefixText(string prefix)
    {
        prefixText = prefix;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (counterText == null) return;

        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);

        string timeString = string.Format(displayFormat, minutes, seconds);
        counterText.text = $"{prefixText}{timeString}{suffixText}";
    }
}
