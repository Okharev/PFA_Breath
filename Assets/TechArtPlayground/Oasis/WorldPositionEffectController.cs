using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem; // <-- Added the New Input System

[ExecuteAlways]
public class ZoneTriggerController : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("The maximum size the zone will grow to.")]
    public float maxRadius = 15f;
    [Tooltip("How long (in seconds) it takes to reach max radius.")]
    public float duration = 1.5f;
    
    [Header("Material References")]
    [Tooltip("Assign the material using the shader here. If empty, it applies globally.")]
    public Material targetMaterial;

    // Shader Property IDs for performance
    private static readonly int GlobalCenterID = Shader.PropertyToID("_GlobalEffectCenter");
    private static readonly int EffectRadiusID = Shader.PropertyToID("_EffectRadius");

    private Coroutine zoneCoroutine;

    void Update()
    {
        // 1. Constantly update the center of the effect
        Shader.SetGlobalVector(GlobalCenterID, transform.position);

        // 2. Trigger expansion using the NEW Input System
        if (Application.isPlaying)
        {
            // Check if a keyboard is connected, then check if Space was pressed
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TriggerZone();
            }
        }
    }

    public void TriggerZone()
    {
        if (zoneCoroutine != null)
        {
            StopCoroutine(zoneCoroutine);
        }
        zoneCoroutine = StartCoroutine(AnimateRadiusRoutine());
    }

    private IEnumerator AnimateRadiusRoutine()
    {
        float timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            
            // Calculate a smooth expansion
            float t = timeElapsed / duration;
            float currentRadius = Mathf.SmoothStep(0f, maxRadius, t);

            // Apply to the specific material, or globally
            if (targetMaterial != null)
            {
                targetMaterial.SetFloat(EffectRadiusID, currentRadius);
            }
            else
            {
                Shader.SetGlobalFloat(EffectRadiusID, currentRadius);
            }

            yield return null;
        }

        // Snap perfectly to the max radius at the end
        if (targetMaterial != null)
            targetMaterial.SetFloat(EffectRadiusID, maxRadius);
        else
            Shader.SetGlobalFloat(EffectRadiusID, maxRadius);
    }

    private void OnDisable()
    {
        if (targetMaterial != null)
            targetMaterial.SetFloat(EffectRadiusID, 0f);
        else
            Shader.SetGlobalFloat(EffectRadiusID, 0f);
    }
}