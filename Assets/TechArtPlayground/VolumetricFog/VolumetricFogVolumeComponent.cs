using UnityEngine;
using UnityEngine.Rendering;

namespace TechArtPlayground.VolumetricFog
{
    [System.Serializable, VolumeComponentMenu("Custom/Volumetric Fog (Advanced)")]
    public class VolumetricFogVolumeComponent : VolumeComponent, IPostProcessComponent
    {
        [Header("Density & Scattering")]
        public MinFloatParameter density = new MinFloatParameter(0.0f, 0f);
        [Tooltip("Forward scattering intensity. Closer to 1.0 means tighter, brighter rays when looking at the sun.")]
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.85f, -0.99f, 0.99f);
        public ColorParameter colorTint = new ColorParameter(Color.white);
        [Tooltip("The color of the fog at the outer edges of the light shaft cone.")]
        public ColorParameter edgeColor = new ColorParameter(new Color(0.4f, 0.45f, 0.5f));
        
        [Tooltip("Artistically overdriven intensity for the light shafts. Does not affect scene lighting.")]
        public MinFloatParameter rayIntensity = new MinFloatParameter(1.0f, 0.0f);

        [Header("Height Falloff")]
        public FloatParameter baseHeight = new FloatParameter(0.0f);
        [Tooltip("How fast the fog thins out as altitude increases. Set to 0 for uniform height.")]
        public MinFloatParameter heightFalloff = new MinFloatParameter(0.05f, 0f);

        [Header("Ambient Global Illumination")]
        [Tooltip("Scales how much the fog absorbs baked or dynamic light probes/ambient sky colors in shadowed areas.")]
        public ClampedFloatParameter ambientMultiplier = new ClampedFloatParameter(0.5f, 0.0f, 2.0f);

        [Header("3D Noise (Wisps)")]
        [Tooltip("Assign your baked 3D Texture asset here.")]
        public Texture3DParameter noiseTexture = new Texture3DParameter(null);
        public FloatParameter noiseScale = new FloatParameter(0.05f);
        public ClampedFloatParameter noiseIntensity = new ClampedFloatParameter(0.8f, 0.0f, 1.0f);
        public Vector3Parameter windVelocity = new Vector3Parameter(new Vector3(0.2f, -0.05f, 0.2f));

        [Header("Raymarching Performance")]
        public MinFloatParameter maxDistance = new MinFloatParameter(100f, 1f);
        [Tooltip("With IGN Dithering enabled, 24-32 steps is typically plenty for crisp, performant rays.")]
        public ClampedIntParameter stepCount = new ClampedIntParameter(32, 8, 128);

        public bool IsActive() => density.value > 0f;
        public bool IsTileCompatible() => false;
    }
}