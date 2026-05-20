using UnityEngine;

/// <summary>
///     A modular camera controller that maintains an isometric perspective.
///     Operates independently of Time.timeScale to ensure smooth rendering
///     even during paused or slow-motion ticks managed by TimeTickManager.
/// </summary>
public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("The transform this camera should follow. Keep decoupled from specific logic scripts.")]
    public Transform target;

    [Header("Isometric Coordinates")] [Tooltip("Distance from the target.")]
    public float distance = 15f;

    [Tooltip("Downward angle (pitch). 30 to 45 is standard for isometric perspectives.")]
    public float pitch = 30f;

    [Tooltip("Horizontal angle (yaw). 45 creates a true isometric diamond grid look.")]
    public float yaw = 45f;

    [Header("Smoothing")] [Tooltip("Enable for smooth camera follow. Disable for a rigid 1:1 follow.")]
    public bool useSmoothing = true;

    [Tooltip("Approximate time to reach the target. Smaller values mean a tighter follow.")]
    public float smoothTime = 0.15f;

    // State variable required by Vector3.SmoothDamp
    private Vector3 currentVelocity = Vector3.zero;

    private void LateUpdate()
    {
        // 1. Calculate the isometric rotation based on pitch and yaw angles
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        // 2. Calculate target position by pushing backwards from the target using the rotation
        // This math is highly efficient (O(1) time complexity) and prevents Gimbal lock.
        Vector3 desiredPosition = target.position + cameraRotation * new Vector3(0f, 0f, -distance);

        // 3. Apply the position translation
        if (useSmoothing)
            // CRITICAL: We use Time.unscaledDeltaTime here.
            // Even if TimeTickManager sets Time.timeScale to 0, the camera will 
            // continue to smoothly damp towards the player if they snap to a new position.
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        else
            // Rigid follow immediately snaps the camera to the correct offset
            transform.position = desiredPosition;

        // 4. Ensure the camera is constantly looking in the correct isometric direction
        transform.rotation = cameraRotation;
    }
}