using UnityEngine;
using Agojie.Core;

namespace Agojie.Quests
{
    /// <summary>Zone à atteindre (objectif InfiltrateZone). Le collider doit être en "Is Trigger".</summary>
    [RequireComponent(typeof(Collider))]
    public class QuestZone : MonoBehaviour
    {
        [SerializeField] private string questId = "Q03";
        [SerializeField] private string zoneId = "Entrepot";

        private bool done;

        // Stay (et pas Enter) : marche aussi si le joueur était déjà dedans quand la mission commence.
        private void OnTriggerStay(Collider other)
        {
            if (done || other.GetComponentInParent<IStealthTarget>() == null) return; // seul le joueur compte
            QuestManager manager = QuestManager.Instance;
            if (manager == null || !manager.IsQuestActive(questId)) return;

            done = true;
            manager.ReportProgress(QuestGoalType.InfiltrateZone, zoneId);
        }
    }
}
