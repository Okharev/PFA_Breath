using UnityEngine;

// Use 'using UnityEngine.UI;' if using standard Legacy Text

/// <summary>
///     Displays real-time information about the game's time manipulation.
/// </summary>
public class TimeDebugUI : MonoBehaviour
{
    // Définir la taille et la position de notre fenêtre de débogage
    private readonly Rect debugWindowRect = new(10, 10, 250, 140);

    private void OnGUI()
    {
        // Sécurité : on s'assure que le TimeTickManager existe bien
        if (TimeTickManager.Instance is null) return;

        // Récupérer les données du Manager
        string phase = TimeTickManager.Instance.CurrentPhase;
        float scale = TimeTickManager.Instance.CurrentTimeScale;
        float progress = TimeTickManager.Instance.ActionProgress * 100f;
        float totalDuration = TimeTickManager.Instance.TotalActionDuration;
        bool isFlowing = TimeTickManager.Instance.IsTimeFlowing();

        // Dessiner une boîte de fond (Background Box)
        GUI.Box(debugWindowRect, "Chronosphere Debug");

        // Créer un style pour le texte pour qu'il soit bien lisible
        GUIStyle textStyle = new(GUI.skin.label);
        textStyle.fontSize = 14;
        textStyle.normal.textColor = Color.white;

        // Préparer le texte à afficher
        string debugText =
            $"Time Flowing: {isFlowing}\n" +
            $"Current Phase: {phase}\n" +
            $"Time Scale: {scale.ToString("F2")}x\n" +
            $"Action Target Time: {totalDuration}s\n" +
            $"Action Progress: {progress.ToString("F0")}%";

        // Définir la zone où le texte sera dessiné (avec un peu de marge par rapport à la boîte)
        Rect textRect = new(20, 35, 230, 100);

        // Dessiner le texte
        GUI.Label(textRect, debugText, textStyle);
    }
}