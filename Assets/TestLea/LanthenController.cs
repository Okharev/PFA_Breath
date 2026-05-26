using UnityEngine;
using UnityEngine.InputSystem;

public class LanthenController : MonoBehaviour
{
    public bool isCarrying;
    public GameObject lanthern;
    private GameObject lanthernIG;


    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            if (!isCarrying)
            {
                isCarrying = true;
                lanthernIG = Instantiate(lanthern, this.transform);

                Vector3 posLanthern = this.transform.position + new Vector3(0.25f, 1.27f, -0.34f);
                lanthernIG.transform.position = posLanthern; 
            }
            else
            {
                isCarrying = false;
                Destroy(lanthernIG);
            }

        }
    
    
    
    }

}
