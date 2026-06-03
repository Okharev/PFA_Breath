using UnityEngine;

public class ChangeCameraTrigger : MonoBehaviour
{
    [SerializeField] public Camera mainCamera;
    public float distanceCamera;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        {
            mainCamera.GetComponent<IsometricCameraFollow>().distance = distanceCamera;

        }
    }
}
