using System.Collections;
using UnityEngine;
using Agojie.AI;
using Agojie.Audio;
using Agojie.Core;
using Agojie.Quests;

namespace Agojie.Player
{
    // Déplacements du joueur : marcher, courir, s'accroupir, esquiver, sauter, grimper.
    // Le CharacterController gère les collisions (murs, sol) à notre place.
    // Le pivot du joueur doit être aux PIEDS (le constructeur de scène le fait).
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour, IStealthTarget
    {
        [Header("Déplacement")]
        public float walkSpeed = 3f;
        public float runSpeed = 7f;
        public float crouchSpeed = 1.8f;
        public float rotationSpeed = 12f;
        public float jumpHeight = 1.2f;
        public float gravity = -20f;

        [Header("Accroupi (C) : furtivité")]
        public float crouchHeight = 1.2f;
        public Transform visual; // modèle 3D, écrasé quand on s'accroupit (optionnel)

        [Header("Esquive (Ctrl gauche)")]
        public float dodgeSpeed = 11f;
        public float dodgeDuration = 0.35f;
        public float invulnerableTime = 0.25f; // pendant ce temps, les coups ne font rien
        public float dodgeCooldown = 0.8f;

        [Header("Bruit : courir alerte les ennemis proches")]
        public float runNoiseRadius = 8f;
        public LayerMask enemyLayers;

        [Header("Escalade")]
        public float climbSpeed = 2.5f;
        public float wallCheckDistance = 0.7f;
        public LayerMask climbableLayer;

        [Header("Références")]
        public Transform cameraTransform;
        public Animator animator; // optionnel, pour plus tard

        // Le combat met canMove à false pendant une attaque.
        [HideInInspector] public bool canMove = true;
        public bool IsClimbing { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsDodging { get; private set; }

        // Prévient l'animation : "Dodge", "Jump".
        public event System.Action<string> OnAction;

        // Ce que voient les ennemis : accroupi = discret, en course = voyant.
        public float Visibility => IsCrouching ? 0.45f : (IsRunning ? 1.3f : 1f);

        CharacterController controller;
        HealthComponent health;
        Vector3 velocity;      // utilisé surtout pour la gravité (axe Y)
        RaycastHit wallHit;    // infos sur le mur devant nous
        float currentSpeed;
        float standHeight;
        Vector3 standCenter;
        Vector3 visualScale;
        float lastDodgeTime = -99f;
        float noiseTimer;

        bool InputLocked => !canMove
            || (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue);

        void Start()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<HealthComponent>();
            standHeight = controller.height;
            standCenter = controller.center;
            if (visual != null) visualScale = visual.localScale;
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            if (health != null && health.IsDead)
                canMove = false;
            if (IsDodging)
                return; // la coroutine d'esquive déplace le joueur
            if (IsClimbing)
                Climb();
            else
                Move();
            UpdateAnimator();
        }

        // ---------- Marcher / courir / s'accroupir / sauter ----------
        void Move()
        {
            Vector3 direction = InputLocked ? Vector3.zero : CameraRelativeInput();

            if (!InputLocked)
            {
                if (Input.GetKeyDown(KeyCode.C))
                    SetCrouch(!IsCrouching);
                if (Input.GetKeyDown(KeyCode.LeftControl) && TryDodge(direction))
                    return;
            }

            IsRunning = !IsCrouching && Input.GetKey(KeyCode.LeftShift) && direction.sqrMagnitude > 0.01f;
            float speed = IsCrouching ? crouchSpeed : (IsRunning ? runSpeed : walkSpeed);
            currentSpeed = direction.magnitude * speed;

            // Le personnage se tourne doucement vers la direction de marche.
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, target,
                    rotationSpeed * Time.deltaTime);
            }

            // Gravité et saut (pas de saut accroupi).
            if (controller.isGrounded && velocity.y < 0f)
                velocity.y = -2f; // colle le joueur au sol
            if (controller.isGrounded && !InputLocked && !IsCrouching && Input.GetButtonDown("Jump"))
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                OnAction?.Invoke("Jump");
            }
            velocity.y += gravity * Time.deltaTime;

            Vector3 motion = direction * currentSpeed + Vector3.up * velocity.y;
            controller.Move(motion * Time.deltaTime);

            if (IsRunning)
                EmitRunNoise();

            // Avancer contre un mur escaladable = commencer à grimper.
            if (!InputLocked && !IsCrouching && Input.GetAxisRaw("Vertical") > 0f && WallInFront())
                StartClimb();
        }

        // ZQSD transformé en direction par rapport à la caméra.
        Vector3 CameraRelativeInput()
        {
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);
            if (cameraTransform == null)
                return input;
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            return forward.normalized * input.z + right.normalized * input.x;
        }

        void SetCrouch(bool crouch)
        {
            IsCrouching = crouch;
            float height = crouch ? crouchHeight : standHeight;
            controller.height = height;
            // On garde les pieds au même endroit : seul le haut de la capsule descend.
            controller.center = standCenter + Vector3.down * (standHeight - height) / 2f;
            if (visual != null)
            {
                float ratio = height / standHeight;
                visual.localScale = new Vector3(visualScale.x, visualScale.y * ratio, visualScale.z);
                visual.localPosition = new Vector3(visual.localPosition.x, controller.center.y, visual.localPosition.z);
            }
        }

        // Courir fait du bruit : les ennemis proches viennent voir.
        void EmitRunNoise()
        {
            noiseTimer -= Time.deltaTime;
            if (noiseTimer > 0f)
                return;
            noiseTimer = 0.5f;
            foreach (Collider c in Physics.OverlapSphere(transform.position, runNoiseRadius, enemyLayers))
            {
                EnemyPerception perception = c.GetComponentInParent<EnemyPerception>();
                if (perception != null)
                    perception.HearNoise(transform.position, runNoiseRadius);
            }
        }

        // ---------- Esquive ----------
        bool TryDodge(Vector3 direction)
        {
            if (Time.time < lastDodgeTime + dodgeCooldown || !controller.isGrounded)
                return false;
            if (direction.sqrMagnitude < 0.01f)
                direction = -transform.forward; // sans direction : on recule
            StartCoroutine(Dodge(direction.normalized));
            return true;
        }

        IEnumerator Dodge(Vector3 direction)
        {
            IsDodging = true;
            lastDodgeTime = Time.time;
            if (IsCrouching)
                SetCrouch(false);
            if (health != null)
                health.IsInvulnerable = true;
            if (animator != null)
                animator.SetTrigger("Dodge");
            OnAction?.Invoke("Dodge");
            GameAudio.Play(Sfx.Dodge, transform.position, 0.6f);

            float elapsed = 0f;
            while (elapsed < dodgeDuration)
            {
                if (health != null && elapsed >= invulnerableTime)
                    health.IsInvulnerable = false;
                controller.Move((direction * dodgeSpeed + Vector3.down * 2f) * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (health != null)
                health.IsInvulnerable = false;
            IsDodging = false;
        }

        // ---------- Grimper ----------
        void Climb()
        {
            float h = InputLocked ? 0f : Input.GetAxisRaw("Horizontal");
            float v = InputLocked ? 0f : Input.GetAxisRaw("Vertical");
            currentSpeed = 0f;

            // Plus de mur devant la poitrine : on est arrivé en haut.
            if (!WallInFront())
            {
                ClimbOverLedge();
                return;
            }

            // Toujours face au mur.
            transform.rotation = Quaternion.LookRotation(-wallHit.normal);
            Vector3 move = (Vector3.up * v + transform.right * h) * climbSpeed;
            controller.Move(move * Time.deltaTime);

            // Espace = lâcher le mur ; redescendu au sol = on arrête.
            if (Input.GetButtonDown("Jump") || (v < 0f && controller.isGrounded))
                StopClimb();
        }

        bool WallInFront()
        {
            Vector3 chest = transform.position + Vector3.up * 1f;
            return Physics.Raycast(chest, transform.forward, out wallHit,
                wallCheckDistance, climbableLayer);
        }

        void StartClimb()
        {
            IsClimbing = true;
            IsRunning = false;
            velocity = Vector3.zero;
        }

        void StopClimb()
        {
            IsClimbing = false;
        }

        void ClimbOverLedge()
        {
            StopClimb();
            // On désactive le controller le temps de "téléporter" le joueur
            // sur le rebord, sinon il bloque le mouvement.
            controller.enabled = false;
            transform.position += Vector3.up * 1f + transform.forward * 0.6f;
            controller.enabled = true;
        }

        // ---------- Animations (quand vous aurez un modèle 3D) ----------
        void UpdateAnimator()
        {
            if (animator == null)
                return;
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("IsClimbing", IsClimbing);
            animator.SetBool("IsCrouching", IsCrouching);
            animator.SetBool("IsGrounded", controller.isGrounded);
        }
    }
}
