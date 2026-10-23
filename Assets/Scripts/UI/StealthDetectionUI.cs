using UnityEngine;
using UnityEngine.UI;
using Agojie.AI;

namespace Agojie.UI
{
    public class StealthDetectionUI : MonoBehaviour
    {
        [Header("Target Perception")]
        [SerializeField] private EnemyPerception enemyPerception;

        [Header("UI Widgets")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image awarenessFillBar;
        [SerializeField] private Color suspiciousColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color alertedColor = new Color(0.9f, 0.1f, 0.1f);

        private void Start()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (enemyPerception == null) return;

            float awareness = enemyPerception.Awareness;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, awareness > 0.05f ? 1f : 0f, Time.deltaTime * 4f);
            }

            if (awarenessFillBar != null)
            {
                awarenessFillBar.fillAmount = awareness;
                awarenessFillBar.color = enemyPerception.IsAlerted ? alertedColor : suspiciousColor;
            }
        }
    }
}
