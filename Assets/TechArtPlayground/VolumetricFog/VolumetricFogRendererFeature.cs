using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;
using TechArtPlayground.VolumetricFog;

public class VolumetricFogRendererFeature : ScriptableRendererFeature
{
    class VolumetricFogPass : ScriptableRenderPass
    {
        private Material fogMaterial;
        
        // Statically capped for shader loop performance
        private const int MAX_PARTICLES = 32; 
        private Vector4[] particlePositions = new Vector4[MAX_PARTICLES]; // xyz: pos, w: radius
        private float[] particleDensities = new float[MAX_PARTICLES];

        private class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public VolumetricFogPass(Material material)
        {
            fogMaterial = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (fogMaterial == null || !resourceData.activeColorTexture.IsValid()) return;

            var fogVolume = VolumeManager.instance.stack.GetComponent<VolumetricFogVolumeComponent>();
            if (fogVolume == null || !fogVolume.IsActive()) return;

            // --- Send Particle Data to Material ---
            int count = Mathf.Min(VolumetricFogParticle.ActiveParticles.Count, MAX_PARTICLES);
            
            for (int i = 0; i < count; i++)
            {
                var p = VolumetricFogParticle.ActiveParticles[i];
                Vector3 pos = p.transform.position;
                particlePositions[i] = new Vector4(pos.x, pos.y, pos.z, p.radius);
                particleDensities[i] = p.densityMultiplier;
            }

            fogMaterial.SetInt("_ParticleCount", count);
            if (count > 0)
            {
                fogMaterial.SetVectorArray("_ParticlePositions", particlePositions);
                fogMaterial.SetFloatArray("_ParticleDensities", particleDensities);
            }
            // --------------------------------------

            fogMaterial.SetFloat("_Anisotropy", fogVolume.anisotropy.value);
            fogMaterial.SetFloat("_Density", fogVolume.density.value);
            fogMaterial.SetFloat("_MaxDistance", fogVolume.maxDistance.value);
            fogMaterial.SetInt("_MaxSteps", fogVolume.stepCount.value);
            fogMaterial.SetColor("_Tint", fogVolume.colorTint.value);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog with Particles", out var passData))
            {
                passData.source = resourceData.activeColorTexture;
                passData.material = fogMaterial;

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => 
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }
        }
    }

    public Material fogMaterial;
    private VolumetricFogPass fogPass;

    public override void Create()
    {
        if (fogMaterial != null) fogPass = new VolumetricFogPass(fogMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (fogPass != null && (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView))
        {
            renderer.EnqueuePass(fogPass);
        }
    }
}