using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
public class MixiZombie : ZombieBase
{
    [Header("Brain")]
    [SerializeField] private NetworkObject brainPrefab; 
    [SerializeField] private float workInterval = 7.5f;
    [SerializeField] private float workAnimLength = 2.0f; 

    [Header("Movement")]
    [SerializeField] private float minY = -7.78f;
    [SerializeField] private float maxY = 0f;

    [Header("Brain Bounce")]
    [SerializeField] private float bounceHeight = 0.5f;
    [SerializeField] private float bounceDuration = 0.3f;
    [SerializeField] private float dropDistance = 0.3f;
    [SerializeField] private float dropDuration = 0.2f;

    private Rigidbody2D rb;
    private int moveDir = 1;

    private float workTimer;
    private bool isWorking;

    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        animator = GetComponent<Animator>();

        SetWalking(false);
        SetWorking(false);

        Invoke(nameof(StartWalking), 0.5f);
    }

    private void StartWalking()
    {
        isWorking = false;
        SetWalking(true);
        SetWorking(false);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        if (!isWorking)
        {
            MoveVertical();
            HandleWorkTimer();
        }
    }

    private void MoveVertical()
    {
        Vector2 pos = rb.position;
        pos.y += moveDir * moveSpeed * Time.fixedDeltaTime;

        if (pos.y <= minY)
        {
            pos.y = minY;
            moveDir = 1;
        }
        else if (pos.y >= maxY)
        {
            pos.y = maxY;
            moveDir = -1;
        }

        rb.MovePosition(pos);
    }

    private void HandleWorkTimer()
    {
        workTimer += Time.fixedDeltaTime;
        if (workTimer >= workInterval)
        {
            workTimer = 0f;
            StartWork();
        }
    }

    private void StartWork()
    {
        if (isWorking) return;

        isWorking = true;
        SetWalking(false);
        SetWorking(true);

        Invoke(nameof(ProduceBrainServerRpc), workAnimLength);
    }

    // ==== SERVER SPAWN BRAIN ====
    [ServerRpc(RequireOwnership = false)]
    private void ProduceBrainServerRpc()
    {
        if (!IsServer || brainPrefab == null) return;

        NetworkObject brainInstance = Instantiate(brainPrefab, transform.position, Quaternion.identity);
        brainInstance.Spawn(true);
        Debug.Log($"✅ Brain spawned by {gameObject.name}: NetworkObjectId={brainInstance.NetworkObjectId}");

        // Gọi ClientRpc cho tất cả client chạy bounce animation
        TriggerBrainBounceClientRpc(brainInstance.NetworkObjectId, transform.position);

        isWorking = false;
        SetWalking(true);
        SetWorking(false);
    }

    [ClientRpc]
    private void TriggerBrainBounceClientRpc(ulong brainId, Vector3 startPos)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(brainId, out NetworkObject brainObj))
        {
            StartCoroutine(BrainBounce(brainObj.transform, startPos));
        }
        else
        {
            Debug.LogWarning($"⚠️ Brain object not found for NetworkObjectId {brainId}");
        }
    }

    private IEnumerator BrainBounce(Transform brainTransform, Vector3 startPos)
    {
        if (brainTransform == null) yield break;

        Rigidbody2D brb = brainTransform.GetComponent<Rigidbody2D>();
        if (brb != null)
        {
            brb.bodyType = RigidbodyType2D.Kinematic;
            brb.linearVelocity = Vector2.zero;
        }

        // Bounce lên
        Vector3 peakPos = startPos + Vector3.up * bounceHeight;
        float elapsed = 0f;
        while (elapsed < bounceDuration && brainTransform != null)
        {
            float t = elapsed / bounceDuration;
            float easeT = 1f - Mathf.Pow(1f - t, 2f);
            brainTransform.position = Vector3.Lerp(startPos, peakPos, easeT);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Drop xuống
        Vector3 finalPos = peakPos + Vector3.down * dropDistance;
        elapsed = 0f;
        while (elapsed < dropDuration && brainTransform != null)
        {
            float t = elapsed / dropDuration;
            float easeT = t * t;
            brainTransform.position = Vector3.Lerp(peakPos, finalPos, easeT);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (brainTransform != null)
            brainTransform.position = finalPos;
    }

    // ===== Animation helpers =====
    private void SetWalking(bool value) => SetWalkingClientRpc(value);
    private void SetWorking(bool value) => SetWorkingClientRpc(value);

    [ClientRpc]
    private void SetWalkingClientRpc(bool value)
    {
        if (animator != null)
            animator.SetBool("isWalking", value);
    }

    [ClientRpc]
    private void SetWorkingClientRpc(bool value)
    {
        if (animator != null)
            animator.SetBool("isWorking", value);
    }

    protected override void Die()
    {
        if (!IsServer) return;

        SetWalking(false);
        SetWorking(false);

        TriggerDieAnimationClientRpc();
        Invoke(nameof(DespawnZombie), 1f);
    }

    [ClientRpc]
    private void TriggerDieAnimationClientRpc()
    {
        if (animator != null)
            animator.SetTrigger("Die");
    }

    private void DespawnZombie()
    {
        if (!IsServer) return;

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn();

        Destroy(gameObject);
    }
}
