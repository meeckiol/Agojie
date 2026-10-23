using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agojie.Quests
{
    [Serializable]
    public class DialogueChoice
    {
        [TextArea(1, 2)]
        public string choiceText;
        public int targetNodeIndex; // -1 to end conversation
        public string triggerQuestId; // Optional quest to unlock
    }

    [Serializable]
    public class DialogueNode
    {
        public string speakerName;
        [TextArea(3, 5)]
        public string dialogueText;
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }

    [CreateAssetMenu(fileName = "NewDialogue", menuName = "Agojie/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        public string dialogueId;
        public List<DialogueNode> nodes = new List<DialogueNode>();
    }
}
