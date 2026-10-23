using UnityEngine;
using Agojie.Quests;

namespace Agojie.Player
{
    // Caméra à la troisième personne : tourne autour du joueur avec la souris
    // et se rapproche quand un mur se trouve entre elle et le joueur.
    // La touche Échap est gérée par PauseMenuUI (curseur + pause).
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;                                  // le joueur
        public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);  // hauteur des épaules
        public float distance = 4f;
        public float minDistance = 0.5f;
        public float mouseSensitivity = 3f;
        public float minPitch = -30f;
        public float maxPitch = 70f;
        public float smooth = 15f;
        public LayerMask collisionLayers = ~0;                    // décoche le layer Player !

        float yaw;          // rotation gauche/droite
        float pitch = 15f;  // rotation haut/bas

        static float shakeIntensity;
        static float shakeTimer;

        /// <summary>Fait trembler la caméra (appelé par le combat quand un coup touche).</summary>
        public static void Shake(float intensity, float duration)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeTimer = Mathf.Max(shakeTimer, duration);
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked; // la souris reste dans le jeu
            Cursor.visible = false;
            if (target != null)
                yaw = target.eulerAngles.y;
        }

        void LateUpdate() // après le déplacement du joueur, pour éviter les tremblements
        {
            if (target == null || Time.timeScale == 0f) // jeu en pause : caméra figée
                return;

            // Pendant un dialogue, la souris sert à cliquer sur les choix.
            bool inDialogue = DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue;
            if (!inDialogue)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 backward = rotation * Vector3.back;

            // Un mur derrière le joueur ? On rapproche la caméra.
            float d = distance;
            if (Physics.SphereCast(pivot, 0.2f, backward, out RaycastHit hit, distance,
                collisionLayers, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(hit.distance, minDistance);

            Vector3 wanted = pivot + backward * d;
            transform.position = Vector3.Lerp(transform.position, wanted, smooth * Time.deltaTime);
            transform.rotation = rotation;

            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                transform.position += Random.insideUnitSphere * shakeIntensity;
                if (shakeTimer <= 0f)
                    shakeIntensity = 0f;
            }
        }
    }
}
