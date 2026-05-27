using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CameraFacing : MonoBehaviour
{
    private void Update()
    {
        Camera camera = null;

#if UNITY_EDITOR
        // Editor Mode — Scene View
        if (!Application.isPlaying && SceneView.lastActiveSceneView != null)
        {
            camera = SceneView.lastActiveSceneView.camera;
        }
#endif

        // Play Mode — Main Camera
        if (camera == null)
        {
            camera = Camera.main;
        }

        if (camera != null)
        {
             transform.forward = camera.transform.forward;
        }
    }
}