using UnityEngine;

namespace TechArtPlayground.Water
{
    [CreateAssetMenu(fileName = "NewWaterProfile", menuName = "StylizedEnvironment/Water Profile")]
    public class WaterProfile : ScriptableObject
    {
        [Header("Wave Parameters")]
        public float waveSpeed = 1.5f;
        public float waveHeight = 2.0f;
        public float waveFrequency = 0.5f;
        public Vector2 waveDirection = new Vector2(1, 0.5f).normalized;

        [Header("Styling")]
        [ColorUsage(false, true)] public Color waterColor;
        [ColorUsage(false, true)] public Color foamColor;
    }
}