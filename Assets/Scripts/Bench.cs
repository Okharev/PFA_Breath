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
        
        // Optional: Trigger a save game event, heal the player, or reset enemy spawns here.
    }
}