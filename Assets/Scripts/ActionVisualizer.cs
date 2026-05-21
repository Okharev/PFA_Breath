using UnityEngine;

/// <summary>
///     A modular component to draw intents (movement, aiming) using LineRenderers.
///     Dynamically generates flat lines to prevent billboarding without rotating the parent object.
/// </summary>
public class ActionVisualizer : MonoBehaviour
{
    public enum IntentType
    {
        Movement,
        Shooting
    }

    [Header("Line Settings")]
    [Tooltip("The base width of the main aiming/movement line.")]
    public float baseLineWidth = 0.1f;

    [Header("Materials")] 
    [Tooltip("Assign your custom Shader Graph material for movement.")]
    public Material movementMaterial;

    [Tooltip("Assign your custom Shader Graph material for the main shooting trajectory.")]
    public Material shootingMaterial;

    [Tooltip("Assign a semi-transparent material for the spread cone area.")]
    public Material spreadMaterial;

    private LineRenderer mainLine;
    private LineRenderer coneSpreadLine; 

    private void Awake()
    {
        // 1. Generate the main precise line
        mainLine = new GameObject("MainAimLine").AddComponent<LineRenderer>();
        mainLine.transform.SetParent(transform);
        SetupFlatLineRenderer(mainLine, baseLineWidth, baseLineWidth);

        // 2. Generate the spread cone visualizer
        coneSpreadLine = new GameObject("SpreadCone").AddComponent<LineRenderer>();
        coneSpreadLine.transform.SetParent(transform);
        SetupFlatLineRenderer(coneSpreadLine, baseLineWidth, baseLineWidth);
        
        // Push the cone slightly down so it doesn't Z-fight with the main line or the floor
        coneSpreadLine.transform.localPosition = new Vector3(0, -0.01f, 0);
    }

    /// <summary>
    ///     Configures a LineRenderer to draw flat against the XZ plane.
    ///     Time Complexity: O(1) initialization overhead.
    /// </summary>
    private void SetupFlatLineRenderer(LineRenderer lr, float startWidth, float endWidth)
    {
        lr.positionCount = 2;
        lr.enabled = false;
        lr.useWorldSpace = true;
        
        // --- THE FLAT LINE FIX ---
        // Forces the line to face its local Z axis instead of the camera
        lr.alignment = LineAlignment.TransformZ; 
        
        // Rotate the transform so its Z-axis points straight up. 
        // This makes the flat side of the line perfectly parallel to the floor.
        lr.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        
        // Ensure rounded corners don't warp weirdly on flat lines
        lr.numCapVertices = 0; 
    }

    public void DrawIntent(Vector3 start, Vector3 end, IntentType type, float spreadAngle = 0f)
    {
        // 1. Draw Main Precise Line
        Material targetMat = type == IntentType.Shooting ? shootingMaterial : movementMaterial;
        if (mainLine.sharedMaterial != targetMat) mainLine.sharedMaterial = targetMat;

        float distance = Vector3.Distance(start, end);
        // mainLine.material.mainTextureScale = new Vector2(distance, 1f);

        mainLine.SetPosition(0, start);
        mainLine.SetPosition(1, end);
        mainLine.enabled = true;

        // 2. Draw Spread Cone underneath
        if (type == IntentType.Shooting && spreadAngle > 0f && spreadMaterial is not null)
        {
            if (coneSpreadLine.sharedMaterial != spreadMaterial) coneSpreadLine.sharedMaterial = spreadMaterial;

            // Convert spread angle from degrees to radians for Mathf.Tan
            float radAngle = spreadAngle * Mathf.Deg2Rad;
            
            // Calculate total width at the end of the line
            float endWidth = 2f * distance * Mathf.Tan(radAngle);

            coneSpreadLine.startWidth = mainLine.startWidth; 
            coneSpreadLine.endWidth = endWidth;              

            coneSpreadLine.SetPosition(0, start);
            coneSpreadLine.SetPosition(1, end);
            coneSpreadLine.enabled = true;
        }
        else
        {
            coneSpreadLine.enabled = false;
        }
    }

    public void Hide()
    {
        mainLine.enabled = false;
        coneSpreadLine.enabled = false;
    }
}