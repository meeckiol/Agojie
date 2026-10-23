using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agojie.Quests
{
    public enum QuestGoalType
    {
        TalkToNPC,
        AssassinateTarget,
        InfiltrateZone,
        CollectIntel
    }

    public enum QuestState
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }

    [Serializable]
    public class QuestGoal
    {
        public string goalDescription;
        public QuestGoalType goalType;
        public string targetIdentifier; // e.g., "SlaverCaptain_1", "SecretLetter"
        public int requiredAmount = 1;
        public int currentAmount = 0;

        public bool IsReached => currentAmount >= requiredAmount;
    }

    [CreateAssetMenu(fileName = "NewQuest", menuName = "Agojie/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [Header("General Info")]
        public string questId;
        public string questTitle;
        [TextArea(3, 5)]
        public string questDescription;

        [Header("Objectives")]
        public List<QuestGoal> goals = new List<QuestGoal>();

        [Header("Suite de l'histoire")]
        public string nextQuestId; // démarre automatiquement quand celle-ci est finie (vide = fin)

        [Header("State")]
        public QuestState state = QuestState.NotStarted;

        public bool CheckCompletion()
        {
            foreach (var goal in goals)
            {
                if (!goal.IsReached) return false;
            }
            state = QuestState.Completed;
            return true;
        }

        public void ResetQuest()
        {
            state = QuestState.NotStarted;
            foreach (var g in goals)
            {
                g.currentAmount = 0;
            }
        }
    }
}
