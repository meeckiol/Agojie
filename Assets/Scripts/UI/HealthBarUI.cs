using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Agojie.Core;

namespace Agojie.UI
{
    public class HealthBarUI : MonoBehaviour
    {
        [Header("Target Health Component")]
        [SerializeField] private HealthComponent targetHealth;

        [Header("UI Elements")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image damageGhostImage; // Delayed bar for juice/game-feel
        [SerializeField] private float ghostDelay = 0.5f;
        [SerializeField] private float ghostShrinkSpeed = 2f;

        private Coroutine ghostCoroutine;

        private void Start()
        {
            if (targetHealth != null)
            {
                SetTarget(targetHealth);
            }
        }

        public void SetTarget(HealthComponent newTarget)
        {
            if (targetHealth != null)
            {
                targetHealth.OnHealthChanged -= UpdateHealthUI;
            }

            targetHealth = newTarget;

            if (targetHealth != null)
            {
                targetHealth.OnHealthChanged += UpdateHealthUI;
                UpdateHealthUI(targetHealth.CurrentHealth, targetHealth.MaxHealth);
            }
        }

        private void OnDestroy()
        {
            if (targetHealth != null)
            {
                targetHealth.OnHealthChanged -= UpdateHealthUI;
            }
        }

        private void UpdateHealthUI(int current, int max)
        {
            float fillAmount = max > 0 ? (float)current / max : 0f;

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = fillAmount;
            }

            if (damageGhostImage != null)
            {
                if (ghostCoroutine != null) StopCoroutine(ghostCoroutine);
                // Soin : la barre fantôme remonte tout de suite avec la vraie barre.
                if (fillAmount >= damageGhostImage.fillAmount)
                {
                    damageGhostImage.fillAmount = fillAmount;
                    return;
                }
                ghostCoroutine = StartCoroutine(GhostBarRoutine(fillAmount));
            }
        }

        private IEnumerator GhostBarRoutine(float targetFill)
        {
            yield return new WaitForSeconds(ghostDelay);

            while (damageGhostImage != null && damageGhostImage.fillAmount > targetFill)
            {
                damageGhostImage.fillAmount = Mathf.MoveTowards(damageGhostImage.fillAmount, targetFill, Time.deltaTime * ghostShrinkSpeed);
                yield return null;
            }
        }
    }
}
