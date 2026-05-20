using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 20f;
    private Camera mainCam;
    private Rigidbody rb;

    private void Start()
    {
        mainCam = Camera.main;
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Note: Used '==' instead of 'is' for Unity Objects to safely check for destroyed objects
        if (Mouse.current == null || mainCam is null) return;

        // Gatekeeper: Lock rotation once the turn begins executing
        if (!TurnManager.Instance.IsExecuting) 
        {
            RotateTowardsMouse();
        }
    }

    private void RotateTowardsMouse()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 aimPoint = ray.GetPoint(distance);
            
            // Use rb.position instead of transform.position for consistency
            Vector3 lookDirection = aimPoint - rb.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                
                // THE FIX: Apply directly to the Rigidbody's rotation property.
                // This updates the physics state immediately, bypassing interpolation locks.
                rb.rotation = Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.unscaledDeltaTime
                );
            }
        }
    }
}