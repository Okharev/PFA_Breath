using UnityEngine;

namespace TechArtPlayground
{
    public class BoidsManager : MonoBehaviour
    {
        private static readonly int CellSize = Shader.PropertyToID("cellSize");
        private static readonly int GridSize = Shader.PropertyToID("gridSize");
        private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int Time1 = Shader.PropertyToID("time");
        private static readonly int Speed = Shader.PropertyToID("speed");
        private static readonly int NumBoids = Shader.PropertyToID("numBoids");
        private static readonly int SightRadius = Shader.PropertyToID("sightRadius");
        private static readonly int SeparationWeight = Shader.PropertyToID("separationWeight");
        private static readonly int TargetPosition = Shader.PropertyToID("targetPosition");
        private static readonly int TargetWeight = Shader.PropertyToID("targetWeight");
        private static readonly int FloorY = Shader.PropertyToID("floorY");
        private static readonly int AvoidanceMargin = Shader.PropertyToID("avoidanceMargin");
        private static readonly int NumObstacles = Shader.PropertyToID("numObstacles");
        private static readonly int PredatorPosition = Shader.PropertyToID("predatorPosition");
        private static readonly int PredatorRadius = Shader.PropertyToID("predatorRadius");

        struct Boid { public Vector3 position; public Vector3 direction; public Vector3 color; public float size; public float currentSpeed; public float roll; }
        struct Obstacle { public Vector3 position; public float radius; }

        [Header("Références")]
        public ComputeShader boidsCompute;
        public Mesh fishMesh;
        public Material fishMaterial;
        public Transform playerTransform;

        [Header("Paramètres du banc")]
        public int numBoids = 10000; // ON AUGMENTE MASSIVEMENT LE NOMBRE !
        public float spawnRadius = 40f;
        public float speed = 5f;
        public float sightRadius = 3f;
        public float separationWeight = 1.5f;
        public float targetWeight = 0.5f;

        [Header("Paramètres Spatiaux (Optimisation GPU)")]
        [Tooltip("La taille de la zone d'une cellule de la grille. Idéalement égale à ton Sight Radius.")]
        public float cellSize = 3f;
        [Tooltip("La taille 3D de ta grille (ex: 64 = grille de 64x64x64)")]
        public int gridSize = 64; 

        [Header("Variations Organiques")]
        public float separationPulseSpeed = 1.0f; 
        public float separationPulseAmount = 0.8f; 

        [Header("Paramètres Visuels")]
        public Color colorA = Color.white;
        public Color colorB = Color.blue;
        public float minSize = 0.5f;
        public float maxSize = 1.5f;
        public float predatorRadius = 5f;

        [Header("Limites du Monde & Décor")]
        public float floorY = 0f; 
        public float floorAvoidanceMargin = 2f; 
        public LayerMask obstacleLayer;
        public int maxObstacles = 50;
        public float scanRadius = 50f;

        private ComputeBuffer boidsBuffer;
        private ComputeBuffer argsBuffer;
        private ComputeBuffer obstaclesBuffer; 
    
        // NOUVEAUX BUFFERS POUR LA GRILLE
        private ComputeBuffer gridOffsetsBuffer;
        private ComputeBuffer boidOffsetsBuffer;

        private Obstacle[] obstaclesArray;
        private Collider[] collBuffer = new Collider[32];
    
        // IDs des 3 Kernels
        private int clearGridKernel, populateGridKernel, csMainKernel;

        void Start()
        {
            clearGridKernel = boidsCompute.FindKernel("ClearGrid");
            populateGridKernel = boidsCompute.FindKernel("PopulateGrid");
            csMainKernel = boidsCompute.FindKernel("CSMain");

            // 1. Initialisation des poissons
            Boid[] boidsArray = new Boid[numBoids];
            for (int i = 0; i < numBoids; i++)
            {
                boidsArray[i].position = transform.position + Random.insideUnitSphere * spawnRadius;
                boidsArray[i].direction = Random.onUnitSphere;
                Color randomColor = Color.Lerp(colorA, colorB, Random.value);
                boidsArray[i].color = new Vector3(randomColor.r, randomColor.g, randomColor.b);
                boidsArray[i].size = Random.Range(minSize, maxSize);
                boidsArray[i].currentSpeed = speed;
                boidsArray[i].roll = 0f;
            }

            boidsBuffer = new ComputeBuffer(numBoids, 48);
            boidsBuffer.SetData(boidsArray);

            // 2. Initialisation des buffers de Grille Spatiale
            int totalCells = gridSize * gridSize * gridSize;
            gridOffsetsBuffer = new ComputeBuffer(totalCells, sizeof(int));
            boidOffsetsBuffer = new ComputeBuffer(numBoids, sizeof(int));

            // 3. Buffer d'obstacles
            obstaclesArray = new Obstacle[maxObstacles];
            obstaclesBuffer = new ComputeBuffer(maxObstacles, 16); 

            // 4. Assignation des buffers aux bons Kernels
            boidsCompute.SetBuffer(clearGridKernel, "gridOffsets", gridOffsetsBuffer);
        
            boidsCompute.SetBuffer(populateGridKernel, "boidsBuffer", boidsBuffer);
            boidsCompute.SetBuffer(populateGridKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(populateGridKernel, "boidOffsets", boidOffsetsBuffer);

            boidsCompute.SetBuffer(csMainKernel, "boidsBuffer", boidsBuffer);
            boidsCompute.SetBuffer(csMainKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(csMainKernel, "boidOffsets", boidOffsetsBuffer);
            boidsCompute.SetBuffer(csMainKernel, "obstaclesBuffer", obstaclesBuffer);

            fishMaterial.SetBuffer("boidsBuffer", boidsBuffer);

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = (uint)fishMesh.GetIndexCount(0);
            args[1] = (uint)numBoids;
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
        }

        void Update()
        {
            var size = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, collBuffer, obstacleLayer);
            int obstacleCount = Mathf.Min(size, maxObstacles);

            for (int i = 0; i < obstacleCount; i++)
            {
                obstaclesArray[i].position = collBuffer[i].bounds.center;
                obstaclesArray[i].radius = collBuffer[i].bounds.extents.magnitude; 
            }

            obstaclesBuffer.SetData(obstaclesArray);
            boidsCompute.SetInt(NumObstacles, obstacleCount);

            float pulse = Mathf.Sin(Time.time * separationPulseSpeed) * separationPulseAmount;
            float dynamicSeparation = Mathf.Max(0.1f, separationWeight + pulse);

            // Envoi des variables globales (Valables pour tous les Kernels)
            boidsCompute.SetFloat(CellSize, cellSize);
            boidsCompute.SetInt(GridSize, gridSize);
            boidsCompute.SetFloat(DeltaTime, Time.deltaTime);
            boidsCompute.SetFloat(Time1, Time.time);
            boidsCompute.SetFloat(Speed, speed);
            boidsCompute.SetInt(NumBoids, numBoids);
            boidsCompute.SetFloat(SightRadius, sightRadius);
            boidsCompute.SetFloat(SeparationWeight, dynamicSeparation);
            boidsCompute.SetVector(TargetPosition, transform.position);
            boidsCompute.SetFloat(TargetWeight, targetWeight);
            boidsCompute.SetFloat(FloorY, floorY);
            boidsCompute.SetFloat(AvoidanceMargin, floorAvoidanceMargin);

            if (playerTransform is not null)
            {
                boidsCompute.SetVector(PredatorPosition, playerTransform.position);
                boidsCompute.SetFloat(PredatorRadius, predatorRadius);
            }

            // --- EXECUTION EN 3 ETAPES DE LA CARTE GRAPHIQUE ---
            int totalCells = gridSize * gridSize * gridSize;
            int gridThreadGroups = Mathf.CeilToInt(totalCells / 64f);
            int boidThreadGroups = Mathf.CeilToInt(numBoids / 64f);

            // Étape 1 : On vide la grille
            boidsCompute.Dispatch(clearGridKernel, gridThreadGroups, 1, 1);
        
            // Étape 2 : On range les poissons
            boidsCompute.Dispatch(populateGridKernel, boidThreadGroups, 1, 1);
        
            // Étape 3 : On calcule le mouvement !
            boidsCompute.Dispatch(csMainKernel, boidThreadGroups, 1, 1);
            // ---------------------------------------------------

            Graphics.DrawMeshInstancedIndirect(fishMesh, 0, fishMaterial, new Bounds(transform.position, Vector3.one * 1000f), argsBuffer);
        }

        void OnDestroy()
        {
            if (boidsBuffer != null) boidsBuffer.Release();
            if (argsBuffer != null) argsBuffer.Release();
            if (obstaclesBuffer != null) obstaclesBuffer.Release();
            if (gridOffsetsBuffer != null) gridOffsetsBuffer.Release();
            if (boidOffsetsBuffer != null) boidOffsetsBuffer.Release();
        }
    }
}