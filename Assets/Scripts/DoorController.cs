using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Door References")]
    [Tooltip("The child object that contains the actual door mesh/collider.")]
    public Transform doorVisual;

    [Header("Movement Settings")]
    public Vector3 closedLocalPosition;
    public Vector3 openLocalPosition;
    public float transitionDuration = 0.5f;

    private Coroutine movementCoroutine;

    /// <summary>
    /// Commands the door to slide to its closed position.
    /// </summary>
    public void CloseDoor()
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);
        movementCoroutine = StartCoroutine(MoveDoorRoutine(closedLocalPosition));
        
        // Optional: Play door slam audio here
    }

    /// <summary>
    /// Commands the door to slide to its open position.
    /// </summary>
    public void OpenDoor()
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);
        movementCoroutine = StartCoroutine(MoveDoorRoutine(openLocalPosition));
        
        // Optional: Play door open audio here
    }

    private IEnumerator MoveDoorRoutine(Vector3 targetPos)
    {
        Vector3 startPos = doorVisual.localPosition;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            // Use unscaled time so the animation plays even if timeScale is 0
            elapsed += Time.unscaledDeltaTime; 
            
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            
            // Smoothstep mathematical easing function for polished movement
            t = t * t * (3f - 2f * t); 
            
            doorVisual.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        doorVisual.localPosition = targetPos;
    }

    // --- LEVEL DESIGNER TOOLING ---
    // These allow the designer to place the door exactly where they want it in the scene view,
    // right-click the script component, and save the coordinates instantly.

    [ContextMenu("Save Current Pos as CLOSED")]
    private void SaveClosedPosition() => closedLocalPosition = doorVisual.localPosition;

    [ContextMenu("Save Current Pos as OPEN")]
    private void SaveOpenPosition() => openLocalPosition = doorVisual.localPosition;
}