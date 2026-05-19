using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A custom UI Toolkit element that draws a radial progress bar using the Vector API.
/// Updated for Unity 6.4 using the modern [UxmlElement] source generator pattern.
/// </summary>
[UxmlElement] // 1. Tells the UI Builder to register this element
public partial class RadialOxygenBar : VisualElement // 2. MUST be partial in Unity 6+
{
    // Backing fields
    private float progress = 1f;
    private float thickness = 8f;
    private Color trackColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
    private Color fillColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    // 3. Expose properties to the UI Builder Inspector using [UxmlAttribute]
    [UxmlAttribute("progress")]
    public float Progress
    {
        get => progress;
        set { progress = Mathf.Clamp01(value); MarkDirtyRepaint(); }
    }

    [UxmlAttribute("thickness")]
    public float Thickness
    {
        get => thickness;
        set { thickness = Mathf.Max(1f, value); MarkDirtyRepaint(); }
    }

    [UxmlAttribute("track-color")]
    public Color TrackColor
    {
        get => trackColor;
        set { trackColor = value; MarkDirtyRepaint(); }
    }

    [UxmlAttribute("fill-color")]
    public Color FillColor
    {
        get => fillColor;
        set { fillColor = value; MarkDirtyRepaint(); }
    }

    public RadialOxygenBar()
    {
        generateVisualContent += OnGenerateVisualContent;
    }

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        if (contentRect.width < 0.1f || contentRect.height < 0.1f) return;

        Painter2D painter = ctx.painter2D;
        float halfWidth = contentRect.width * 0.5f;
        float halfHeight = contentRect.height * 0.5f;
        
        Vector2 center = new Vector2(halfWidth, halfHeight);
        float radius = Mathf.Min(halfWidth, halfHeight) - (thickness * 0.5f);

        // Background Track
        painter.strokeColor = trackColor;
        painter.lineWidth = thickness;
        painter.lineCap = LineCap.Round;
        painter.BeginPath();
        painter.Arc(center, radius, 0, 360f);
        painter.Stroke();

        // Active Fill
        if (progress > 0f)
        {
            painter.strokeColor = fillColor;
            painter.lineWidth = thickness;
            painter.lineCap = LineCap.Round;

            float startAngle = -90f;
            float endAngle = startAngle + (360f * progress);

            painter.BeginPath();
            painter.Arc(center, radius, startAngle, endAngle);
            painter.Stroke();
        }
    }
}