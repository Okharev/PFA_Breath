using Ability.NewAbilitySystem;
using Unity.VisualScripting;
using UnityEngine;

public class BubbleTrigger : MonoBehaviour
{
    private OxygenRestoration oxygenRestoration;

    private void Awake()
    {
        oxygenRestoration = GetComponentInParent<OxygenRestoration>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        {
            OxygenComponent oxy = other.gameObject.GetComponent<OxygenComponent>();

            // Check Oxygen first
            if (other.gameObject.GetComponent<OxygenComponent>().HasOxygen(oxy.maxOxygen)) return;

            // Replenish
            oxy.Replenish(oxygenRestoration.bonus);


            StopAllCoroutines();
            StartCoroutine(oxygenRestoration.DestroyBubble());
        }
    }

}
