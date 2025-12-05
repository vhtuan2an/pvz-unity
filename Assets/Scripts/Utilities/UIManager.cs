using UnityEngine;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public GameObject plantPanel;
    public GameObject zombiePanel;

    void Start()
    {
        StartCoroutine(WaitForRoleSelection());
    }

    IEnumerator WaitForRoleSelection()
    {
        // Chờ LobbyManager sẵn sàng và role được chọn
        while (LobbyManager.Instance == null || LobbyManager.Instance.SelectedRole == PlayerRole.None)
        {
            yield return null;
        }

        // Sau khi role đã chọn, bật panel tương ứng
        switch (LobbyManager.Instance.SelectedRole)
        {
            case PlayerRole.Plant:
                if (plantPanel != null) plantPanel.SetActive(true);
                if (zombiePanel != null) zombiePanel.SetActive(false);
                break;

            case PlayerRole.Zombie:
                if (plantPanel != null) plantPanel.SetActive(false);
                if (zombiePanel != null) zombiePanel.SetActive(true);
                break;
        }
    }
}
