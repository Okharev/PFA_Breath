using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Action Settings")] 
    public int actionTurnCost = 1;
    public float moveSpeed = 5f;

    [Header("Movement Limits")] 
    public float maxMoveDistance = 5f;

    [Header("References")] 
    public ActionVisualizer moveVisualizer;

    private bool isMoving;
    private Camera mainCam;
    private Rigidbody rb;
    private Vector3 targetPosition;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // --- KINEMATIC SETUP ---
        rb.isKinematic = false; // Disables gravity and physics-based forces
        rb.useGravity = false; // Redundant when kinematic, but good for clarity
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooths visual movement
        // -----------------------
        
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        if (!TurnManager.Instance.IsExecuting)
        {
            isMoving = false;
            Vector3? previewTarget = GetClampedMouseTarget();

            if (previewTarget.HasValue)
            {
                moveVisualizer.DrawIntent(transform.position, previewTarget.Value, ActionVisualizer.IntentType.Movement);

                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    targetPosition = previewTarget.Value;
                    isMoving = true;
                    moveVisualizer.Hide(); 
                    TurnManager.Instance.ExecuteTurns(actionTurnCost);
                }
            }
            else
            {
                moveVisualizer.Hide();
            }
        }
        else
        {
            moveVisualizer.Hide();
        }
    }

    private void FixedUpdate()
    {
        if (isMoving && TurnManager.Instance.IsExecuting)
        {
            // MovePosition handles kinematic translation safely.
            Vector3 newPos = Vector3.MoveTowards(rb.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            if (Vector3.Distance(rb.position, targetPosition) < 0.1f) 
            {
                isMoving = false;
            }
        }
    }

    private Vector3? GetClampedMouseTarget()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 rawTarget = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            Vector3 direction = rawTarget - transform.position;
            
            if (direction.magnitude > maxMoveDistance) 
                return transform.position + direction.normalized * maxMoveDistance;

            return rawTarget;
        }

        return null;
    }
}