using UnityEngine;
using Unity.Netcode;

public class LobProjectile : NetworkBehaviour
{
    public enum ProjectileType
    {
        Kernel,
        Butter,
        Cabbage,  // For future use
        Melon,    // For future use
        FrozenMelon // For future use
    }

    [Header("Projectile Settings")]
    [SerializeField] private ProjectileType projectileType = ProjectileType.Kernel;
    [SerializeField] private int damage = 20;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float arcScanHeight = 2f; 

    [Header("Effect Settings")]
    [SerializeField] private bool applySlow = false;
    [SerializeField] private float slowDuration = 0f;
    [SerializeField] private float slowPercentage = 0f;
    [SerializeField] private bool applyStun = false;
    [SerializeField] private float stunDuration = 3f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float flightDuration;
    private float flightTimer;
    private bool isLaunched = false;
    private Transform targetTransform;

    public void Initialize(Transform target, Vector3 destination)
    {
        targetTransform = target;
        startPosition = transform.position;
        // Add some randomness to target position to make it look natural? Or fixed?
        // For now fixed to target center/bottom
        targetPosition = destination;

        // Calculate distance
        float distance = Vector3.Distance(startPosition, targetPosition);
        flightDuration = distance / speed;
        flightTimer = 0f;
        isLaunched = true;
    }

    private void Update()
    {
        if (!IsServer || !isLaunched) return;

        flightTimer += Time.deltaTime;
        float t = flightTimer / flightDuration;

        if (t >= 1f)
        {
            // Reached target
            HitTarget();
            return;
        }

        // Parabolic movement
        // Linear interpolation for X (and Z if 3D, but 2D here mostly)
        Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, t);
        
        // Add arc height: sin(t * PI) gives a nice arc from 0 to 1 to 0
        float height = Mathf.Sin(t * Mathf.PI) * arcScanHeight;
        
        currentPos.y += height;

        transform.position = currentPos;
        
        // Optional: Rotate projectile to follow arc
        // Calculate tangent if needed
    }

    private void HitTarget()
    {
        isLaunched = false; // Stop moving
        
        // Check for collision at target
        // We can do a small overlap circle check or just assume if we reached target pos we hit whatever is there
        // Since we are targeting a specific zombie, we should check if it's still alive/there
        
        // Optimization: Use the targetTransform we saved if it's still valid
        if (targetTransform != null)
        {
            ZombieBase zombie = targetTransform.GetComponent<ZombieBase>();
            if (zombie != null)
            {
                ApplyEffects(zombie);
            }
        }
        else
        {
            // If target dead/gone, maybe check area for splash?
            // For now simple single target hit
            Debug.Log("LobProjectile missed or target lost");
        }

        // Destroy self
        DespawnProjectile();
    }
    
    private void ApplyEffects(ZombieBase zombie)
    {
        // Damage
        zombie.TakeDamage(damage);
        
        // Status Effects
        if (projectileType == ProjectileType.Butter || applyStun)
        {
            // Stun = 100% slow
            // applyTint: false (yellow butter), vfxTarget: Head
            zombie.ApplySlow(stunDuration, 1.0f, "butter", "ButterEffect", stunDuration, false, ZombieBase.VFXTargetType.Head);
        }
        else if (applySlow)
        {
            zombie.ApplySlow(slowDuration, slowPercentage, "lob_slow");
        }

        Debug.Log($"LobProjectile ({projectileType}) hit {zombie.name}");
        
        // Play sound
        if (NetworkGameManager.Instance != null)
        {
            string soundName = "splat"; // Default
            if (projectileType == ProjectileType.Butter) soundName = "butter";
            else if (projectileType == ProjectileType.Kernel) soundName = "kernel";
            
            NetworkGameManager.Instance.PlaySoundClientRpc(soundName);
        }
    }

    private void DespawnProjectile()
    {
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
            Destroy(gameObject);
        }
    }
}
