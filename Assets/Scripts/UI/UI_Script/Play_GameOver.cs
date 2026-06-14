using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject uiDocument; // Ton UI à afficher
    public HealthComponent playerHealth;

    private bool isGameOver = false;

    void Start()
    {
        uiDocument.SetActive(false);

       // playerHealth = GameObject.FindGameObjectWithTag("Player")
                         //.GetComponent<HealthComponent>();
    }

    void Update()
    {
        if (!isGameOver && playerHealth.CurrentHealth <= 0)
        {
            Time.timeScale = 0f;
            isGameOver = true;
            uiDocument.SetActive(true);
        }
    }
}