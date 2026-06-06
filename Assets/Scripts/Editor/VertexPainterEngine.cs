using System;
using UnityEngine;

namespace Editor
{
    // Bitmask for channel selection
    [Flags]
    public enum ColorChannel
    {
        None = 0,
        R = 1 << 0,
        G = 1 << 1,
        B = 1 << 2,
        A = 1 << 3,
        All = ~0
    }

    public enum PaintMode
    {
        Replace,
        Add
    }

    [Serializable]
    public struct BrushSettings
    {
        public float radius;
        public float opacity;
        public float falloff;
        public Color32 targetColor;
        public ColorChannel channelMask;
        public PaintMode mode;
    }


    public class VertexPainterEngine
    {
        /// <summary>
        ///     Applies a brush stroke to the mesh. Time Complexity: O(V) where V is vertex count.
        /// </summary>
        public void ApplyBrush(MeshFilter meshFilter, Vector3 hitPoint, BrushSettings brush)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) return;

            // Fetch colors. If none exist, initialize the array to white.
            Color32[] colors = mesh.colors32;
            if (colors.Length == 0)
            {
                colors = new Color32[mesh.vertexCount];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
            }

            Vector3[] vertices = mesh.vertices;
            Transform transform = meshFilter.transform;
            bool modified = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                // Convert local vertex to world space for distance checking
                Vector3 worldPos = transform.TransformPoint(vertices[i]);
                float distance = Vector3.Distance(worldPos, hitPoint);

                if (distance <= brush.radius)
                {
                    float weight = CalculateFalloff(distance, brush.radius, brush.falloff) * brush.opacity;
                    colors[i] = BlendColors(colors[i], brush.targetColor, weight, brush.channelMask, brush.mode);
                    modified = true;
                }
            }

            if (modified)
            {
                mesh.colors32 = colors;
                mesh.UploadMeshData(false); // Unity 6.4 best practice to push to GPU immediately
            }
        }

        /// <summary>
        ///     Replaces all instances of a color across the entire mesh.
        /// </summary>
        public void MassReplaceColor(Mesh mesh, Color32 targetColor, Color32 replacementColor, float tolerance)
        {
            if (mesh == null) return;

            Color32[] colors = mesh.colors32;
            if (colors.Length == 0) return;

            bool modified = false;

            for (int i = 0; i < colors.Length; i++)
                if (ColorDistance(colors[i], targetColor) <= tolerance)
                {
                    colors[i] = replacementColor;
                    modified = true;
                }

            if (modified)
            {
                mesh.colors32 = colors;
                mesh.UploadMeshData(false);
            }
        }

        // --- Math & Blending Helpers ---

        private static float CalculateFalloff(float distance, float radius, float falloff)
        {
            float normalizedDist = distance / radius;
            return Mathf.SmoothStep(1f, 0f, Mathf.Pow(normalizedDist, falloff));
        }

        private static Color32 BlendColors(Color32 current, Color32 target, float weight, ColorChannel mask,
            PaintMode mode)
        {
            byte r = (mask & ColorChannel.R) != 0 ? BlendChannel(current.r, target.r, weight, mode) : current.r;
            byte g = (mask & ColorChannel.G) != 0 ? BlendChannel(current.g, target.g, weight, mode) : current.g;
            byte b = (mask & ColorChannel.B) != 0 ? BlendChannel(current.b, target.b, weight, mode) : current.b;
            byte a = (mask & ColorChannel.A) != 0 ? BlendChannel(current.a, target.a, weight, mode) : current.a;

            return new Color32(r, g, b, a);
        }

        private static byte BlendChannel(byte current, byte target, float weight, PaintMode mode)
        {
            float c = current / 255f;
            float t = target / 255f;
            float result = c;

            if (mode == PaintMode.Replace)
                result = Mathf.Lerp(c, t, weight);
            else if (mode == PaintMode.Add)
                result = Mathf.Clamp01(c + t * weight);

            return (byte)(result * 255f);
        }

        private float ColorDistance(Color32 c1, Color32 c2)
        {
            return Vector4.Distance(
                new Vector4(c1.r, c1.g, c1.b, c1.a),
                new Vector4(c2.r, c2.g, c2.b, c2.a)
            ) / 255f;
        }
    }
}