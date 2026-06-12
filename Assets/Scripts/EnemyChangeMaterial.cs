using Ability.NewAbilitySystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class EnemyChangeMaterial : MonoBehaviour
{
    public Material _material;
    private EnemyAIController aiController;
    private NavMeshAgent navMeshAgent;
    private HealthComponent healthComponent;
    private EnemyHealthPresenter presenter;
    public MeshRenderer renderer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        aiController = GetComponent<EnemyAIController>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        presenter = GetComponent<EnemyHealthPresenter>();
        healthComponent = GetComponent<HealthComponent>();
        renderer = GetComponentInChildren<MeshRenderer>();

        healthComponent.OnDeath += ChangeEnemyMat;

    }

    private void ChangeEnemyMat(GameObject gameObject)
    {


        renderer.material = _material;

        Destroy(aiController);
        Destroy(navMeshAgent);
        Destroy(presenter);
    }

}
