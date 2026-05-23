using UnityEngine;

/// <summary>
///     Displays real-time information about the game's discrete turn state.
/// </summary>
public class TurnDebugUI : MonoBehaviour
{
    private readonly Rect debugWindowRect = new(10, 10, 250, 120);

    // private void OnGUI()
    // {
    //     if (TurnManager.Instance is null) return;
// 
    //     // Fetch discrete data from the new Manager
    //     bool isExecuting = TurnManager.Instance.IsExecuting;
    //     int currentTurn = TurnManager.Instance.CurrentTurn;
    //     float currentScale = Time.timeScale;
// 
    //     GUI.Box(debugWindowRect, "Turn Manager Debug");
// 
    //     GUIStyle textStyle = new(GUI.skin.label)
    //     {
    //         fontSize = 14,
    //         normal =
    //         {
    //             textColor = Color.white
    //         }
    //     };
// 
    //     string state = isExecuting ? "Resolving Actions..." : "Awaiting Input";
// 
    //     string debugText =
    //         $"State: {state}\n" +
    //         $"Global Turn: {currentTurn}\n" +
    //         $"Time Scale: {currentScale:F2}x\n" +
    //         $"Time Per Turn: {TurnManager.Instance.secondsPerTurn}s";
// 
    //     Rect textRect = new(20, 35, 230, 80);
    //     GUI.Label(textRect, debugText, textStyle);
    // }
}