using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Agojie.Audio;
using Agojie.Core;

namespace Agojie.Player
{
    /// <summary>Quand le joueur meurt : message à l'écran puis on recommence le niveau.</summary>
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        public Text deathText;          // optionnel
        public float restartDelay = 3f;

        HealthComponent health;

        void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        void OnEnable()
        {
            health.OnDeath += HandleDeath;
            health.OnDamageTaken += HandleHurt;
        }

        void OnDisable()
        {
            health.OnDeath -= HandleDeath;
            health.OnDamageTaken -= HandleHurt;
        }

        void HandleHurt(Vector3 hitPoint, Vector3 direction)
        {
            if (!health.IsDead)
                GameAudio.Play(Sfx.Hurt, transform.position, 0.8f);
        }

        void HandleDeath()
        {
            GameAudio.Play(Sfx.Death, transform.position);
            if (deathText != null)
                deathText.text = "L'Agojie est tombée...";
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            yield return new WaitForSecondsRealtime(restartDelay);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
