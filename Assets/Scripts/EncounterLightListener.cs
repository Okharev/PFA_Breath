using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EncounterRoomTrigger))]
public class EncounterLightController : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The root transform to search for Directional Lights. Leaves empty to search from this GameObject down.")]
    [SerializeField] private Transform lightHierarchyRoot;
    
    [Tooltip("How long it takes for the lights to fade back to their original intensity.")]
    [SerializeField] private float fadeDuration = 2.5f;

    // The component we are observing
    private EncounterRoomTrigger encounterTrigger;

    // Cache-friendly struct to hold the light and its target intensity
    private struct LightData
    {
        public Light SceneLight;
        public float TargetIntensity;
    }

    // Array provides contiguous memory allocation, which is slightly faster for iteration in Update/Coroutines
    private LightData[] directionalLights;

    private void Awake()
    {
        encounterTrigger = GetComponent<EncounterRoomTrigger>();
        
        // Default to this transform if an external root wasn't assigned
        if (lightHierarchyRoot == null)
        {
            lightHierarchyRoot = transform;
        }

        CacheAndResetLights();
    }

    private void OnEnable()
    {
        // OBSERVER PATTERN: Subscribe to the room cleared event
        if (encounterTrigger != null)
        {
            encounterTrigger.OnRoomCleared += HandleRoomCleared;
        }
    }

    private void OnDisable()
    {
        // ALWAYS unsubscribe to prevent memory leaks and null reference exceptions
        if (encounterTrigger != null)
        {
            encounterTrigger.OnRoomCleared -= HandleRoomCleared;
        }
    }

    private void CacheAndResetLights()
    {
        // O(N) Traversal of the hierarchy to find all lights (true = include inactive gameobjects)
        Light[] allChildLights = lightHierarchyRoot.GetComponentsInChildren<Light>(true);
        
        // Temporary list for filtering
        List<LightData> validLights = new List<LightData>();

        foreach (Light currentLight in allChildLights)
        {
            if (currentLight.type == LightType.Spot)
            {
                // Cache the artist's original configuration
                validLights.Add(new LightData
                {
                    SceneLight = currentLight,
                    TargetIntensity = currentLight.intensity
                });

                // Immediately turn the light off for the start of the encounter
                currentLight.intensity = 0f;
            }
        }

        // Convert to array for cache-friendly runtime iteration
        directionalLights = validLights.ToArray();
    }

    private void HandleRoomCleared()
    {
        // Begin the fade-in process when the trigger resolves the encounter
        StartCoroutine(FadeLightsInRoutine());
    }

    private IEnumerator FadeLightsInRoutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Normalize time between 0 and 1
            float t = elapsedTime / fadeDuration;
            
            // SmoothStep provides a non-linear ease-in/ease-out effect, looking much more natural than standard Lerp
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // O(M) loop updating intensities
            for (int i = 0; i < directionalLights.Length; i++)
            {
                directionalLights[i].SceneLight.intensity = Mathf.Lerp(0f, directionalLights[i].TargetIntensity, smoothT);
            }

            // Yield execution until the next frame
            yield return null;
        }

        // Failsafe: Ensure final values are perfectly matched to target, avoiding floating-point precision errors
        for (int i = 0; i < directionalLights.Length; i++)
        {
            directionalLights[i].SceneLight.intensity = directionalLights[i].TargetIntensity;
        }
    }
}