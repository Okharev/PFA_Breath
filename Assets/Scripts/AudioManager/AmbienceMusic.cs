using Ability.NewAbilitySystem;
using System.Collections;
using UnityEngine;
using static Autodesk.Fbx.FbxTime;

public class AmbienceMusic : MonoBehaviour
{
    private AudioManager _audioManager;
    private GameModeManager _gameModeManager;

    public float timeToFade = 0.25f;

    [Header("Music")]
    public string explorationMusic;
    public string combatMusic;

    //[Header("Room")]
    //public string effectClearing;



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
        //if (newMode == GameMode.Exploration)
        //{
        //    _audioManager.Play(explorationMusic);
        //    _audioManager.Pause(combatMusic);
        //}
        //else
        //{
        //    _audioManager.Play(combatMusic);
        //    _audioManager.Pause(explorationMusic);
        //}

        StopAllCoroutines();
        StartCoroutine(FadeTrack(newMode));

    }

    private IEnumerator FadeTrack(GameMode newMode)
    {
        float elapsedTime = 0f;

        Sound exploSound = _audioManager.GetSound(explorationMusic);
        Sound combatSound = _audioManager.GetSound(combatMusic);

        if (newMode == GameMode.Exploration)
        {
            _audioManager.Play(explorationMusic);


            while (elapsedTime < timeToFade)
            {
                exploSound.source.volume = Mathf.Lerp(0, 1, elapsedTime / timeToFade);
                combatSound.source.volume = Mathf.Lerp(1, 0, elapsedTime / timeToFade);

                // Define borders for volume
                exploSound.source.volume = Mathf.Clamp(exploSound.source.volume, 0, exploSound.volume);
                combatSound.source.volume = Mathf.Clamp(combatSound.source.volume, 0, combatSound.volume);

                elapsedTime += Time.deltaTime;
                yield return null;
            }


            _audioManager.Pause(combatMusic);
        }
        else
        {
            _audioManager.Play(combatMusic);


            while (elapsedTime < timeToFade)
            {
                combatSound.source.volume = Mathf.Lerp(0, 1, elapsedTime / timeToFade);
                exploSound.source.volume = Mathf.Lerp(1, 0, elapsedTime / timeToFade);

                // Define borders for volume
                exploSound.source.volume = Mathf.Clamp(exploSound.source.volume, 0, exploSound.volume);
                combatSound.source.volume = Mathf.Clamp(combatSound.source.volume, 0, combatSound.volume);

                elapsedTime += Time.unscaledDeltaTime;

                yield return null;
            }

            _audioManager.Pause(explorationMusic);

        }




    }

    private void HandleClearingRoom()
    {

    }
}
