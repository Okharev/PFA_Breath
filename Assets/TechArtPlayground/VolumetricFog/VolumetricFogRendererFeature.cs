using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TechArtPlayground.VolumetricFog
{
    public class VolumetricFogRendererFeature : ScriptableRendererFeature
    {
        class VolumetricFogPass : ScriptableRenderPass
        {
            private Material fogMaterial;
        
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
                var cameraData = frameData.Get<UniversalCameraData>();
                
                if (fogMaterial == null || !resourceData.activeColorTexture.IsValid()) return;

                var fogVolume = VolumeManager.instance.stack.GetComponent<VolumetricFogVolumeComponent>();
                if (fogVolume == null || !fogVolume.IsActive()) return;

                // Push properties to the material
                fogMaterial.SetFloat("_RayIntensity", fogVolume.rayIntensity.value);
                fogMaterial.SetFloat("_Density", fogVolume.density.value);
                fogMaterial.SetFloat("_Anisotropy", fogVolume.anisotropy.value);
                fogMaterial.SetColor("_Tint", fogVolume.colorTint.value);
                fogMaterial.SetColor("_EdgeColor", fogVolume.edgeColor.value);
            
                fogMaterial.SetFloat("_BaseHeight", fogVolume.baseHeight.value);
                
                // Math Tuning: Multiply falloff by 1 / ln(2) to prep for HLSL's exp2()
                float base2Falloff = fogVolume.heightFalloff.value * 1.442695f; 
                fogMaterial.SetFloat("_HeightFalloff", base2Falloff);
                
                fogMaterial.SetFloat("_AmbientMultiplier", fogVolume.ambientMultiplier.value);
                fogMaterial.SetFloat("_MaxDistance", fogVolume.maxDistance.value);
                fogMaterial.SetInt("_MaxSteps", fogVolume.stepCount.value);

                if (fogVolume.noiseTexture.value != null)
                {
                    fogMaterial.SetTexture("_NoiseTex", fogVolume.noiseTexture.value);
                    fogMaterial.SetFloat("_NoiseScale", fogVolume.noiseScale.value);
                    fogMaterial.SetFloat("_NoiseIntensity", fogVolume.noiseIntensity.value);
                    fogMaterial.SetVector("_WindVelocity", fogVolume.windVelocity.value);
                    fogMaterial.EnableKeyword("_VOLUMETRIC_NOISE");
                }
                else
                {
                    fogMaterial.DisableKeyword("_VOLUMETRIC_NOISE");
                }

                // 1. Allocate Half-Resolution Texture
// 1. Allocate Half-Resolution Texture
                var cameraDesc = cameraData.cameraTargetDescriptor;

// Initialize RenderGraph's specific TextureDesc struct
                TextureDesc textureDesc = new TextureDesc(
                    Mathf.Max(1, cameraDesc.width / 2),
                    Mathf.Max(1, cameraDesc.height / 2)
                );

                textureDesc.name = "HalfResFogTarget";
// Use modern GraphicsFormat for ARGBHalf (16-bit float per channel)
                textureDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat; 
                textureDesc.depthBufferBits = DepthBits.None;
// Optimization: We overwrite every pixel in the shader, so no need to clear memory
                textureDesc.clearBuffer = false; 

// Create the handle using the new struct
                TextureHandle halfResFogTarget = renderGraph.CreateTexture(textureDesc);
                
                // PASS 0: Raymarch into the half-resolution target
// PASS 0: Raymarch into the half-resolution target
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog Raymarch", out var passData))
                {
                    passData.source = resourceData.activeColorTexture; 
                    passData.material = fogMaterial;

                    // Explicitly declare that we are reading the depth buffer
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
    
                    // FIX: Declare that we are reading the active color texture (used by the Blitter for UVs)
                    builder.UseTexture(passData.source, AccessFlags.Read); 

                    // Declare our write target
                    builder.SetRenderAttachment(halfResFogTarget, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => 
                    {
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

// PASS 1: Depth-Aware Upsample and Composite
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog Composite", out var passData))
                {
                    passData.source = halfResFogTarget; 
                    passData.material = fogMaterial;

                    // Explicitly declare that we are reading the depth buffer
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
    
                    // FIX: Declare that we are reading the half-res fog target we created in Pass 0
                    builder.UseTexture(passData.source, AccessFlags.Read);

                    // Declare our write target (Outputting back to the screen)
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => 
                    {
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 1);
                    });
                }
            }
        }

        public Material fogMaterial;
        private VolumetricFogPass fogPass;

        public override void Create()
        {
            if (fogMaterial != null)
            {
                fogPass = new VolumetricFogPass(fogMaterial);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (fogPass != null && (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView))
            {
                renderer.EnqueuePass(fogPass);
            }
        }
    }
}