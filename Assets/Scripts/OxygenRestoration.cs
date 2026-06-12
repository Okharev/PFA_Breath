using System.Collections;
using System.Timers;
using UnityEngine;

public class OxygenRestoration : MonoBehaviour
{
    public float bonus;
    public float speed = 0.25f;

    [SerializeField] private GameObject bubble;


    public IEnumerator DestroyBubble()
    {
           float elapsedTime = 0f;

        while (elapsedTime < speed)
        {
            bubble.transform.localScale -= new Vector3(elapsedTime, elapsedTime, elapsedTime) ;

            yield return null;
            elapsedTime += Time.unscaledDeltaTime;
        }

        Destroy(bubble);

    }
    

}
