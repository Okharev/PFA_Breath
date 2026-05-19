using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles rotating the player to face the mouse cursor.
/// Operates on unscaled time, allowing the player to aim ONLY when time is stopped.
/// </summary>
public class PlayerRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("How fast the player turns to face the mouse. Operates independently of Time.timeScale.")]
    public float rotationSpeed = 20f;

    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || mainCam is null) return;

        // GATEKEEPER: Only allow rotation if time is currently paused/waiting for input.
        // If an action is executing (time is flowing), lock the rotation.
        if (!TimeTickManager.Instance.IsTimeFlowing())
        {
            RotateTowardsMouse();
        }
    }

    private void RotateTowardsMouse()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        // 1. Create a mathematical plane at the player's exact Y position. (O(1) complexity)
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

        // 2. Cast the ray against our mathematical plane
        if (groundPlane.Raycast(ray, out float distance))
        {
            // 3. Get the 3D point where the ray intersected the plane
            Vector3 aimPoint = ray.GetPoint(distance);

            // 4. Calculate the direction to look, flattening the Y axis
            Vector3 lookDirection = aimPoint - transform.position;
            lookDirection.y = 0f;

            // 5. Prevent LookRotation errors if the mouse is exactly dead-center on the player
            if (lookDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

                // 6. Smoothly rotate towards the target using UNSCALED time.
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.unscaledDeltaTime
                );
            }
        }
    }
}