using UnityEngine;

public class BoidsManager : MonoBehaviour
{
    private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
    private static readonly int Time1 = Shader.PropertyToID("time");
    private static readonly int Speed = Shader.PropertyToID("speed");
    private static readonly int NumBoids = Shader.PropertyToID("numBoids");
    private static readonly int SightRadius = Shader.PropertyToID("sightRadius");
    private static readonly int SeparationWeight = Shader.PropertyToID("separationWeight");
    private static readonly int TargetPosition = Shader.PropertyToID("targetPosition");
    private static readonly int TargetWeight = Shader.PropertyToID("targetWeight");
    private static readonly int NumObstacles = Shader.PropertyToID("numObstacles");
    private static readonly int ObstaclesBuffer = Shader.PropertyToID("obstaclesBuffer");
    private static readonly int PredatorPosition = Shader.PropertyToID("predatorPosition");
    private static readonly int PredatorRadius = Shader.PropertyToID("predatorRadius");
    private static readonly int BoidsBuffer = Shader.PropertyToID("boidsBuffer");
    private static readonly int FloorY = Shader.PropertyToID("floorY");
    private static readonly int AvoidanceMargin = Shader.PropertyToID("avoidanceMargin");

    struct Boid
    {
        public Vector3 position;
        public Vector3 direction;
        public Vector3 color;
        public float size;
        public float currentSpeed;
    }

    struct Obstacle
    {
        public Vector3 position;
        public float radius;
    }

    [Header("Références")]
    public ComputeShader boidsCompute;
    public Mesh fishMesh;
    public Material fishMaterial;
    public Transform playerTransform;

    [Header("Paramètres du banc")]
    public int numBoids = 1000;
    public float spawnRadius = 20f;
    public float speed = 5f;
    public float sightRadius = 3f;
    public float separationWeight = 1.5f;
    public float targetWeight = 0.5f;

    // --- NOUVEAU : Paramètres Organiques ---
    [Header("Variations Organiques")]
    [Tooltip("Vitesse à laquelle le banc se dilate et se contracte")]
    public float separationPulseSpeed = 1.0f; 
    [Tooltip("Force de la variation (+ ou - par rapport à la séparation de base)")]
    public float separationPulseAmount = 0.8f; 

    [Header("Paramètres Visuels")]
    public Color colorA = Color.white;
    public Color colorB = Color.blue;
    public float minSize = 0.5f;
    public float maxSize = 1.5f;
    public float predatorRadius = 5f;
    [Header("Limites du Monde")]
    [Tooltip("La hauteur Y de ton sol")]
    public float floorY = 0f; 
    [Tooltip("Distance à laquelle les poissons commencent à remonter")]
    public float floorAvoidanceMargin = 1.5f;

    [Header("Conscience du Décor")]
    public LayerMask obstacleLayer;
    public int maxObstacles = 50;
    public float scanRadius = 30f;

    private ComputeBuffer boidsBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer obstaclesBuffer; 
    private Obstacle[] obstaclesArray;
    private Collider[] buffer = new Collider[32];
    
    private int kernelID;

    void Start()
    {
        kernelID = boidsCompute.FindKernel("CSMain");

        Boid[] boidsArray = new Boid[numBoids];
        for (int i = 0; i < numBoids; i++)
        {
            boidsArray[i].position = transform.position + Random.insideUnitSphere * spawnRadius;
            boidsArray[i].direction = Random.onUnitSphere;
            Color randomColor = Color.Lerp(colorA, colorB, Random.value);
            boidsArray[i].color = new Vector3(randomColor.r, randomColor.g, randomColor.b);
            boidsArray[i].size = Random.Range(minSize, maxSize);
            boidsArray[i].currentSpeed = speed;
        }

        boidsBuffer = new ComputeBuffer(numBoids, 44);
        boidsBuffer.SetData(boidsArray);

        obstaclesArray = new Obstacle[maxObstacles];
        obstaclesBuffer = new ComputeBuffer(maxObstacles, 16); 

        boidsCompute.SetBuffer(kernelID, BoidsBuffer, boidsBuffer);
        fishMaterial.SetBuffer(BoidsBuffer, boidsBuffer);

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)fishMesh.GetIndexCount(0);
        args[1] = (uint)numBoids;
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    void Update()
    {
        var size = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, buffer, obstacleLayer);
        int obstacleCount = Mathf.Min(size, maxObstacles);

        for (int i = 0; i < obstacleCount; i++)
        {
            obstaclesArray[i].position = buffer[i].bounds.center;
            obstaclesArray[i].radius = buffer[i].bounds.extents.magnitude; 
        }

        obstaclesBuffer.SetData(obstaclesArray);
        boidsCompute.SetBuffer(kernelID, ObstaclesBuffer, obstaclesBuffer);
        boidsCompute.SetInt(NumObstacles, obstacleCount);

        // --- NOUVEAU : Séparation Dynamique ---
        // On calcule une onde douce basée sur le temps
        float pulse = Mathf.Sin(Time.time * separationPulseSpeed) * separationPulseAmount;
        // On empêche la séparation de devenir négative (les poissons s'attireraient !)
        float dynamicSeparation = Mathf.Max(0.1f, separationWeight + pulse);

        boidsCompute.SetFloat(DeltaTime, Time.deltaTime);
        boidsCompute.SetFloat(Time1, Time.time);
        boidsCompute.SetFloat(Speed, speed);
        boidsCompute.SetInt(NumBoids, numBoids);
        boidsCompute.SetFloat(SightRadius, sightRadius);
        boidsCompute.SetFloat(FloorY, floorY);
        boidsCompute.SetFloat(AvoidanceMargin, floorAvoidanceMargin);
        
        // On envoie notre nouvelle séparation animée à la carte graphique
        boidsCompute.SetFloat(SeparationWeight, dynamicSeparation);
        
        boidsCompute.SetVector(TargetPosition, transform.position);
        boidsCompute.SetFloat(TargetWeight, targetWeight);

        if (playerTransform is not null)
        {
            boidsCompute.SetVector(PredatorPosition, playerTransform.position);
            boidsCompute.SetFloat(PredatorRadius, predatorRadius);
        }

        int threadGroups = Mathf.CeilToInt(numBoids / 64f);
        boidsCompute.Dispatch(kernelID, threadGroups, 1, 1);

        Graphics.DrawMeshInstancedIndirect(fishMesh, 0, fishMaterial, new Bounds(transform.position, Vector3.one * 1000f), argsBuffer);
    }

    void OnDestroy()
    {
        if (boidsBuffer != null) boidsBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        if (obstaclesBuffer != null) obstaclesBuffer.Release();
    }
}