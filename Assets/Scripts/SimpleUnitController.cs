using Unity.Netcode;
using UnityEngine;

public class SimpleUnitController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int maxHealth = 100;
    
    private PlayerRole unitRole;
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    public void SetRole(PlayerRole role)
    {
        unitRole = role;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        currentHealth.Value = maxHealth;
        networkPosition.Value = transform.position;
        
        // Subscribe to network variable changes
        currentHealth.OnValueChanged += OnHealthChanged;
        networkPosition.OnValueChanged += OnPositionChanged;
        
        // Setup visual appearance
        SetupVisuals();
        
        // Add collider for combat
        SetupCollider();
    }
    
    private void SetupVisuals()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            // Set color based on role and ownership
            Color baseColor = unitRole == PlayerRole.Plant ? Color.green : Color.red;
            
            if (!IsOwner)
            {
                // Make opponent units slightly darker
                baseColor *= 0.7f;
            }
            
            renderer.material.color = baseColor;
        }
        
        // Scale down the cube a bit
        transform.localScale = Vector3.one * 0.8f;
        
        gameObject.name = $"{unitRole}Unit_{(IsOwner ? "Mine" : "Enemy")}";
    }
    
    private void SetupCollider()
    {
        var collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.isTrigger = true; // Enable trigger for combat detection
        }
    }
    
    void Update()
    {
        if (IsOwner)
        {
            // Only the owner controls movement
            HandleMovement();
        }
        else
        {
            // Non-owners just interpolate to network position
            InterpolatePosition();
        }
    }
    
    private void HandleMovement()
    {
        // Simple AI: move forward based on role
        Vector3 direction = unitRole == PlayerRole.Plant ? Vector3.right : Vector3.left;
        Vector3 newPosition = transform.position + direction * moveSpeed * Time.deltaTime;
        
        transform.position = newPosition;
        
        // Update network position
        networkPosition.Value = newPosition;
        
        // Check if unit has moved too far and should be destroyed
        if (Mathf.Abs(transform.position.x) > 15f)
        {
            DestroyUnit();
        }
    }
    
    private void InterpolatePosition()
    {
        // Smooth interpolation to network position for non-owners
        transform.position = Vector3.Lerp(transform.position, networkPosition.Value, Time.deltaTime * 10f);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Only server handles combat
        
        var otherUnit = other.GetComponent<SimpleUnitController>();
        if (otherUnit != null && otherUnit.unitRole != this.unitRole)
        {
            Debug.Log($"{unitRole} unit encountered {otherUnit.unitRole} unit - Combat!");
            
            // Both units take damage
            TakeDamage(25);
            otherUnit.TakeDamage(25);
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (!IsServer) return; // Only server can modify health
        
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        
        // Trigger damage effect on all clients
        ShowDamageEffectClientRpc();
        
        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth.Value}");
    }
    
    [ClientRpc]
    private void ShowDamageEffectClientRpc()
    {
        // Visual damage feedback
        StartCoroutine(DamageFlashEffect());
    }
    
    private System.Collections.IEnumerator DamageFlashEffect()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var originalColor = renderer.material.color;
            
            // Flash white
            renderer.material.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            
            // Return to original color
            renderer.material.color = originalColor;
        }
    }
    
    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        Debug.Log($"{gameObject.name} health changed: {oldHealth} -> {newHealth}");
        
        if (newHealth <= 0)
        {
            DestroyUnit();
        }
    }
    
    private void OnPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        // This is called when network position updates
        if (!IsOwner)
        {
            // Update position for non-owners
            transform.position = newPos;
        }
    }
    
    private void DestroyUnit()
    {
        if (IsServer)
        {
            Debug.Log($"{gameObject.name} is being destroyed");
            
            // Play destruction effect on all clients
            PlayDestructionEffectClientRpc();
            
            // Despawn the network object
            GetComponent<NetworkObject>().Despawn();
        }
    }
    
    [ClientRpc]
    private void PlayDestructionEffectClientRpc()
    {
        // Add destruction visual effect here
        Debug.Log($"{gameObject.name} destroyed!");
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        currentHealth.OnValueChanged -= OnHealthChanged;
        networkPosition.OnValueChanged -= OnPositionChanged;
        
        base.OnNetworkDespawn();
    }
}