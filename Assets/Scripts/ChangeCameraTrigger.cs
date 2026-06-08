using UnityEngine;

public class ChangeCameraTrigger : MonoBehaviour
{
    [SerializeField] public Camera mainCamera;
    public float distanceCamera = 15f;
    public float pitchCamera = 45f;
    public float yawCamera = 40f;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            mainCamera.GetComponent<IsometricCameraFollow>().distance = distanceCamera;
            mainCamera.GetComponent<IsometricCameraFollow>().pitch = pitchCamera;
            mainCamera.GetComponent<IsometricCameraFollow>().yaw = yawCamera;
        }
    }
}