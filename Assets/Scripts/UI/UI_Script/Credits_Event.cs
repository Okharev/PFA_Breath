using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class Credits_Event : MonoBehaviour
{
    private Button ButtonBack;
    private UIDocument UI_Credits;
    private AudioManager audioManager;

    private void Awake()
    {
        UI_Credits = GetComponent<UIDocument>();
        //Start button to load Main_Scene
        ButtonBack = UI_Credits.rootVisualElement.Q("B_Back") as Button;
        ButtonBack.RegisterCallback<ClickEvent>(BackOnMenu);
    }
    private void OnDisable()
    {
        ButtonBack.UnregisterCallback<ClickEvent>(BackOnMenu);
    }
    private void BackOnMenu(ClickEvent evt)
    {
        SceneManager.LoadScene("00_StartMainMenu");
    }
}
