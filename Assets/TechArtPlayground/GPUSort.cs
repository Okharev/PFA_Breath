using UnityEngine;

namespace TechArtPlayground
{
    public static class GPUSort
    {
        private static readonly int Block = Shader.PropertyToID("block");
        private static readonly int Step = Shader.PropertyToID("step");
        private static readonly int Count = Shader.PropertyToID("count");
        private static readonly int SortBuffer = Shader.PropertyToID("SortBuffer");

                private static readonly int InputBuffer = Shader.PropertyToID("InputBuffer");
        private static readonly int OutputBuffer = Shader.PropertyToID("OutputBuffer");
        private static readonly int GlobalHist = Shader.PropertyToID("GlobalHist");
        private static readonly int LocalOffsets = Shader.PropertyToID("LocalOffsets");
        private static readonly int NumElements = Shader.PropertyToID("numElements");
        private static readonly int NumBlocks = Shader.PropertyToID("numBlocks");
        private static readonly int BitShift = Shader.PropertyToID("bitShift");

        /// <summary>
        /// Executes an O(N) 4-Pass Radix Sort on the GPU (8-bit digits).
        /// </summary>
        public static void RadixSort(ComputeShader radixShader, GraphicsBuffer sortBuffer,
            GraphicsBuffer tempSortBuffer, GraphicsBuffer globalHist, GraphicsBuffer localOffsets, int paddedCount)
        {
            int kernelHistogram = radixShader.FindKernel("LocalHistogram");
            int kernelScan = radixShader.FindKernel("GlobalScan");
            int kernelScatter = radixShader.FindKernel("Scatter");

            // 256 threads per group
            int numBlocks = Mathf.Max(1, Mathf.CeilToInt(paddedCount / 256f));

            radixShader.SetInt(NumElements, paddedCount);
            radixShader.SetInt(NumBlocks, numBlocks);

            GraphicsBuffer input = sortBuffer;
            GraphicsBuffer output = tempSortBuffer;

            // 4 passes for 32-bit hash (shift by 0, 8, 16, 24)
            for (int pass = 0; pass < 4; pass++)
            {
                int bitShift = pass * 8;
                radixShader.SetInt(BitShift, bitShift);

                // Pass 1: Local Histogram (Write counts into GlobalHist)
                radixShader.SetBuffer(kernelHistogram, InputBuffer, input);
                radixShader.SetBuffer(kernelHistogram, GlobalHist, globalHist);
                radixShader.SetBuffer(kernelHistogram, LocalOffsets, localOffsets);
                radixShader.Dispatch(kernelHistogram, numBlocks, 1, 1);

                // Pass 2: Global Scan (Prefix sum across all blocks)
                // Dispatches exactly 1 group of 256 threads to handle the 256 digits
                radixShader.SetBuffer(kernelScan, GlobalHist, globalHist);
                radixShader.Dispatch(kernelScan, 1, 1, 1);

                // Pass 3: Scatter (Move data to Output Buffer)
                radixShader.SetBuffer(kernelScatter, InputBuffer, input);
                radixShader.SetBuffer(kernelScatter, OutputBuffer, output);
                radixShader.SetBuffer(kernelScatter, GlobalHist, globalHist);
                radixShader.SetBuffer(kernelScatter, LocalOffsets, localOffsets);
                radixShader.Dispatch(kernelScatter, numBlocks, 1, 1);

                // Ping-pong buffers
                (input, output) = (output, input);
            }
        }

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