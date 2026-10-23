using UnityEngine;
using Agojie.Core;

namespace Agojie.Quests
{
    /// <summary>PNJ avec qui on parle (touche E via PlayerInteractor).</summary>
    public class NPCDialogue : MonoBehaviour, IInteractable
    {
        [SerializeField] private string npcId = "Hounon";   // utilisé par les objectifs TalkToNPC
        [SerializeField] private string npcName = "Hounon"; // affiché dans le prompt
        [SerializeField] private DialogueData dialogue;

        public string GetInteractionPrompt() => "Parler à " + npcName;

        public void Interact(GameObject initiator)
        {
            // Le PNJ se tourne vers celui qui lui parle.
            Vector3 toInitiator = initiator.transform.position - transform.position;
            toInitiator.y = 0f;
            if (toInitiator.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toInitiator);

            QuestManager.Instance?.ReportProgress(QuestGoalType.TalkToNPC, npcId);
            if (DialogueManager.Instance != null && dialogue != null)
            {
                DialogueManager.Instance.StartDialogue(dialogue);
            }
        }
    }
}
