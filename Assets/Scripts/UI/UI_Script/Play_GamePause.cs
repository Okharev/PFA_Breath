using UnityEngine;
using UnityEngine.UIElements;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private UIDocument pauseMenu;

    private VisualElement root;
    private Button resumeButton;

    private bool isPaused = false;

    private void Start()
    {
        root = pauseMenu.rootVisualElement;

        // Récupère le bouton "Reprendre"
        resumeButton = root.Q<Button>("ResumeButton");
        resumeButton.clicked += ResumeGame;

        // Cache le menu au démarrage
        root.style.display = DisplayStyle.None;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            Debug.Log("Mettez pause Batard");
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    private void PauseGame()
    {
        root.style.display = DisplayStyle.Flex;
        Time.timeScale = 0f;
        isPaused = true;
    }

    private void ResumeGame()
    {
        root.style.display = DisplayStyle.None;
        Time.timeScale = 1f;
        isPaused = false;
    }
}