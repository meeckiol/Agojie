using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Agojie.Quests
{
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Header("Quests in Scene / Story")]
        [SerializeField] private List<QuestData> allQuests = new List<QuestData>();
        [SerializeField] private float delayBeforeNextQuest = 4f;

        private List<QuestData> activeQuests = new List<QuestData>();

        public event Action<QuestData> OnQuestStarted;
        public event Action<QuestData, QuestGoal> OnQuestProgress;
        public event Action<QuestData> OnQuestCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Les ScriptableObject gardent leurs valeurs entre deux "Play" dans l'éditeur :
            // sans ce reset, une quête finie une fois resterait "Completed" pour toujours.
            allQuests.RemoveAll(q => q == null);
            foreach (var quest in allQuests)
            {
                quest.ResetQuest();
            }
        }

        public void StartQuest(string questId)
        {
            QuestData quest = allQuests.Find(q => q.questId == questId);
            if (quest != null && quest.state == QuestState.NotStarted)
            {
                quest.state = QuestState.InProgress;
                activeQuests.Add(quest);
                OnQuestStarted?.Invoke(quest);
                Debug.Log($"[Quest Started] {quest.questTitle}");
            }
        }

        public void ReportProgress(QuestGoalType type, string targetIdentifier, int amount = 1)
        {
            foreach (var quest in activeQuests)
            {
                if (quest.state != QuestState.InProgress) continue;

                foreach (var goal in quest.goals)
                {
                    if (goal.goalType == type && goal.targetIdentifier == targetIdentifier && !goal.IsReached)
                    {
                        goal.currentAmount = Mathf.Min(goal.currentAmount + amount, goal.requiredAmount);
                        OnQuestProgress?.Invoke(quest, goal);

                        if (quest.CheckCompletion())
                        {
                            OnQuestCompleted?.Invoke(quest);
                            Debug.Log($"[Quest Completed] {quest.questTitle}!");
                            // Pas tout de suite : on est en train de parcourir activeQuests.
                            if (!string.IsNullOrEmpty(quest.nextQuestId))
                                StartCoroutine(StartQuestLater(quest.nextQuestId));
                        }
                    }
                }
            }
        }

        public List<QuestData> GetActiveQuests() => activeQuests;

        public bool IsQuestActive(string questId)
        {
            QuestData quest = allQuests.Find(q => q.questId == questId);
            return quest != null && quest.state == QuestState.InProgress;
        }

        private IEnumerator StartQuestLater(string questId)
        {
            yield return new WaitForSeconds(delayBeforeNextQuest);
            StartQuest(questId);
        }
    }
}
