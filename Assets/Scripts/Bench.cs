using Ability.NewAbilitySystem;
using Skills.UI;
using UnityEngine;

public class CheckpointBench : MonoBehaviour, IInteractable
{
    public void Interact(GameObject instigator)
    {
        Debug.Log("[CheckpointBench] Bench clicked! Opening Skill Tree.");
        
        // Trigger the UI to open
        SkillTreeUIController.Instance.OpenMenu();

        if (instigator.TryGetComponent(out OxygenComponent oxygen))
        {
            oxygen.Replenish(oxygen.maxOxygen);
        }
        
        if (instigator.TryGetComponent(out HealthComponent health))
        {
            health.Heal(health.maxHealth);
        }
    }
}