using UnityEngine;

namespace Agojie.Core
{
    /// <summary>
    /// Interface implemented by any object or NPC that can be interacted with (NPC dialogues, quest items, doors).
    /// </summary>
    public interface IInteractable
    {
        string GetInteractionPrompt();
        void Interact(GameObject initiator);
    }
}
