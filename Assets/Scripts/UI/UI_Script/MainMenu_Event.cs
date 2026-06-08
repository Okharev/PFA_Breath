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
    }


    private void OnDisable()
    {
        ButtonStart.UnregisterCallback<ClickEvent>(OnPlayGameClick);
        ButtonZoo.UnregisterCallback<ClickEvent>(OnPlayZooClick);
        ButtonCredit.UnregisterCallback<ClickEvent>(OnPlayCreditClick);
    }

    private void OnPlayGameClick(ClickEvent evt)
    {
        Debug.Log("Bienvenue a XAR SAROTH !!!");
        SceneManager.LoadScene("BlockoutF GA");
    }

    private void OnPlayZooClick(ClickEvent evt)
    {
        Debug.Log(" LES ZOO ZOO");
        SceneManager.LoadScene("Scene_Zoo");
    }

    private void OnPlayCreditClick(ClickEvent evt)
    {
        Debug.Log("The End M*therfucker");
        SceneManager.LoadScene("04_CreditMenu");
    }

    private void QuitClick(ClickEvent evt)
    {
        Debug.Log("Tmort Tmort AAAAAAH");
        Application.Quit();
    }
}