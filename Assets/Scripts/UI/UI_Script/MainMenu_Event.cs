using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenu_Event : MonoBehaviour
{
    private Button ButtonCredit;
    private Button ButtonQuit;
    private Button ButtonStart;
    private Button ButtonZoo;
    private UIDocument UIMain_Menu;
    private AudioManager audioManager;

    public string buttonSound;

    private void Awake()
    {
        UIMain_Menu = GetComponent<UIDocument>();
        //Start button to load Main_Scene
        ButtonStart = UIMain_Menu.rootVisualElement.Q("B_Start") as Button;
        ButtonStart.RegisterCallback<ClickEvent>(OnPlayGameClick);
        //Button to Zoo Scene
        ButtonZoo = UIMain_Menu.rootVisualElement.Q("B_Zoo") as Button;
        ButtonZoo.RegisterCallback<ClickEvent>(OnPlayZooClick);
        //Button to credit of the team membres
        ButtonCredit = UIMain_Menu.rootVisualElement.Q("B_Credit") as Button;
        ButtonCredit.RegisterCallback<ClickEvent>(OnPlayCreditClick);
        //Button to quit game
        ButtonQuit = UIMain_Menu.rootVisualElement.Q("B_Quit") as Button;
        ButtonQuit.RegisterCallback<ClickEvent>(QuitClick);

        audioManager = AudioManager.instance;
    }


    private void OnDisable()
    {
        ButtonStart.UnregisterCallback<ClickEvent>(OnPlayGameClick);
        ButtonZoo.UnregisterCallback<ClickEvent>(OnPlayZooClick);
        ButtonCredit.UnregisterCallback<ClickEvent>(OnPlayCreditClick);
    }

    

    private void OnPlayGameClick(ClickEvent evt)
    {
        StartCoroutine(PlayGameAndWait("01_Level"));
        //Debug.Log("Bienvenue a XAR SAROTH !!!");
        //audioManager.Play("clic");
        //SceneManager.LoadScene("01_Level");
    }

    private void OnPlayZooClick(ClickEvent evt)
    {
        //Debug.Log(" LES ZOO ZOO");
        //audioManager.Play("clic");
        //SceneManager.LoadScene("Scene_Zoo");
        StartCoroutine(PlayGameAndWait("Scene_Zoo"));
    }

    private void OnPlayCreditClick(ClickEvent evt)
    {
        //Debug.Log("The End M*therfucker");
        //audioManager.Play("clic");
        //SceneManager.LoadScene("04_CreditMenu");
        StartCoroutine(PlayGameAndWait("04_CreditMenu"));
    }

    private void QuitClick(ClickEvent evt)
    {
        //Debug.Log("Tmort Tmort AAAAAAH");
        //audioManager.Play("clic");
        //Application.Quit();
        StartCoroutine(QuitGameAndWait());
    }

    private IEnumerator PlayGameAndWait(string scene)
    {
        Sound s = audioManager.GetSound(buttonSound);
        audioManager.Play(buttonSound);


        yield return new WaitForSeconds(s.clip.length);
        SceneManager.LoadScene(scene);
    }

    private IEnumerator QuitGameAndWait()
    {
        Sound s = audioManager.GetSound(buttonSound);
        audioManager.Play(buttonSound);


        yield return new WaitForSeconds(s.clip.length);
        Application.Quit();
    }

}