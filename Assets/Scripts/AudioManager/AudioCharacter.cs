using Ability.NewAbilitySystem;
using UnityEngine;

public class AudioCharacter : MonoBehaviour
{
    
    private AudioManager _audioManager;

    private HealthComponent _healthComponent;
   [SerializeField] private AbilityController _abilityController;

    [Header("Health Component")]
    public string hitSound;
    public string deathSound;

    [Header("Ability Controller")]
    public string abilityExecuted;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _audioManager = AudioManager.instance;

        _healthComponent = GetComponent<HealthComponent>();
        _healthComponent.OnTakeDamage += SoundDamageTaken;
        _healthComponent.OnDeath += SoundDeath;

        //_abilityController.GetComponent<AbilityController>();
        //_abilityController.OnAbilityExecuted += SoundAbility;
    }

    private void SoundDamageTaken(float damage)
    {
        //Debug.Log("un son a été joué");
        
        if (hitSound == null) return;
        _audioManager.Play(hitSound);
        
    }

    private void SoundDeath(GameObject gameObject)
    {
        if (deathSound == null) return;
        _audioManager.Play(deathSound);
    }

    private void SoundAbility()
    {
        if (abilityExecuted == null) return;
        Debug.Log("sound can be played");
        _audioManager.PlayOnGO(abilityExecuted, this.gameObject);
    }
}

