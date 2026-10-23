using UnityEngine;
using UnityEngine.UI;
using Agojie.Quests;

namespace Agojie.UI
{
    public class QuestTrackerUI : MonoBehaviour
    {
        [Header("UI Text Components")]
        [SerializeField] private Text questTitleText;
        [SerializeField] private Text questObjectiveText;
        [SerializeField] private CanvasGroup containerGroup;

        private QuestManager subscribedManager;

        // Start (et pas OnEnable) : tous les Awake sont passés, donc QuestManager.Instance existe.
        private void Start()
        {
            if (containerGroup != null) containerGroup.alpha = 0f;
            subscribedManager = QuestManager.Instance;
            if (subscribedManager != null)
            {
                subscribedManager.OnQuestStarted += HandleQuestStarted;
                subscribedManager.OnQuestProgress += HandleQuestProgress;
                subscribedManager.OnQuestCompleted += HandleQuestCompleted;
            }
        }

        private void OnDestroy()
        {
            if (subscribedManager != null)
            {
                subscribedManager.OnQuestStarted -= HandleQuestStarted;
                subscribedManager.OnQuestProgress -= HandleQuestProgress;
                subscribedManager.OnQuestCompleted -= HandleQuestCompleted;
            }
        }

        private void HandleQuestStarted(QuestData quest)
        {
            if (containerGroup != null) containerGroup.alpha = 1f;
            if (questTitleText != null) questTitleText.text = quest.questTitle;
            ShowGoals(quest);
        }

        private void HandleQuestProgress(QuestData quest, QuestGoal goal)
        {
            ShowGoals(quest);
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            if (questObjectiveText == null) return;
            questObjectiveText.text = string.IsNullOrEmpty(quest.nextQuestId)
                ? "✓ Mission accomplie. Abomey te remercie, Agojie !"
                : "✓ Terminé ! Nouvelle mission bientôt...";
        }

        // Affiche chaque objectif : "- Ramasser les lettres (1/3)" ou "✓ ..." quand il est fait.
        private void ShowGoals(QuestData quest)
        {
            if (questObjectiveText == null) return;
            var lines = new System.Text.StringBuilder();
            foreach (var goal in quest.goals)
            {
                string mark = goal.IsReached ? "✓ " : "- ";
                string count = goal.requiredAmount > 1 ? $" ({goal.currentAmount}/{goal.requiredAmount})" : "";
                lines.AppendLine(mark + goal.goalDescription + count);
            }
            questObjectiveText.text = lines.ToString().TrimEnd();
        }
    }
}
