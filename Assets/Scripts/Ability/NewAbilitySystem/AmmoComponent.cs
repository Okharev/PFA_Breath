using System;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    public class AmmoComponent : MonoBehaviour
    {
        [Header("Magazine Settings")]
        public int maxAmmo = 30;
    
        public int CurrentAmmo { get; private set; }

        // Optional: Event for UI to update instantly when ammo changes
        public event Action<int, int> OnAmmoChanged; 

        private void Awake()
        {
            CurrentAmmo = maxAmmo;
        }

        public bool HasAmmo(int amount)
        {
            return CurrentAmmo >= amount;
        }

        public void Consume(int amount)
        {
            CurrentAmmo = Mathf.Max(0, CurrentAmmo - amount);
            OnAmmoChanged?.Invoke(CurrentAmmo, maxAmmo);
        }

        public void Reload()
        {
            CurrentAmmo = maxAmmo;
            OnAmmoChanged?.Invoke(CurrentAmmo, maxAmmo);
        }
    }
}