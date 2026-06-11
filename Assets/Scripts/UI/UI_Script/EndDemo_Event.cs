using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
public class EndDemo_Event : MonoBehaviour
{
    private Button ButtonBacktoMenu;
    private Button ButtonCredit;
    private Button ButtonQuit;
    private UIDocument UI_EndDemo;

    private void Awake()
    {
        UI_EndDemo = GetComponent<UIDocument>();
        //Button to Back to Menu
        ButtonBacktoMenu = UI_EndDemo.rootVisualElement.Q("B_MainMenu") as Button;
        ButtonBacktoMenu.RegisterCallback<ClickEvent>(OnBackClick);
        //Button to credit of the team membres
        ButtonCredit = UI_EndDemo.rootVisualElement.Q("B_Credit") as Button;
        ButtonCredit.RegisterCallback<ClickEvent>(OnPlayCreditClick);
        //Button to quit game
        ButtonQuit = UI_EndDemo.rootVisualElement.Q("B_Quit") as Button;
        ButtonQuit.RegisterCallback<ClickEvent>(QuitClick);
    }
    private void OnDisable()
    {
        ButtonBacktoMenu.UnregisterCallback<ClickEvent>(OnBackClick);
        ButtonCredit.UnregisterCallback<ClickEvent>(OnPlayCreditClick);
    }

    private void OnBackClick(ClickEvent evt)
    {
        SceneManager.LoadScene("00_StartMainMenu");
    }
    private void OnPlayCreditClick(ClickEvent evt)
    {
        SceneManager.LoadScene("00_StartMainMenu");
    }
    private void QuitClick(ClickEvent evt)
    {
        Application.Quit();
    }
}
