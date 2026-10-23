using System;
using UnityEngine;

namespace Agojie.Core
{
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;

        // Vrai pendant une esquive : les coups ne font rien.
        public bool IsInvulnerable { get; set; }

        // Events for UI and Animation systems
        public event Action<int, int> OnHealthChanged; // (current, max)
        public event Action<Vector3, Vector3> OnDamageTaken; // (hitPoint, direction)
        public event Action OnDeath;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (IsDead || IsInvulnerable) return;

            currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnDamageTaken?.Invoke(hitPoint, hitDirection);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(int amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            OnDeath?.Invoke();
        }
    }
}
