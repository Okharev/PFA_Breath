using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TechArtPlayground.Water
{
    [RequireComponent(typeof(Transform))]
    public class PlanarReflectionManager : MonoBehaviour
    {
        [Header("Reflection Settings")]
        [Tooltip("What layers should be reflected? Turn off particles, small props, and the water layer itself.")]
        public LayerMask reflectionMask = -1;
        
        [Range(0.1f, 1.0f)]
        [Tooltip("Resolution scale of the reflection. 0.5 is 50% of screen resolution. Lower is much faster.")]
        public float resolutionScale = 0.5f;

        [Tooltip("Offsets the mathematical clipping plane to prevent shoreline artifacts.")]
        public float clipPlaneOffset = 0.07f;

        [Header("Optimizations")]
        public bool disableShadows = true;
        public bool disablePostProcessing = true;

        private Camera _reflectionCamera;
        private RenderTexture _reflectionTexture;
        private static readonly int ReflectionTexId = Shader.PropertyToID("_PlanarReflectionTex");

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += ExecutePlanarReflections;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ExecutePlanarReflections;
            Cleanup();
        }

        private void Cleanup()
        {
            if (_reflectionCamera != null)
            {
                _reflectionCamera.targetTexture = null;
                SafeDestroy(_reflectionCamera.gameObject);
            }
            if (_reflectionTexture != null)
            {
                RenderTexture.ReleaseTemporary(_reflectionTexture);
                _reflectionTexture = null;
            }
        }

        private void ExecutePlanarReflections(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
                return;

            if (_reflectionCamera != null && camera == _reflectionCamera)
                return;

            UpdateReflectionCamera(camera);
            
            UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera);
            Shader.SetGlobalTexture(ReflectionTexId, _reflectionTexture);
        }

        private void UpdateReflectionCamera(Camera srcCamera)
        {
            if (_reflectionCamera == null)
                _reflectionCamera = CreateReflectionCamera(srcCamera);

            // Match Main Camera Settings
            _reflectionCamera.CopyFrom(srcCamera);
            
            // CRITICAL FIX 1: Prevent Unity from raycasting UI/Mouse events through the warped reflection matrix.
            // This stops the "Screen position out of view frustum" error in its tracks.
            _reflectionCamera.cameraType = CameraType.Reflection; 
            
            _reflectionCamera.cullingMask = reflectionMask;
            _reflectionCamera.useOcclusionCulling = false;

            // Apply Optimizations
            var urpData = _reflectionCamera.GetComponent<UniversalAdditionalCameraData>();
            if (urpData != null)
            {
                urpData.renderShadows = !disableShadows;
                urpData.renderPostProcessing = !disablePostProcessing;
                urpData.requiresColorOption = CameraOverrideOption.Off;
                urpData.requiresDepthOption = CameraOverrideOption.Off;
            }

            // Create/Resize RenderTexture if needed (Safeguarded against 0x0 Editor Glitches)
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(srcCamera.pixelWidth * resolutionScale));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(srcCamera.pixelHeight * resolutionScale));

            if (_reflectionTexture == null || _reflectionTexture.width != targetWidth || _reflectionTexture.height != targetHeight)
            {
                if (_reflectionTexture != null) RenderTexture.ReleaseTemporary(_reflectionTexture);
                
                _reflectionTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 16, RenderTextureFormat.DefaultHDR);
                _reflectionTexture.filterMode = FilterMode.Bilinear;
            }

            _reflectionCamera.targetTexture = _reflectionTexture;

            // --- MATH: Calculate the Reflection Matrix ---
            Vector3 normal = transform.up;
            Vector3 pos = transform.position;

            // CRITICAL FIX 2: Safeguard against degenerate matrices if looking perfectly parallel to the water
            if (Mathf.Abs(Vector3.Dot(srcCamera.transform.forward, normal)) < 0.001f) return;

            float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
            Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            Matrix4x4 reflection = Matrix4x4.identity;
            reflection.m00 = (1F - 2F * reflectionPlane[0] * reflectionPlane[0]);
            reflection.m01 = (   - 2F * reflectionPlane[0] * reflectionPlane[1]);
            reflection.m02 = (   - 2F * reflectionPlane[0] * reflectionPlane[2]);
            reflection.m03 = (   - 2F * reflectionPlane[3] * reflectionPlane[0]);

            reflection.m10 = (   - 2F * reflectionPlane[1] * reflectionPlane[0]);
            reflection.m11 = (1F - 2F * reflectionPlane[1] * reflectionPlane[1]);
            reflection.m12 = (   - 2F * reflectionPlane[1] * reflectionPlane[2]);
            reflection.m13 = (   - 2F * reflectionPlane[3] * reflectionPlane[1]);

            reflection.m20 = (   - 2F * reflectionPlane[2] * reflectionPlane[0]);
            reflection.m21 = (   - 2F * reflectionPlane[2] * reflectionPlane[1]);
            reflection.m22 = (1F - 2F * reflectionPlane[2] * reflectionPlane[2]);
            reflection.m23 = (   - 2F * reflectionPlane[3] * reflectionPlane[2]);

            reflection.m30 = 0F;
            reflection.m31 = 0F;
            reflection.m32 = 0F;
            reflection.m33 = 1F;

            _reflectionCamera.worldToCameraMatrix = srcCamera.worldToCameraMatrix * reflection;

            // --- MATH: Calculate the Oblique Projection Matrix ---
            Vector4 cameraSpacePlane = CameraSpacePlane(_reflectionCamera, pos, normal, 1.0f);
            Matrix4x4 projection = srcCamera.CalculateObliqueMatrix(cameraSpacePlane);
            _reflectionCamera.projectionMatrix = projection;

            // CRITICAL FIX 3: Rigorously align the Unity Transform to Match the custom Matrix
            // URP uses the physical transform for Frustum culling bounds.
            _reflectionCamera.transform.position = reflection.MultiplyPoint(srcCamera.transform.position);
            Vector3 forward = reflection.MultiplyVector(srcCamera.transform.forward);
            Vector3 up = reflection.MultiplyVector(srcCamera.transform.up);
            
            if (forward.sqrMagnitude > 0.001f)
            {
                _reflectionCamera.transform.rotation = Quaternion.LookRotation(forward, up);
            }
        }

        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            Vector3 offsetPos = pos + normal * clipPlaneOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cPos = m.MultiplyPoint(offsetPos);
            Vector3 cNormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
        }

        private Camera CreateReflectionCamera(Camera srcCamera)
        {
            GameObject go = new GameObject("WaterReflectionCamera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            go.hideFlags = HideFlags.HideAndDontSave; 
            
            Camera cam = go.GetComponent<Camera>();
            cam.enabled = false; 
            // We set the type again here just for good measure upon initialization
            cam.cameraType = CameraType.Reflection; 
            return cam;
        }

        private void SafeDestroy(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}