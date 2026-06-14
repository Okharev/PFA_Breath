using UnityEngine;
using UnityEngine.AI;

public class PlayerAnimation : MonoBehaviour
{
    private Animator _animator;
    private NavMeshAgent _agent;

    public float remainingDistance = 0.05f;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (_animator == null || _agent == null) return;

        // CRITICAL FIX: Ensure the agent is bound to the mesh before querying distance
        if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh) 
        {
            _animator.SetBool("isWalking", false);
            return;
        }

        if (_agent.remainingDistance >= remainingDistance)
        {
            _animator.SetBool("isWalking", true);
        }
        else
        {
            _animator.SetBool("isWalking", false);
        }
    }
}