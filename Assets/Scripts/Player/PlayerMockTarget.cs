using UnityEngine;
using Agojie.Core;

namespace Agojie.Player
{
    /// <summary>
    /// Temporary/Mock Player component for testing Enemy AI, Perception and UI
    /// until the full Player Controller (movement, climb, combat) is integrated.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerMockTarget : MonoBehaviour
    {
        public HealthComponent Health { get; private set; }

        private void Awake()
        {
            Health = GetComponent<HealthComponent>();
            // Automatically set player layer for testing
            gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }
}
