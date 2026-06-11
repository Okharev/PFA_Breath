using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameOver_Event : MonoBehaviour
{
    private Button ButtonRetart;
    private Button ButtonMainMenu; 
    private Button ButtonCredit; 
    private Button ButtonQuit;
    private UIDocument UI_GameOver;
    private AudioManager audioManager;

    private void Awake()
    {
        UI_GameOver = GetComponent<UIDocument>();
        //Start button to load Main_Scene
        ButtonRetart = UI_GameOver.rootVisualElement.Q("B_TryAgain") as Button;
        ButtonRetart.RegisterCallback<ClickEvent>(OnTryAgain);

        ButtonMainMenu = UI_GameOver.rootVisualElement.Q("B_MainMenu") as Button;
        ButtonRetart.RegisterCallback<ClickEvent>(BackOnMenu);

        ButtonCredit = UI_GameOver.rootVisualElement.Q("B_Credit") as Button;
        ButtonRetart.RegisterCallback<ClickEvent>(OnCreditClick);

        ButtonQuit = UI_GameOver.rootVisualElement.Q("B_Quit") as Button;
        ButtonRetart.RegisterCallback<ClickEvent>(OnQuitGame);
    }   
    private void OnDisable()
    {
        ButtonRetart.UnregisterCallback<ClickEvent>(OnTryAgain);
        ButtonMainMenu.UnregisterCallback<ClickEvent>(BackOnMenu);
        ButtonCredit.UnregisterCallback<ClickEvent>(OnCreditClick);
        ButtonQuit.UnregisterCallback<ClickEvent>(OnQuitGame);
    }
 
    private void OnTryAgain(ClickEvent evt)
    {
        SceneManager.LoadScene("01_Level");
    }
    private void BackOnMenu(ClickEvent evt)
    {
        SceneManager.LoadScene("00_StartMainMenu");
    }
    private void OnCreditClick(ClickEvent evt)
    {
    SceneManager.LoadScene("04_CreditMenu");
    }
   private void OnQuitGame(ClickEvent evt)
    {
        Application.Quit();
    }

}




