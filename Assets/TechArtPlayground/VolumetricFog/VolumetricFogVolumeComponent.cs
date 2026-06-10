using UnityEngine.Rendering;

namespace TechArtPlayground.VolumetricFog
{
    [System.Serializable, VolumeComponentMenu("Custom/Volumetric Fog")]
    public class VolumetricFogVolumeComponent : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.7f, -0.99f, 0.99f);
        public MinFloatParameter density = new MinFloatParameter(0.0f, 0f);
        public MinFloatParameter maxDistance = new MinFloatParameter(100f, 1f);
        public ClampedIntParameter stepCount = new ClampedIntParameter(32, 8, 128);
        public ColorParameter colorTint = new ColorParameter(UnityEngine.Color.white);

        // The pass will only inject if density is greater than 0
        public bool IsActive() => density.value > 0f;
        public bool IsTileCompatible() => false;
    }
}