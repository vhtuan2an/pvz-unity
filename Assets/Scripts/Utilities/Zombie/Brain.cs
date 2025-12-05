using System.Collections;
using UnityEngine;

public class Brain : MonoBehaviour
{
    [Header("Settings")]
    public int brainValue = 25;
    public float lifetime = 10f;
    public float rotationSpeed = 50f;
    public float hoverCollectRadius = 1.5f;
    public Vector2 collectTarget = new Vector2(8.9f, 3.7f);

    private bool isCollected = false;
    private SpriteRenderer sr;
    private Collider2D col;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    void Start()
    {
        Invoke(nameof(AutoDespawn), lifetime);
    }

    void Update()
    {
        if (!isCollected)
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    void OnMouseDown()
    {
        if (!IsLocalPlayerZombie()) return;
        TryCollect();
    }

    void TryCollect()
    {
        if (isCollected) return;
        isCollected = true;
        StartCoroutine(FlyAndDie());
    }

    bool IsLocalPlayerZombie()
    {
        return LobbyManager.Instance != null && LobbyManager.Instance.SelectedRole == PlayerRole.Zombie;
    }

    IEnumerator FlyAndDie()
    {
        while (Vector3.Distance(transform.position, collectTarget) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, collectTarget, 40f * Time.deltaTime);
            yield return null;
        }

        if (IsLocalPlayerZombie())
        {
        ZombieManager.Instance?.OnBrainCollected(brainValue);
        }

        
        Destroy(gameObject);
    }

    void AutoDespawn()
    {
        if (!isCollected)
            Destroy(gameObject);
    }
}
