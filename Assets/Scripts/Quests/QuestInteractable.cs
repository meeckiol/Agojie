using UnityEngine;
using Agojie.Audio;
using Agojie.Core;

namespace Agojie.Quests
{
    /// <summary>
    /// Objet ou personne lié à une mission : lettre à ramasser, captif à libérer...
    /// Touche E (via PlayerInteractor). Fait avancer l'objectif "goalType / targetId".
    /// </summary>
    public class QuestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string questId = "Q02";            // utilisable seulement pendant cette mission
        [SerializeField] private QuestGoalType goalType = QuestGoalType.CollectIntel;
        [SerializeField] private string targetId = "Lettre";
        [SerializeField] private string prompt = "Ramasser la lettre";
        [SerializeField] private bool hideUntilQuestActive = true;  // invisible avant la mission
        [SerializeField] private bool destroyOnUse = true;
        [SerializeField] private bool spin = false;                 // tourne sur lui-même pour se faire remarquer
        [SerializeField] private GameObject[] disableOnUse;         // ex : les chaînes d'un captif

        private QuestManager manager;
        private bool used;

        private void Start()
        {
            manager = QuestManager.Instance;
            if (manager != null) manager.OnQuestStarted += HandleQuestStarted;
            if (hideUntilQuestActive) SetVisible(IsActive());
        }

        private void OnDestroy()
        {
            if (manager != null) manager.OnQuestStarted -= HandleQuestStarted;
        }

        private void Update()
        {
            if (spin && !used) transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        public string GetInteractionPrompt() => IsActive() ? prompt : prompt + " (plus tard)";

        public void Interact(GameObject initiator)
        {
            if (used || !IsActive()) return;
            used = true;

            manager.ReportProgress(goalType, targetId);
            GameAudio.Play(Sfx.Pickup, transform.position);
            foreach (GameObject go in disableOnUse)
            {
                if (go != null) go.SetActive(false);
            }
            // Prévient les autres scripts de l'objet (ex : le captif se relève).
            BroadcastMessage("OnQuestInteracted", SendMessageOptions.DontRequireReceiver);

            if (destroyOnUse)
                Destroy(gameObject);
            else
                foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        private bool IsActive() => manager != null && manager.IsQuestActive(questId);

        private void HandleQuestStarted(QuestData quest)
        {
            if (hideUntilQuestActive && quest.questId == questId) SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = visible;
            foreach (Light l in GetComponentsInChildren<Light>()) l.enabled = visible;
        }
    }
}
