using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     A reusable radial progress bar using the Vector API.
/// </summary>
[UxmlElement]
public partial class RadialProgressBar : VisualElement
{
    private Color fillColor = new(0.2f, 0.8f, 0.2f, 1f);
    private float progress = 1f;
    private float thickness = 8f;
    private Color trackColor = new(0.1f, 0.1f, 0.1f, 0.5f);

    public RadialProgressBar()
    {
        generateVisualContent += OnGenerateVisualContent;
    }

    [UxmlAttribute("progress")]
    public float Progress
    {
        get => progress;
        set
        {
            progress = Mathf.Clamp01(value);
            MarkDirtyRepaint();
        }
    }

    [UxmlAttribute("thickness")]
    public float Thickness
    {
        get => thickness;
        set
        {
            thickness = Mathf.Max(1f, value);
            MarkDirtyRepaint();
        }
    }

    [UxmlAttribute("track-color")]
    public Color TrackColor
    {
        get => trackColor;
        set
        {
            trackColor = value;
            MarkDirtyRepaint();
        }
    }

    [UxmlAttribute("fill-color")]
    public Color FillColor
    {
        get => fillColor;
        set
        {
            fillColor = value;
            MarkDirtyRepaint();
        }
    }

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        if (contentRect.width < 0.1f || contentRect.height < 0.1f) return;

        Painter2D painter = ctx.painter2D;
        float halfWidth = contentRect.width * 0.5f;
        float halfHeight = contentRect.height * 0.5f;

        Vector2 center = new(halfWidth, halfHeight);
        float radius = Mathf.Min(halfWidth, halfHeight) - thickness * 0.5f;

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
            float endAngle = startAngle + 360f * progress;

            painter.BeginPath();
            painter.Arc(center, radius, startAngle, endAngle);
            painter.Stroke();
        }
    }
}