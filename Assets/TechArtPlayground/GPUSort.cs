using UnityEngine;
using UnityEngine.Rendering;

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
        /// Executes an O(N) 8-Pass Stable Radix Sort on the GPU (4-bit digits).
        /// </summary>
        public static void RadixSort(CommandBuffer cmd, ComputeShader radixShader, GraphicsBuffer sortBuffer, GraphicsBuffer tempSortBuffer, GraphicsBuffer globalHist, GraphicsBuffer localOffsets, int paddedCount)
        {
            int kernelHistogram = radixShader.FindKernel("LocalHistogram");
            int kernelScan = radixShader.FindKernel("GlobalScan");
            int kernelScatter = radixShader.FindKernel("Scatter");

            int numBlocks = Mathf.Max(1, Mathf.CeilToInt(paddedCount / 256f));

            cmd.SetComputeIntParam(radixShader, NumElements, paddedCount);
            cmd.SetComputeIntParam(radixShader, NumBlocks, numBlocks);

            GraphicsBuffer input = sortBuffer;
            GraphicsBuffer output = tempSortBuffer;

            for (int pass = 0; pass < 8; pass++)
            {
                int bitShift = pass * 4;
                cmd.SetComputeIntParam(radixShader, BitShift, bitShift);

                // Pass 1: Local Stable Histogram
                cmd.SetComputeBufferParam(radixShader, kernelHistogram, InputBuffer, input);
                cmd.SetComputeBufferParam(radixShader, kernelHistogram, GlobalHist, globalHist);
                cmd.SetComputeBufferParam(radixShader, kernelHistogram, LocalOffsets, localOffsets);
                cmd.DispatchCompute(radixShader, kernelHistogram, numBlocks, 1, 1);

                // Pass 2: Global Scan (1 group of 16 threads)
                cmd.SetComputeBufferParam(radixShader, kernelScan, GlobalHist, globalHist);
                cmd.DispatchCompute(radixShader, kernelScan, 1, 1, 1); 

                // Pass 3: Deterministic Scatter
                cmd.SetComputeBufferParam(radixShader, kernelScatter, InputBuffer, input);
                cmd.SetComputeBufferParam(radixShader, kernelScatter, OutputBuffer, output);
                cmd.SetComputeBufferParam(radixShader, kernelScatter, GlobalHist, globalHist);
                cmd.SetComputeBufferParam(radixShader, kernelScatter, LocalOffsets, localOffsets);
                cmd.DispatchCompute(radixShader, kernelScatter, numBlocks, 1, 1);

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