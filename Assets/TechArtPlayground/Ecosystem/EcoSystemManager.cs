using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TechArtPlayground
{
    [DefaultExecutionOrder(-50)]
    public class EcosystemManager : MonoBehaviour
    {
        private static readonly int GridOffsets = Shader.PropertyToID("gridOffsets");
        private static readonly int SortBuffer = Shader.PropertyToID("SortBuffer");
        private static readonly int ReadBoidsBuffer = Shader.PropertyToID("ReadBoidsBuffer");
        private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int WriteBoidsBuffer = Shader.PropertyToID("WriteBoidsBuffer");
        private static readonly int BoidsBuffer = Shader.PropertyToID("boidsBuffer");
        private static readonly int SpeciesOffset = Shader.PropertyToID("_SpeciesOffset");
        private static readonly int EcosystemCenter = Shader.PropertyToID("ecosystemCenter");
        private static readonly int EcosystemRadius = Shader.PropertyToID("ecosystemRadius");

        // --- REFERENCES ---
        [Header("Compute & Sorting")] public ComputeShader ecosystemCompute;

        public ComputeShader bitonicSortCompute;
        [Range(1, 10)] public int sortFrequency = 4;


        [Header("Global Ecosystem Bounds")]
        public Transform ecosystemCenter;
        public float ecosystemRadius = 80f;
        
        
        [Header("Grid Settings")] public float cellSize = 3f;

        public int gridSize = 64;

        [Header("Ecosystem Configuration")] public List<SpeciesSetup> speciesSettings = new();

        public List<SchoolSetup> schoolSettings = new();

        // Buffers
        private GraphicsBuffer boidsBufferA, boidsBufferB, currentBuffer, nextBuffer;

        // Kernels
        private int clearGridKernel, populateHashesKernel, buildOffsetsKernel, csMainKernel;
        private int frameCount;
        private GraphicsBuffer gridOffsetsBuffer, sortBuffer;
        private int paddedCount;

        private readonly Bounds renderBounds = new(Vector3.zero, Vector3.one * 2000f);
        private GraphicsBuffer speciesBuffer, schoolBuffer;

        // --- INTERNAL DATA ---
        private int totalBoids;

        private void Start()
        {
            // 1. KERNEL SETUP
            clearGridKernel = ecosystemCompute.FindKernel("ClearGrid");
            populateHashesKernel = ecosystemCompute.FindKernel("PopulateHashes");
            buildOffsetsKernel = ecosystemCompute.FindKernel("BuildGridOffsets");
            csMainKernel = ecosystemCompute.FindKernel("CSMain");

            // 2. CALCULATE TOTAL BOIDS
            totalBoids = 0;
            foreach (SchoolSetup school in schoolSettings) totalBoids += school.boidCount;

            if (totalBoids == 0) return;

            paddedCount = Mathf.NextPowerOfTwo(totalBoids);
            EcosystemBoid[] boidsData = new EcosystemBoid[totalBoids];

            // 3. BUILD PROFILE ARRAYS FOR GPU
            SpeciesProfile[] speciesProfiles = new SpeciesProfile[speciesSettings.Count];
            for (int i = 0; i < speciesSettings.Count; i++)
            {
                speciesProfiles[i] = speciesSettings[i].profile;
                speciesSettings[i].count = 0; // Reset just in case
            }

            SchoolProfile[] schoolProfiles = new SchoolProfile[schoolSettings.Count];
            for (int i = 0; i < schoolSettings.Count; i++)
                schoolProfiles[i] = new SchoolProfile
                {

                    speciesID = (uint)schoolSettings[i].speciesIndex
                };

            // 4. GENERATE BOIDS CONTIGUOUSLY BY SPECIES
            // (This ensures that even if you order your schools randomly in the inspector, 
            // the memory is grouped by species so the rendering offsets work perfectly).
            int currentIndex = 0;
            for (int spIndex = 0; spIndex < speciesSettings.Count; spIndex++)
            {
                SpeciesSetup species = speciesSettings[spIndex];
                species.offset = currentIndex;

                for (int scIndex = 0; scIndex < schoolSettings.Count; scIndex++)
                    if (schoolSettings[scIndex].speciesIndex == spIndex)
                    {
                        int count = schoolSettings[scIndex].boidCount;
                        GenerateBoidsForSchool(boidsData, ref currentIndex, count, (uint)scIndex,
                            schoolProfiles[scIndex]);
                        species.count += count;
                    }

                // Create Draw Arguments for this species
                if (species.count > 0 && species.mesh != null)
                    species.argsBuffer = CreateArgsBuffer(species.mesh, species.count);
            }

            // 5. INITIALIZE BUFFERS
            int boidStride = Marshal.SizeOf(typeof(EcosystemBoid));
            boidsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalBoids, boidStride);
            boidsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalBoids, boidStride);
            boidsBufferA.SetData(boidsData);
            boidsBufferB.SetData(boidsData);
            currentBuffer = boidsBufferA;
            nextBuffer = boidsBufferB;

            speciesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, speciesProfiles.Length,
                Marshal.SizeOf(typeof(SpeciesProfile)));
            speciesBuffer.SetData(speciesProfiles);

            schoolBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, schoolProfiles.Length,
                Marshal.SizeOf(typeof(SchoolProfile)));
            schoolBuffer.SetData(schoolProfiles);

            int totalCells = gridSize * gridSize * gridSize;
            gridOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalCells, sizeof(int));
            sortBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount,
                Marshal.SizeOf(typeof(GPUSort.BoidHashPair)));

            // 6. BIND STATIC COMPUTE VARIABLES
            ecosystemCompute.SetInt("numBoids", totalBoids);
            ecosystemCompute.SetInt("paddedCount", paddedCount);
            ecosystemCompute.SetFloat("cellSize", cellSize);
            ecosystemCompute.SetInt("gridSize", gridSize);

            ecosystemCompute.SetBuffer(clearGridKernel, "gridOffsets", gridOffsetsBuffer);

            ecosystemCompute.SetBuffer(csMainKernel, "speciesBuffer", speciesBuffer);
            ecosystemCompute.SetBuffer(csMainKernel, "schoolBuffer", schoolBuffer);
            ecosystemCompute.SetBuffer(csMainKernel, "gridOffsets", gridOffsetsBuffer);
            ecosystemCompute.SetBuffer(csMainKernel, "SortBuffer", sortBuffer);
        }

        private void Update()
        {
            if (totalBoids == 0) return;

            frameCount++;
            ecosystemCompute.SetFloat(DeltaTime, Time.deltaTime);

            int gridThreadGroups = (gridSize * gridSize * gridSize + 63) / 64;
            int boidThreadGroups = (totalBoids + 63) / 64;
            int paddedThreadGroups = (paddedCount + 63) / 64;

            // --- 1. SPATIAL HASH SORTING ---
            if (frameCount % sortFrequency == 0)
            {
                ecosystemCompute.Dispatch(clearGridKernel, gridThreadGroups, 1, 1);

                ecosystemCompute.SetBuffer(populateHashesKernel, ReadBoidsBuffer, currentBuffer);
                ecosystemCompute.SetBuffer(populateHashesKernel, SortBuffer, sortBuffer);
                ecosystemCompute.Dispatch(populateHashesKernel, paddedThreadGroups, 1, 1);

                GPUSort.Sort(bitonicSortCompute, sortBuffer, paddedCount);

                ecosystemCompute.SetBuffer(buildOffsetsKernel, SortBuffer, sortBuffer);
                ecosystemCompute.SetBuffer(buildOffsetsKernel, GridOffsets, gridOffsetsBuffer);
                ecosystemCompute.Dispatch(buildOffsetsKernel, boidThreadGroups, 1, 1);
            }

            // --- 2. PHYSICS SIMULATION ---
            if (ecosystemCenter != null) {
                ecosystemCompute.SetVector(EcosystemCenter, ecosystemCenter.position);
            } else {
                ecosystemCompute.SetVector(EcosystemCenter, Vector3.zero);
            }
            ecosystemCompute.SetFloat(EcosystemRadius, ecosystemRadius);

            ecosystemCompute.SetBuffer(csMainKernel, ReadBoidsBuffer, currentBuffer);
            
            ecosystemCompute.SetBuffer(csMainKernel, ReadBoidsBuffer, currentBuffer);
            ecosystemCompute.SetBuffer(csMainKernel, WriteBoidsBuffer, nextBuffer);
            ecosystemCompute.Dispatch(csMainKernel, boidThreadGroups, 1, 1);

            (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);

            // --- 3. RENDER ALL SPECIES ---
            foreach (SpeciesSetup species in speciesSettings)
                if (species.count > 0 && species.material != null && species.mesh != null)
                    RenderSpecies(species.material, species.mesh, species.argsBuffer, species.offset);
        }

        private void OnDestroy()
        {
            boidsBufferA?.Release();
            boidsBufferB?.Release();
            speciesBuffer?.Release();
            schoolBuffer?.Release();
            gridOffsetsBuffer?.Release();
            sortBuffer?.Release();

            foreach (SpeciesSetup sp in speciesSettings) sp.argsBuffer?.Release();
        }

        private void RenderSpecies(Material mat, Mesh mesh, GraphicsBuffer args, int bufferOffset)
        {
            mat.SetBuffer(BoidsBuffer, currentBuffer);
            mat.SetFloat(SpeciesOffset, bufferOffset);

            RenderParams rp = new(mat) { worldBounds = renderBounds };
            Graphics.RenderMeshIndirect(rp, mesh, args);
        }

        // --- UTILITIES ---
        private void GenerateBoidsForSchool(EcosystemBoid[] array, ref int index, int count, uint schoolID, SchoolProfile school)
        {
            // 1. Safely get the global center
            Vector3 globalCenter = (ecosystemCenter != null) ? ecosystemCenter.position : Vector3.zero;

            // 2. Pick a random starting location for this specific school 
            // (We multiply by 0.8f so the school doesn't spawn exactly on the very edge of the boundary)
            Vector3 schoolStartPos = globalCenter + Random.insideUnitSphere * (ecosystemRadius * 0.8f);
    
            // How tightly packed the school is when they first spawn
            float initialGroupingRadius = 5f; 

            for (int i = 0; i < count; i++)
            {
                // 3. Spawn each boid close to the school's starting position
                array[index].position = schoolStartPos + Random.insideUnitSphere * initialGroupingRadius;
        
                array[index].direction = Random.onUnitSphere;
                array[index].currentSpeed = 5f;
                array[index].roll = 0f;
                array[index].flapPhase = Random.Range(0f, 100f);

                // Juice Parameters
                array[index].flapAmplitude = 1f;
                array[index].size = Random.Range(0.7f, 1.3f);

                array[index].schoolID = schoolID;
                index++;
            }
        }

        private GraphicsBuffer CreateArgsBuffer(Mesh mesh, int count)
        {
            uint[] args = new uint[5] { mesh.GetIndexCount(0), (uint)count, 0, 0, 0 };
            GraphicsBuffer buffer = new(GraphicsBuffer.Target.IndirectArguments, 1, args.Length * sizeof(uint));
            buffer.SetData(args);
            return buffer;
        }
        
        private void OnDrawGizmos()
        {
            if (ecosystemCenter is not null)
            {
                Gizmos.color = new Color(0.0f, 1.0f, 1.0f, 0.3f); // Semi-transparent Cyan
                Gizmos.DrawWireSphere(ecosystemCenter.position, ecosystemRadius);
            }
        }

        // --- EDITOR HELPER ---
        [ContextMenu("Setup Default Ecosystem")]
        private void SetupDefaultEcosystem()
        {
            speciesSettings.Clear();
            schoolSettings.Clear();

            // Setup Species
            speciesSettings.Add(new SpeciesSetup
            {
                name = "Yellow Fish",
                profile = new SpeciesProfile
                {
                    speed = 6f, sightRadius = 3f, separationWeight = 1.5f, alignmentWeight = 1.2f,
                    cohesionWeight = 1.5f, predatorSpecies = 3, preySpecies = -1, sizeMultiplier = 1f
                }
            });
            speciesSettings.Add(new SpeciesSetup
            {
                name = "Koi",
                profile = new SpeciesProfile
                {
                    speed = 4f, sightRadius = 4f, separationWeight = 1.2f, alignmentWeight = 1.0f,
                    cohesionWeight = 1.2f, predatorSpecies = 3, preySpecies = -1, sizeMultiplier = 3f
                }
            });
            speciesSettings.Add(new SpeciesSetup
            {
                name = "Ray",
                profile = new SpeciesProfile
                {
                    speed = 3f, sightRadius = 6f, separationWeight = 2.0f, alignmentWeight = 0.5f,
                    cohesionWeight = 0.5f, predatorSpecies = -1, preySpecies = -1, sizeMultiplier = 10f
                }
            });
            speciesSettings.Add(new SpeciesSetup
            {
                name = "Orca",
                profile = new SpeciesProfile
                {
                    speed = 8f, sightRadius = 15f, separationWeight = 1.5f, alignmentWeight = 1.0f,
                    cohesionWeight = 1.0f, predatorSpecies = -1, preySpecies = 1, sizeMultiplier = 20f
                }
            });

            // Setup Schools
            schoolSettings.Add(new SchoolSetup
            {
                name = "Yellow School A", speciesIndex = 0, boidCount = 150,
            });
            schoolSettings.Add(new SchoolSetup
            {
                name = "Yellow School B", speciesIndex = 0, boidCount = 150,
            });
            schoolSettings.Add(new SchoolSetup
            {
                name = "Yellow School C", speciesIndex = 0, boidCount = 150
            });
            schoolSettings.Add(new SchoolSetup
            {
                name = "Koi Pond", speciesIndex = 1, boidCount = 10
            });
            schoolSettings.Add(new SchoolSetup
            {
                name = "Ray Group", speciesIndex = 2, boidCount = 15
            });
            schoolSettings.Add(new SchoolSetup
            {
                name = "Orca 1", speciesIndex = 3, boidCount = 1
            });
            schoolSettings.Add(new SchoolSetup
            {
                name = "Orca 2", speciesIndex = 3, boidCount = 1
            });

            Debug.Log(
                "Default Ecosystem Populated! Remember to assign your Meshes and Materials to the Species Settings.");
        }

        // --- CONFIGURATION CLASSES ---
        [Serializable]
        public class SpeciesSetup
        {
            public string name = "New Species";
            public Mesh mesh;
            public Material material;

            [Header("Behavior Rules")] public SpeciesProfile profile;

            // Internal tracking for rendering
            [HideInInspector] public int offset;
            [HideInInspector] public int count;
            public GraphicsBuffer argsBuffer;
        }

        [Serializable]
        public class SchoolSetup
        {
            public string name = "New School";

            [Tooltip("The index of the Species in the Species Settings list (e.g., 0 for Yellow Fish)")]
            public int speciesIndex;

            public int boidCount = 100;

        }

        // --- DATA STRUCTURES (Must match HLSL exactly) ---
        [StructLayout(LayoutKind.Sequential)]
        public struct EcosystemBoid
        {
            public Vector3 position;
            public Vector3 direction;
            public float currentSpeed;
            public float roll;
            public float flapPhase;
            public float flapAmplitude; // <-- Ensure this is here and public!
            public float size; // <-- Ensure this is here and public!
            public uint schoolID;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct SpeciesProfile
        {
            public float speed;
            public float sightRadius;
            public float separationWeight;
            public float alignmentWeight;
            public float cohesionWeight;
            public int predatorSpecies;
            public int preySpecies;
            public float sizeMultiplier;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SchoolProfile
        {

            public uint speciesID;
        }
    }
}