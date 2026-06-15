using Ability.NewAbilitySystem;
using Dialogues.UI;
using Skills.UI;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerEndDemo : MonoBehaviour, IInteractable
{
    public static TriggerEndDemo Instance { get; private set; }

    [SerializeField] private EndDemo_Event enddemo;
    //[SerializeField] private DialogueDebugDirectTrigger dialogueTrigger;
    [SerializeField] private DoorObjectives doorObjectives;

    public bool isEntered = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        //dialogueTrigger.onCleared += activateEndDemoTrigger;
        doorObjectives.triggerEndLevel += activateEndDemoTrigger;
        // Deactivate the end level at the start of the level
        gameObject.SetActive(false);
    }

    public void activateEndDemoTrigger()
    {
        gameObject.SetActive(true);
    }

    public void EndDemo()
    {
        if (enddemo == null) 
        {     
            Debug.Log("UI End Demo missing");
            return;
        }
        enddemo.gameObject.SetActive(true);
    }


    public void Interact(GameObject instigator)
    {
        // Interactable when close enough
        StartCoroutine(PlaySound());
    }

    private IEnumerator PlaySound()
    {
        AudioSource s = GetComponent<AudioSource>();
        s.Play();


        yield return new WaitForSeconds(s.clip.length);
        EndDemo();

    }

}
