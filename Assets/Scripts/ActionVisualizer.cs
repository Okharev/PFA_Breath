using UnityEngine;

/// <summary>
/// A modular component to draw intents (movement, aiming) using a LineRenderer and custom shaders.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ActionVisualizer : MonoBehaviour
{
    public enum IntentType { Movement, Shooting }

    [Header("Materials")]
    [Tooltip("Assign your custom Shader Graph material for movement.")]
    public Material movementMaterial;
    [Tooltip("Assign your custom Shader Graph material for shooting/aiming.")]
    public Material shootingMaterial;

    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        lineRenderer.useWorldSpace = true;
    }

    /// <summary>
    /// Draws an intent line and swaps to the appropriate shader material.
    /// Time Complexity: O(1).
    /// </summary>
    public void DrawIntent(Vector3 start, Vector3 end, IntentType type)
    {
        // Swap material based on intent to leverage different shader effects
        Material targetMat = type == IntentType.Shooting ? shootingMaterial : movementMaterial;
        
        if (lineRenderer.sharedMaterial != targetMat)
        {
            lineRenderer.sharedMaterial = targetMat;
        }

        // Calculate texture tiling based on distance so dashes don't stretch
        float distance = Vector3.Distance(start, end);
        lineRenderer.material.mainTextureScale = new Vector2(distance, 1f);

        if (!lineRenderer.enabled) lineRenderer.enabled = true;

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    public void Hide()
    {
        if (lineRenderer.enabled) lineRenderer.enabled = false;
    }
}