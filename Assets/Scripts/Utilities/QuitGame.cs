using UnityEngine;

/// <summary>
/// Simple script to quit the application.
/// Attach this to a Quit button and call QuitApplication() via OnClick.
/// </summary>
public class QuitGame : MonoBehaviour
{
    /// <summary>
    /// Quits the application. Works in both Editor and Build.
    /// </summary>
    public void QuitApplication()
    {
        Debug.Log("[QuitGame] Quitting application...");
        
#if UNITY_EDITOR
        // Stop playing in Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit the built application
        Application.Quit();
#endif
    }
}
