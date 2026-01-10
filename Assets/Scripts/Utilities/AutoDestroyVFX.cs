using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    public float lifetime = 5f;
    private float spawnTime;

    private void Start()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (Time.time >= spawnTime + lifetime)
        {
            // Debug.Log($"Auto-destroying VFX after {lifetime}s");
            Destroy(gameObject);
        }
    }
}
