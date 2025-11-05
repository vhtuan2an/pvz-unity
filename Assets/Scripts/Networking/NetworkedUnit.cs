using Unity.Netcode;
using UnityEngine;

public class NetworkedUnit : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int damage = 25;
    
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(100);
    private NetworkVariable<PlayerRole> unitRole = new NetworkVariable<PlayerRole>();
    
    private Rigidbody rb;
    private bool isMoving = true;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        currentHealth.Value = maxHealth;
        rb = GetComponent<Rigidbody>();
        
        // Set unit role based on owner
        if (IsOwner)
        {
            PlayerRole ownerRole = (PlayerRole)System.Enum.Parse(typeof(PlayerRole), PlayerPrefs.GetString("PlayerRole"));
            SetUnitRoleServerRpc(ownerRole);
        }
        
        // Setup visuals
        currentHealth.OnValueChanged += OnHealthChanged;
        unitRole.OnValueChanged += OnRoleChanged;
        
        SetupVisuals();
    }

    [ServerRpc]
    private void SetUnitRoleServerRpc(PlayerRole role)
    {
        unitRole.Value = role;
    }

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        // Update health bar or effects
        if (newHealth <= 0)
        {
            DestroyUnit();
        }
    }

    private void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Color color = unitRole.Value == PlayerRole.Plant ? Color.green : Color.red;
            
            // Make opponent units slightly transparent
            if (!IsOwner)
            {
                color.a = 0.8f;
            }
            
            renderer.material.color = color;
        }
        
        // Set unit name
        gameObject.name = $"{unitRole.Value}_Unit_{(IsOwner ? "Mine" : "Enemy")}";
    }

    void Update()
    {
        if (!IsServer || !isMoving) return;
        
        // Move unit based on role
        Vector3 direction = unitRole.Value == PlayerRole.Plant ? Vector3.right : Vector3.left;
        transform.Translate(direction * moveSpeed * Time.deltaTime);
        
        // Remove unit if it goes too far
        if (Mathf.Abs(transform.position.x) > 15f)
        {
            DestroyUnit();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        
        var otherUnit = other.GetComponent<NetworkedUnit>();
        if (otherUnit != null && otherUnit.unitRole.Value != this.unitRole.Value)
        {
            // Combat: both units take damage
            TakeDamage(otherUnit.damage);
            otherUnit.TakeDamage(this.damage);
            
            // Stop both units for combat
            SetMoving(false);
            otherUnit.SetMoving(false);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (!IsServer) return;
        
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damageAmount);
        
        // Visual feedback
        TakeDamageClientRpc();
    }

    [ClientRpc]
    private void TakeDamageClientRpc()
    {
        // Add damage effect (flash red, shake, etc.)
        StartCoroutine(DamageEffect());
    }

    private System.Collections.IEnumerator DamageEffect()
    {
        var renderer = GetComponent<Renderer>();
        var originalColor = renderer.material.color;
        
        // Flash red
        renderer.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        renderer.material.color = originalColor;
    }

    private void SetMoving(bool moving)
    {
        isMoving = moving;
        
        if (moving)
        {
            // Resume movement after 2 seconds
            Invoke(nameof(ResumeMovement), 2f);
        }
    }

    private void ResumeMovement()
    {
        isMoving = true;
    }

    private void DestroyUnit()
    {
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
        unitRole.OnValueChanged -= OnRoleChanged;
        base.OnNetworkDespawn();
    }
}