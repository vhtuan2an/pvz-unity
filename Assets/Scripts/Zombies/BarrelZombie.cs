using UnityEngine;
using Unity.Netcode;

public class BarrelZombie : ZombieBase
{
    [Header("Barrel")]
    [SerializeField] private GameObject barrelPrefab;
    [SerializeField] private float rollCooldown = 3f;
    [SerializeField] private float rollDuration = 0.6f;
    [SerializeField] private Transform barrelSpawnPoint;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float laneHeight = 1.3f;
    [SerializeField] private Vector3 detectionOffset = new Vector3(-0.8f, 0f, 0f);
    [SerializeField] private float yTolerance = 0.25f;

    private float rollTimer;
    private bool isRolling;
    private bool isDead;

    private Collider2D col;
    private PlantBase currentTarget;
    


    protected override void Start()
    {
        base.Start();
        col = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (!IsServer || isDead || isRolling)
            return;

        rollTimer += Time.deltaTime;

        if (rollTimer < rollCooldown)
            return;

        currentTarget = FindNearestPlantInFront();

        if (currentTarget != null)
        {
            StartRoll();
        }
        else
        {
            rollTimer = rollCooldown - 0.4f;
        }
    }


        private PlantBase FindNearestPlantInFront()
    {
        GameObject[] plants = GameObject.FindGameObjectsWithTag("Plant");

        if (plants.Length == 0)
            return null;

        Vector3 detectionOrigin = transform.position;
        PlantBase nearestPlant = null;
        float nearestDistance = float.MaxValue;

        foreach (var plantObj in plants)
        {
            Collider2D plantCol = plantObj.GetComponentInChildren<Collider2D>();
            if (plantCol == null)
                continue;

            Vector3 plantPos = plantCol.bounds.center;

            if (plantPos.x < detectionOrigin.x)
            {
                float yDiff = Mathf.Abs(plantPos.y - detectionOrigin.y);

                if (yDiff <= (laneHeight * 0.5f) + yTolerance)
                {
                    float distance = detectionOrigin.x - plantPos.x;

                    if (distance > 0f && distance <= detectionRange)
                    {
                        if (distance < nearestDistance)
                        {
                            PlantBase plant = plantObj.GetComponent<PlantBase>();
                            if (plant != null)
                            {
                                nearestDistance = distance;
                                nearestPlant = plant;

                                Debug.Log(
                                    $"[BarrelZombie] Checking plant: {plant.name}, " +
                                    $"yDiff: {yDiff:F2}, distance: {nearestDistance:F2}"
                                );
                            }
                        }
                    }
                }
            }
        }

        if (nearestPlant != null)
        {
            Debug.Log($"[BarrelZombie] FOUND TARGET: {nearestPlant.name}");
        }

        return nearestPlant;
    }

    private Vector3 GetDetectionOrigin()
    {
        return transform.position;
    }

    private void StartRoll()
    {
        if (!IsServer || isRolling || currentTarget == null)
            return;

        Debug.Log($"[BarrelZombie] Rolling barrel at target: {currentTarget.name}");

        isRolling = true;
        rollTimer = 0f;

        SetRollingClientRpc(true);
        SpawnBarrel();

        Invoke(nameof(StopRolling), rollDuration);
    }

    private void StopRolling()
    {
        isRolling = false;
        SetRollingClientRpc(false);
        currentTarget = null;
    }

    private void SpawnBarrel()
    {
        if (barrelPrefab == null || barrelSpawnPoint == null)
        {
            Debug.LogWarning("[BarrelZombie] Missing barrel prefab or spawn point!");
            return;
        }

        GameObject barrel = Instantiate(barrelPrefab, barrelSpawnPoint.position, Quaternion.identity);
        NetworkObject netObj = barrel.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
        else
        {
            Debug.LogWarning("[BarrelZombie] Barrel prefab missing NetworkObject!");
        }
    }

    protected override void Die()
    {
        if (!IsServer || isDead) return;

        isDead = true;
        CancelInvoke();
        currentTarget = null;

        DieClientRpc();
        base.Die();

        enabled = false;
    }

    [ClientRpc]
    private void SetRollingClientRpc(bool value)
    {
        if (animator != null)
            animator.SetBool("isRolling", value);
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        if (animator != null)
            animator.SetTrigger("Die");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider2D c = GetComponent<Collider2D>();
        Vector3 origin = c != null
            ? c.bounds.center + detectionOffset
            : transform.position + detectionOffset;


        Vector3 boxCenter = origin + Vector3.left * (detectionRange * 0.5f);
        Vector3 boxSize = new Vector3(detectionRange, laneHeight + (yTolerance * 2), 0.1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(boxCenter, boxSize);

        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireCube(origin, new Vector3(0.5f, laneHeight + yTolerance * 2, 0.1f));

        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, Vector3.left * detectionRange);
    }
#endif
}