using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class WinterMint : PlantBase
{
    [Header("Winter-mint Settings")]
    [SerializeField] private float freezeDuration = 5f;
    [SerializeField] private float slowPercentage = 0.5f;
    [SerializeField] private float slowDuration = 3f;
    
    [Header("VFX Settings")]
    [SerializeField] private string freezeVFXPrefabName = "FreezeEffect";
    [SerializeField] private float freezeVFXDuration = 5f;

    private Animator animator;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        
        if (IsServer)
        {
            StartCoroutine(WintermintSequence());
        }
    }

    private IEnumerator WintermintSequence()
    {
        // Play Intro animation
        TriggerIntroClientRpc();
        
        // Wait for Intro animation to start
        yield return null;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Intro"))
            yield return null;
        
        // Wait for Intro animation to finish
        float introLength = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(introLength);

        FreezeAllZombies();

        // Play Idle animation
        TriggerIdleClientRpc();
        yield return new WaitForSeconds(freezeDuration);

        // Slow zombies after unfreeze
        SlowAllZombies();

        // Play Outro animation
        TriggerOutroClientRpc();

        // Wait for Outro animation to start
        yield return null;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Outro"))
            yield return null;

        // Wait for Outro animation to finish
        float outroLength = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(outroLength);

        Die();
    }

    private void FreezeAllZombies()
    {
        if (!IsServer) return;

        ZombieBase[] zombies = FindObjectsByType<ZombieBase>(FindObjectsSortMode.None);
        
        Debug.Log($"Winter-mint freezing {zombies.Length} zombies (100% slow with VFX for {freezeVFXDuration}s)");

        foreach (ZombieBase zombie in zombies)
        {
            zombie.ApplySlow(freezeDuration, 1.0f, "wintermint_freeze", freezeVFXPrefabName, freezeVFXDuration);
        }
    }

    private void SlowAllZombies()
    {
        if (!IsServer) return;

        ZombieBase[] zombies = FindObjectsByType<ZombieBase>(FindObjectsSortMode.None);
        
        Debug.Log($"Winter-mint slowing {zombies.Length} zombies ({slowPercentage * 100}% slow)");

        foreach (ZombieBase zombie in zombies)
        {
            zombie.ApplySlow(slowDuration, slowPercentage, "wintermint_slow");
        }
    }

    [ClientRpc]
    private void TriggerIntroClientRpc()
    {
        if (animator != null)
        {
            animator.Play("Intro");
        }
    }

    [ClientRpc]
    private void TriggerIdleClientRpc()
    {
        if (animator != null)
        {
            animator.Play("Idle");
        }
    }

    [ClientRpc]
    private void TriggerOutroClientRpc()
    {
        if (animator != null)
        {
            animator.Play("Outro");
        }
    }
}