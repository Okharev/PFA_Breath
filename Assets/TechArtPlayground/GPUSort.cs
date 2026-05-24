using UnityEngine;

namespace TechArtPlayground
{
    public static class GPUSort
    {
        private static readonly int Block = Shader.PropertyToID("block");
        private static readonly int Step = Shader.PropertyToID("step");
        private static readonly int Count = Shader.PropertyToID("count");
        private static readonly int SortBuffer = Shader.PropertyToID("SortBuffer");

        // Struct matching the HLSL definition
        public struct BoidHashPair
        {
            public uint boidIndex;
            public uint cellHash;
        }

        /// <summary>
        /// Executes a Bitonic Sort on the GPU.
        /// </summary>
        /// <param name="sortShader">The BitonicSort Compute Shader</param>
        /// <param name="sortBuffer">The buffer containing BoidHashPairs</param>
        /// <param name="paddedCount">The size of the buffer (MUST be a Power of 2)</param>
        public static void Sort(ComputeShader sortShader, ComputeBuffer sortBuffer, int paddedCount)
        {
            int kernel = sortShader.FindKernel("BitonicSort");
            int threadGroups = Mathf.CeilToInt(paddedCount / 256f);

            // Bitonic Sort Algorithm: Nested loops controlling the GPU dispatches
            for (int k = 2; k <= paddedCount; k <<= 1) // Block size
            {
                for (int j = k >> 1; j > 0; j >>= 1) // Step size
                {
                    sortShader.SetInt(Block, k);
                    sortShader.SetInt(Step, j);
                    sortShader.SetInt(Count, paddedCount);
                    sortShader.SetBuffer(kernel, SortBuffer, sortBuffer);

                    // Dispatch a synchronization pass
                    sortShader.Dispatch(kernel, threadGroups, 1, 1);
                }
            }
        }
    }
}