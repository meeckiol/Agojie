using UnityEngine;
using UnityEngine.UI;
using Agojie.Core;
using Agojie.Quests;

namespace Agojie.Player
{
    /// <summary>
    /// Cherche l'objet interactif le plus proche (PNJ, levier...) et l'utilise avec E.
    /// Pendant un dialogue, c'est DialogueUI qui gère la touche E.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float interactRange = 2f;
        public LayerMask interactableLayers = ~0;
        public Text promptText; // optionnel : affiche "[E] Parler à ..."

        IInteractable current;

        void Update()
        {
            bool inDialogue = DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue;
            current = inDialogue ? null : FindNearest();

            if (promptText != null)
                promptText.text = current != null ? "[E] " + current.GetInteractionPrompt() : "";

            if (current != null && Time.timeScale > 0f && Input.GetKeyDown(KeyCode.E))
                current.Interact(gameObject);
        }

        IInteractable FindNearest()
        {
            IInteractable best = null;
            float bestDistance = float.MaxValue;
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up, interactRange,
                interactableLayers, QueryTriggerInteraction.Collide);
            foreach (Collider c in hits)
            {
                IInteractable interactable = c.GetComponentInParent<IInteractable>();
                if (interactable == null)
                    continue;
                float distance = (c.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = interactable;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
