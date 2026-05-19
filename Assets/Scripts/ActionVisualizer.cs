using UnityEngine;

/// <summary>
///     A modular component to draw intent lines (movement, aiming) using a LineRenderer.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ActionVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        
        // Setup default LineRenderer settings via code to prevent inspector mistakes
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        lineRenderer.useWorldSpace = true;
    }

    /// <summary>
    ///     Draws a line from point A to point B.
    ///     Time Complexity: O(1).
    /// </summary>
    public void DrawIntent(Vector3 start, Vector3 end)
    {
        if (!lineRenderer.enabled) lineRenderer.enabled = true;
        
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    /// <summary>
    ///     Hides the visualizer.
    /// </summary>
    public void Hide()
    {
        if (lineRenderer.enabled) lineRenderer.enabled = false;
    }
}