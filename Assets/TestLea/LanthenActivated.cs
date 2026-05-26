using UnityEngine;

public class LanthenActivated : MonoBehaviour
{
    public string tagPlatform;
    [SerializeField] private SphereCollider _triggerZone;
    public bool _isActivated;

    private void Awake()
    {
        _triggerZone = GetComponent<SphereCollider>();
        Debug.Log("On active la lantern");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (tagPlatform == null) return;

        if (other.gameObject.CompareTag(tagPlatform))
        {
            other.gameObject.GetComponent<MeshRenderer>().enabled = true;
        }
               
    }

    private void OnTriggerExit(Collider other)
    {
        if (tagPlatform == null) return;

        if (other.gameObject.CompareTag(tagPlatform))
        {
            other.gameObject.GetComponent<MeshRenderer>().enabled = false;
        }
    }
}
