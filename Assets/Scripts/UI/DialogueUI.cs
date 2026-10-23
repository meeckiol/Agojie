using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Agojie.Quests;

namespace Agojie.UI
{
    /// <summary>
    /// Affiche les dialogues de DialogueManager avec un effet machine à écrire.
    /// E / Entrée / clic : finir la phrase, puis passer à la suivante.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Transform choicesContainer;
        [SerializeField] private Button choiceTemplate; // bouton désactivé servant de modèle
        [SerializeField] private float charsPerSecond = 45f;

        private readonly List<Button> spawnedChoices = new List<Button>();
        private DialogueManager manager;
        private DialogueChoice[] pendingChoices = new DialogueChoice[0];
        private Coroutine typing;
        private string fullText = "";
        private int shownFrame = -1;

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (choiceTemplate != null) choiceTemplate.gameObject.SetActive(false);

            manager = DialogueManager.Instance;
            if (manager == null) return;
            manager.OnDialogueNodeDisplayed += ShowNode;
            manager.OnChoicesDisplayed += ShowChoices;
            manager.OnDialogueEnded += Hide;
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.OnDialogueNodeDisplayed -= ShowNode;
            manager.OnChoicesDisplayed -= ShowChoices;
            manager.OnDialogueEnded -= Hide;
        }

        private void Update()
        {
            // shownFrame : la touche qui a ouvert le dialogue ne doit pas aussi le faire avancer.
            if (panel == null || !panel.activeSelf || Time.frameCount == shownFrame || Time.timeScale == 0f) return;
            bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
            if (!pressed) return;

            if (typing != null)
            {
                FinishTyping();
            }
            else if (pendingChoices.Length == 0)
            {
                manager.NextNode();
            }
        }

        private void ShowNode(string speaker, string text)
        {
            if (panel != null) panel.SetActive(true);
            shownFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (speakerText != null) speakerText.text = speaker;
            fullText = text ?? "";
            ClearChoices();

            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(TypeText());
        }

        private IEnumerator TypeText()
        {
            for (int i = 1; i <= fullText.Length; i++)
            {
                if (bodyText != null) bodyText.text = fullText.Substring(0, i);
                yield return new WaitForSecondsRealtime(1f / charsPerSecond);
            }
            typing = null;
            SpawnChoices();
        }

        private void FinishTyping()
        {
            StopCoroutine(typing);
            typing = null;
            if (bodyText != null) bodyText.text = fullText;
            SpawnChoices();
        }

        // Appelé juste après ShowNode : on garde les choix et on les montre quand le texte est fini.
        private void ShowChoices(DialogueChoice[] choices)
        {
            pendingChoices = choices ?? new DialogueChoice[0];
            if (typing == null) SpawnChoices();
        }

        private void SpawnChoices()
        {
            ClearChoices();
            if (pendingChoices.Length == 0 || choiceTemplate == null || choicesContainer == null) return;

            Cursor.lockState = CursorLockMode.None; // il faut la souris pour cliquer
            Cursor.visible = true;

            for (int i = 0; i < pendingChoices.Length; i++)
            {
                int index = i; // copie locale, sinon tous les boutons utiliseraient le dernier i
                Button button = Instantiate(choiceTemplate, choicesContainer);
                button.gameObject.SetActive(true);
                Text label = button.GetComponentInChildren<Text>();
                if (label != null) label.text = (i + 1) + ". " + pendingChoices[i].choiceText;
                button.onClick.AddListener(() => manager.SelectChoice(index));
                spawnedChoices.Add(button);
            }
        }

        private void ClearChoices()
        {
            foreach (Button button in spawnedChoices)
            {
                if (button != null) Destroy(button.gameObject);
            }
            spawnedChoices.Clear();
        }

        private void Hide()
        {
            if (typing != null)
            {
                StopCoroutine(typing);
                typing = null;
            }
            ClearChoices();
            pendingChoices = new DialogueChoice[0];
            if (panel != null) panel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
