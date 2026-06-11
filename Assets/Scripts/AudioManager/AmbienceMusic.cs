using Ability.NewAbilitySystem;
using UnityEngine;

public class AmbienceMusic : MonoBehaviour
{
    private AudioManager _audioManager;
    private GameModeManager _gameModeManager;


    [Header("Music")]
    public string explorationMusic;
    public string combatMusic;

    [Header("Room")]
    public string effectClearing;



    private void Start()
    {
        _audioManager = AudioManager.instance;
        _gameModeManager = GameModeManager.Instance;

        GameModeManager.OnGameModeChanged += HandleAmbienceChange;
        HandleAmbienceChange(GameModeManager.Instance.CurrentMode);

    }

    // Change music ambience with Game mode : Exploration or Combat
    private void HandleAmbienceChange(GameMode newMode)
    {
        Debug.Log("game mode changed");
        if (newMode == GameMode.Exploration)
        {
            _audioManager.Play(explorationMusic);
            _audioManager.Pause(combatMusic);
        }
        else
        {
            _audioManager.Play(combatMusic);
            _audioManager.Pause(explorationMusic);
        }

    }

    private void HandleClearingRoom()
    {

    }
}
