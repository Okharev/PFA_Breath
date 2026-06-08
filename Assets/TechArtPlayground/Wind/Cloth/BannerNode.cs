using UnityEngine;

namespace TechArtPlayground.Wind.Cloth
{
    public class PhysicsBannerNode : MonoBehaviour
    {
        [Header("Appearance")]
        public Mesh clothMesh;
        public Material clothMaterial;

        [Header("Physics Painting (Vertex Colors)")]
        [Tooltip("Red = Inverse Mass (0 = pinned, 1 = free).\nGreen = Stiffness multiplier.\nBlue = Self-Collision Mask.")]
        [TextArea] public string info = "Paint vertex colors on your mesh in DCC software (Blender/Maya) to control simulation properties.";
    }
}