using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public abstract class BaseTurnAI : MonoBehaviour, ITurnEntity
{
    protected NavMeshAgent agent;
    protected Rigidbody rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();

        // Standardize physics for turn-based syncing
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Decouple agent from transform so we can manually Lerp/MovePosition
        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    protected virtual void Start()
    {
        TurnManager.Instance.RegisterEntity(this);
    }

    protected virtual void Update()
    {
        agent.nextPosition = rb.position;
    }

    protected virtual void OnDestroy()
    {
        if (TurnManager.Instance) TurnManager.Instance.UnregisterEntity(this);
    }

    // --- ITURNENTITY TEMPLATE METHOD ---

    public void PlanAction()
    {
        OnPlanAction();
    }

    public void ExecuteAction()
    {
        OnExecuteAction();
    }

    public void EndTurn()
    {
        OnEndTurn();
    }

    // --- ABSTRACT HOOKS (Children MUST implement these) ---

    protected abstract void OnPlanAction();
    protected abstract void OnExecuteAction();
    protected abstract void OnEndTurn();
}