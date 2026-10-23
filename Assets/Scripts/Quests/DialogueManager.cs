using System;
using System.Collections;
using UnityEngine;

namespace Agojie.Quests
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        public bool IsInDialogue { get; private set; }

        public event Action<string, string> OnDialogueNodeDisplayed; // speaker, text
        public event Action<DialogueChoice[]> OnChoicesDisplayed;
        public event Action OnDialogueEnded;

        private DialogueData currentDialogue;
        private int currentNodeIndex = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartDialogue(DialogueData dialogue)
        {
            if (dialogue == null || dialogue.nodes.Count == 0) return;

            currentDialogue = dialogue;
            currentNodeIndex = 0;
            IsInDialogue = true;

            DisplayCurrentNode();
        }

        public void NextNode()
        {
            if (!IsInDialogue || currentDialogue == null) return;

            var node = currentDialogue.nodes[currentNodeIndex];
            if (node.choices.Count > 0)
            {
                // Must make a choice; cannot simply skip
                return;
            }

            currentNodeIndex++;
            if (currentNodeIndex < currentDialogue.nodes.Count)
            {
                DisplayCurrentNode();
            }
            else
            {
                EndDialogue();
            }
        }

        public void SelectChoice(int choiceIndex)
        {
            if (!IsInDialogue || currentDialogue == null) return;

            var node = currentDialogue.nodes[currentNodeIndex];
            if (choiceIndex >= 0 && choiceIndex < node.choices.Count)
            {
                var choice = node.choices[choiceIndex];

                if (!string.IsNullOrEmpty(choice.triggerQuestId))
                {
                    QuestManager.Instance?.StartQuest(choice.triggerQuestId);
                }

                if (choice.targetNodeIndex >= 0 && choice.targetNodeIndex < currentDialogue.nodes.Count)
                {
                    currentNodeIndex = choice.targetNodeIndex;
                    DisplayCurrentNode();
                }
                else
                {
                    EndDialogue();
                }
            }
        }

        private void DisplayCurrentNode()
        {
            var node = currentDialogue.nodes[currentNodeIndex];
            OnDialogueNodeDisplayed?.Invoke(node.speakerName, node.dialogueText);

            if (node.choices.Count > 0)
            {
                OnChoicesDisplayed?.Invoke(node.choices.ToArray());
            }
            else
            {
                OnChoicesDisplayed?.Invoke(Array.Empty<DialogueChoice>());
            }
        }

        public void EndDialogue()
        {
            IsInDialogue = false;
            currentDialogue = null;
            currentNodeIndex = 0;
            OnDialogueEnded?.Invoke();
        }
    }
}
