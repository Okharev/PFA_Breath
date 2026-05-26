using System;
using UnityEngine;

public class LanthernPlatformManager : MonoBehaviour
{
    public Collider inactiveCollider;
    
    void Start()
    {
        //make the platform invisible
        GetComponent<MeshRenderer>().enabled = false;
       //inactiveCollider.gameObject.SetActive(true);
       //inactiveCollider.gameObject.GetComponent<MeshRenderer>().enabled = false;
    }

}
