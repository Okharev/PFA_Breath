using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

public class SkinnedMeshtoMesh : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMesh;
    public VisualEffect VFXGraph;
    public float refreshRate;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine (UpdateVFXGraph());
    }
    IEnumerator UpdateVFXGraph()
    {
        while(gameObject.activeSelf)
        {
            Mesh m = new Mesh();
            skinnedMesh.BakeMesh(m);
            Vector3[] vertices = m.vertices;
            Mesh m2 = new Mesh();
            m2.vertices = vertices;
            VFXGraph.SetMesh("Mesh", m2);

            yield return new WaitForSeconds(refreshRate);
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
