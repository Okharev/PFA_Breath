using System;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    public class OxygenComponent : MonoBehaviour
    {
        [Header("Oxygen Settings")] public float maxOxygen = 100f;

        [SerializeField] public float CurrentOxygen;

        private void Awake()
        {
            CurrentOxygen = maxOxygen;
        }

        private void Start()
        {
            OnOxygenChanged?.Invoke(CurrentOxygen, maxOxygen);
        }

        // UI Event Hook
        public event Action<float, float> OnOxygenChanged;

        public bool HasOxygen(float amount)
        {
            return CurrentOxygen >= amount;
        }

        public void Consume(float amount)
        {
            CurrentOxygen = Mathf.Clamp(CurrentOxygen - amount, 0f, maxOxygen);
            OnOxygenChanged?.Invoke(CurrentOxygen, maxOxygen);
        }

        public void Replenish(float amount)
        {
            CurrentOxygen = Mathf.Clamp(CurrentOxygen + amount, 0f, maxOxygen);
            OnOxygenChanged?.Invoke(CurrentOxygen, maxOxygen);
        }
    }
}