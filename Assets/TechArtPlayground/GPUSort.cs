using UnityEngine;

namespace TechArtPlayground
{
    public static class GPUSort
    {
        private static readonly int Block = Shader.PropertyToID("block");
        private static readonly int Step = Shader.PropertyToID("step");
        private static readonly int Count = Shader.PropertyToID("count");
        private static readonly int SortBuffer = Shader.PropertyToID("SortBuffer");

        /// <summary>
        ///     Executes a Bitonic Sort on the GPU.
        /// </summary>
        public static void Sort(ComputeShader sortShader, GraphicsBuffer sortBuffer, int paddedCount)
        {
            int kernel = sortShader.FindKernel("BitonicSort");
            int threadGroups = Mathf.CeilToInt(paddedCount / 256f);

            // OPTIMIZATION: Hoist static properties outside the loops!
            // We only need to tell the GPU what buffer and count to use ONCE per full sort.
            sortShader.SetInt(Count, paddedCount);
            sortShader.SetBuffer(kernel, SortBuffer, sortBuffer);

            // Bitonic Sort Algorithm
            for (int k = 2; k <= paddedCount; k <<= 1) // Block size
            for (int j = k >> 1; j > 0; j >>= 1) // Step size
            {
                // Only update the variables that actually change per step
                sortShader.SetInt(Block, k);
                sortShader.SetInt(Step, j);

                sortShader.Dispatch(kernel, threadGroups, 1, 1);
            }
        }

        public struct BoidHashPair
        {
            public uint boidIndex;
            public uint cellHash;
        }
    }
}