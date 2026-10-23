using UnityEngine;

namespace Agojie.Core
{
    /// <summary>
    /// Interface implemented by any entity that can receive damage (Player, Enemies, Destructible objects).
    /// </summary>
    public interface IDamageable
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
        bool IsDead { get; }

        void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection);
    }
}
