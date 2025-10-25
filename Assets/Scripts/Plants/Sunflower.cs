using UnityEngine;
using System.Collections;

public class Sunflower : PlantBase
{
    [Header("Sunflower Settings")]
    public GameObject sunPrefab;
    public Transform dropPoint;
    public float dropInterval = 24.3f; // seconds between drops
    public int defaultHealth = 300;

    private Coroutine dropRoutine;

    protected override void Start()
    {
        // set health for this plant type
        maxHealth = defaultHealth;
        base.Start();

        // ensure there's a drop point (placed above the sunflower by default)
        if (dropPoint == null)
        {
            GameObject dp = new GameObject("DropPoint");
            dp.transform.parent = transform;
            dp.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            dropPoint = dp.transform;
        }

        // start dropping suns if prefab assigned
        if (sunPrefab != null)
            dropRoutine = StartCoroutine(DropSunRoutine());
        else
            Debug.LogWarning($"{name}: Sun prefab not assigned on Sunflower.");
    }

    private IEnumerator DropSunRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(dropInterval);
            if (sunPrefab != null && dropPoint != null)
                Instantiate(sunPrefab, dropPoint.position, Quaternion.identity);
        }
    }

    protected virtual void OnDisable()
    {
        if (dropRoutine != null)
        {
            StopCoroutine(dropRoutine);
            dropRoutine = null;
        }
    }

    protected override void Die()
    {
        // stop routine when dying
        OnDisable();
        base.Die();
    }
}
```// filepath: d:\hacker\SE115 unity\pvz\pvz-unity\Assets\Scripts\Plants\Sunflower.cs
using UnityEngine;
using System.Collections;

public class Sunflower : PlantBase
{
    [Header("Sunflower Settings")]
    public GameObject sunPrefab;
    public Transform dropPoint;
    public float dropInterval = 24.3f; // seconds between drops
    public int defaultHealth = 100;

    private Coroutine dropRoutine;

    protected override void Start()
    {
        // set health for this plant type
        maxHealth = defaultHealth;
        base.Start();

        // ensure there's a drop point (placed above the sunflower by default)
        if (dropPoint == null)
        {
            GameObject dp = new GameObject("DropPoint");
            dp.transform.parent = transform;
            dp.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            dropPoint = dp.transform;
        }

        // start dropping suns if prefab assigned
        if (sunPrefab != null)
            dropRoutine = StartCoroutine(DropSunRoutine());
        else
            Debug.LogWarning($"{name}: Sun prefab not assigned on Sunflower.");
    }

    private IEnumerator DropSunRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(dropInterval);
            if (sunPrefab != null && dropPoint != null)
                Instantiate(sunPrefab, dropPoint.position, Quaternion.identity);
        }
    }

    protected virtual void OnDisable()
    {
        if (dropRoutine != null)
        {
            StopCoroutine(dropRoutine);
            dropRoutine = null;
        }
    }

    protected override void Die()
    {
        // stop routine when dying
        OnDisable();
        base.Die();
    }
}