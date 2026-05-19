using UnityEngine;
using UnityEngine.InputSystem;

// Indispensable pour le nouveau système d'entrée

/// <summary>
///     Gère le déplacement en cliquant, mis à jour avec le New Input System.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Action Settings")] public float actionTimeCost = 1.0f;

    public float moveSpeed = 5f;
    private bool isMoving;
    private Camera mainCam;

    private Rigidbody rb;
    private Vector3 targetPosition;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
    }

    [Header("Movement Limits")]
    [Tooltip("How far the player can move in a single action tick.")]
    public float maxMoveDistance = 5f;
    
    [Header("References")]
    public ActionVisualizer moveVisualizer;

    private void Update()
    {
        if (Mouse.current == null) return;

        if (!TimeTickManager.Instance.IsTimeFlowing())
        {
            isMoving = false;

            // 1. Calculate and preview the clamped movement target
            Vector3? previewTarget = GetClampedMouseTarget();

            if (previewTarget.HasValue)
            {
                // Draw the line from the player to the max possible distance
                moveVisualizer.DrawIntent(transform.position, previewTarget.Value);

                // 2. Execute movement on click
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    targetPosition = previewTarget.Value;
                    isMoving = true;
                    moveVisualizer.Hide(); // Hide line while moving
                    TimeTickManager.Instance.TriggerActionTick(actionTimeCost);
                }
            }
            else
            {
                moveVisualizer.Hide();
            }
        }
        else
        {
            // Ensure the line is hidden while time is flowing
            moveVisualizer.Hide(); 
        }
    }

    /// <summary>
    ///     Casts a ray to the mouse and clamps the distance to maxMoveDistance.
    /// </summary>
    private Vector3? GetClampedMouseTarget()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 rawTarget = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            Vector3 direction = rawTarget - transform.position;
            float distance = direction.magnitude;

            // If the mouse is further than our max distance, clamp it.
            if (distance > maxMoveDistance)
            {
                return transform.position + (direction.normalized * maxMoveDistance);
            }
            
            return rawTarget;
        }

        return null;
    }

    private void FixedUpdate()
    {
        if (isMoving)
        {
            Vector3 newPos = Vector3.MoveTowards(rb.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            if (Vector3.Distance(rb.position, targetPosition) < 0.1f) isMoving = false;
        }
    }

    private void TrySetMovementTarget()
    {
        // On lit la position actuelle de la souris avec le nouveau système
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            targetPosition = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            isMoving = true;

            TimeTickManager.Instance.TriggerActionTick(actionTimeCost);
        }
    }
}