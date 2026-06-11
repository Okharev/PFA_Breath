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
        if (_animator == null) return;
        
    if (_agent.remainingDistance >= remainingDistance)
        {
            Debug.Log("il y a encore de la marche à faire");
            _animator.SetBool("isWalking", true);
        }
        else
        {
            _animator.SetBool("isWalking", false);
        }
}
}
