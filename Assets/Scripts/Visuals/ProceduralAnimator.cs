using UnityEngine;
using Agojie.AI;
using Agojie.Audio;
using Agojie.Core;
using Agojie.Player;

namespace Agojie.Visuals
{
    /// <summary>
    /// Anime un personnage fait de formes simples, sans fichier d'animation :
    /// chaque frame, on calcule l'angle des bras, jambes, torse et tête avec des sinus.
    /// Marche/course, accroupi, escalade, attaques, roulade, étourdi, coup reçu, mort.
    /// Joue aussi les bruits de pas, synchronisés avec les jambes.
    /// </summary>
    public class ProceduralAnimator : MonoBehaviour
    {
        [Header("Articulations (pivots)")]
        public Transform hips;
        public Transform torso;
        public Transform head;
        public Transform armL;
        public Transform armR;
        public Transform legL;
        public Transform legR;

        [Header("Réglages")]
        public float stepLength = 0.8f;      // mètres parcourus par pas
        public float footstepVolume = 0.3f;  // 0 = pas de bruits de pas
        public bool kneel;                   // à genoux (captifs)

        PlayerMovement player;
        PlayerCombat combat;
        EnemyController enemy;
        HealthComponent health;

        Quaternion hipsRest, torsoRest, headRest, armLRest, armRRest, legLRest, legRRest, modelRest;
        Vector3 hipsRestPos;
        Vector3 lastPosition;
        float phase;        // avance avec la marche : sin(phase) balance les membres
        float speed;        // vitesse lissée (m/s)
        float hitTimer;
        float deathBlend;
        string action = "";
        float actionTime;
        float actionDuration = 0.5f;

        void Awake()
        {
            player = GetComponentInParent<PlayerMovement>();
            combat = GetComponentInParent<PlayerCombat>();
            enemy = GetComponentInParent<EnemyController>();
            health = GetComponentInParent<HealthComponent>();

            if (hips == null || torso == null || head == null || armL == null || armR == null || legL == null || legR == null)
            {
                Debug.LogWarning("[ProceduralAnimator] Articulation manquante sur " + name);
                enabled = false;
                return;
            }
            hipsRest = hips.localRotation;
            hipsRestPos = hips.localPosition;
            torsoRest = torso.localRotation;
            headRest = head.localRotation;
            armLRest = armL.localRotation;
            armRRest = armR.localRotation;
            legLRest = legL.localRotation;
            legRRest = legR.localRotation;
            modelRest = transform.localRotation;
            lastPosition = transform.position;
        }

        void OnEnable()
        {
            if (player != null) player.OnAction += Play;
            if (combat != null) combat.OnAction += Play;
            if (enemy != null) enemy.OnAction += Play;
            if (health != null) health.OnDamageTaken += HandleHit;
        }

        void OnDisable()
        {
            if (player != null) player.OnAction -= Play;
            if (combat != null) combat.OnAction -= Play;
            if (enemy != null) enemy.OnAction -= Play;
            if (health != null) health.OnDamageTaken -= HandleHit;
        }

        // Une action jouée par-dessus la marche (attaque, roulade...).
        public void Play(string actionName)
        {
            action = actionName;
            actionTime = 0f;
            switch (actionName)
            {
                case "Attack1":
                case "Attack2":
                case "Attack3": actionDuration = combat != null ? combat.lightDuration : 0.5f; break;
                case "HeavyAttack": actionDuration = combat != null ? combat.heavyDuration : 1f; break;
                case "Assassinate": actionDuration = combat != null ? combat.assassinateDuration : 0.9f; break;
                case "Dodge": actionDuration = player != null ? player.dodgeDuration : 0.35f; break;
                case "Attack": actionDuration = 0.9f; break;   // ennemi : élan 0.4 s + récupération 0.5 s
                case "Stunned": actionDuration = 1.2f; break;
                default: actionDuration = 0.4f; break;
            }
        }

        void HandleHit(Vector3 hitPoint, Vector3 direction)
        {
            hitTimer = 0.25f;
        }

        // Appelé par QuestInteractable (BroadcastMessage) : le captif libéré se relève.
        void OnQuestInteracted()
        {
            kneel = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            if (enemy != null && enemy.IsDead)
                return; // EnemyController couche déjà le corps au sol

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            float vertical = Mathf.Abs(delta.y) / dt;
            delta.y = 0f;
            speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-10f * dt));

            if (player != null && health != null && health.IsDead)
            {
                // Mort du joueur : le corps bascule en arrière.
                deathBlend = Mathf.MoveTowards(deathBlend, 1f, dt * 2f);
                transform.localRotation = modelRest * Quaternion.Euler(-85f * deathBlend, 0f, 0f);
                return;
            }

            bool climbing = player != null && player.IsClimbing;
            bool crouch = kneel || (player != null && player.IsCrouching);

            // ---- Cycle de marche
            float previous = Mathf.Sin(phase);
            phase += (climbing ? vertical * 2f : speed) / stepLength * Mathf.PI * dt;
            phase = Mathf.Repeat(phase, Mathf.PI * 2f); // reste petit, le sinus ne change pas
            float swing = Mathf.Sin(phase);
            bool footDown = Mathf.Sign(previous) != Mathf.Sign(swing);
            if (footDown && !climbing && speed > 0.6f && footstepVolume > 0f)
                GameAudio.Play(Sfx.Step, transform.position, footstepVolume * (crouch ? 0.35f : Mathf.Clamp(speed / 4f, 0.5f, 1.3f)));

            float legAmp = Mathf.Min(speed * 11f, 55f);
            float moving = Mathf.Clamp01(speed / 2.5f);

            float hipsX = 0f;
            float torsoX = Mathf.Clamp01(speed / 7f) * 14f + Mathf.Sin(Time.time * 2f) * 1.2f; // penché en courant + respiration
            float torsoY = 0f;
            float headX = 0f;
            float headZ = 0f;
            float armLX = -swing * legAmp * 0.8f;
            float armRX = swing * legAmp * 0.8f;
            float armRY = 0f;
            float legLX = swing * legAmp;
            float legRX = -swing * legAmp;
            Vector3 hipsOffset = Vector3.up * (Mathf.Abs(Mathf.Cos(phase)) * 0.05f * moving);

            if (crouch)
            {
                hipsOffset += Vector3.down * 0.38f;
                legLX = -55f + swing * legAmp * 0.4f;
                legRX = -55f - swing * legAmp * 0.4f;
                torsoX += 22f;
                armLX = -25f;
                armRX = -25f + swing * 10f;
            }

            if (climbing)
            {
                armLX = -165f + swing * 20f;
                armRX = -165f - swing * 20f;
                legLX = -20f + swing * 25f;
                legRX = -20f - swing * 25f;
                torsoX = 0f;
            }

            // ---- Actions
            if (action != "")
            {
                actionTime += dt;
                float t = Mathf.Clamp01(actionTime / actionDuration);
                switch (action)
                {
                    case "Attack1": // taille horizontale
                    {
                        float s = Ease(0.2f, 0.5f, t);
                        armRX = -95f;
                        armRY = Mathf.Lerp(-80f, 70f, s);
                        torsoY = Mathf.Lerp(-25f, 25f, s);
                        break;
                    }
                    case "Attack2": // revers
                    {
                        float s = Ease(0.2f, 0.5f, t);
                        armRX = -95f;
                        armRY = Mathf.Lerp(70f, -80f, s);
                        torsoY = Mathf.Lerp(25f, -25f, s);
                        break;
                    }
                    case "Attack3": // coup vertical
                    {
                        float s = Ease(0.2f, 0.5f, t);
                        armRX = Mathf.Lerp(-175f, -30f, s);
                        torsoX += Mathf.Lerp(-10f, 20f, s);
                        break;
                    }
                    case "HeavyAttack": // armé au-dessus de la tête, puis on écrase
                    {
                        float up = Ease(0f, 0.35f, t);
                        float down = Ease(0.35f, 0.45f, t);
                        armRX = Mathf.Lerp(Mathf.Lerp(0f, -185f, up), -20f, down);
                        armLX = armRX;
                        torsoX += Mathf.Lerp(Mathf.Lerp(0f, -15f, up), 30f, down);
                        hipsOffset += Vector3.down * 0.15f * down;
                        break;
                    }
                    case "Assassinate": // on recule la lame, puis estocade
                    {
                        float back = Ease(0f, 0.4f, t);
                        float thrust = Ease(0.4f, 0.55f, t);
                        armRX = Mathf.Lerp(Mathf.Lerp(0f, -60f, back), -100f, thrust);
                        armRY = Mathf.Lerp(-30f * back, 0f, thrust);
                        torsoY = Mathf.Lerp(-30f * back, 10f, thrust);
                        torsoX += 15f * thrust;
                        break;
                    }
                    case "Dodge": // roulade avant
                    {
                        hipsX = 360f * Ease(0f, 1f, t);
                        hipsOffset += Vector3.down * 0.45f * Mathf.Sin(Mathf.PI * t);
                        legLX = -70f;
                        legRX = -70f;
                        armLX = -60f;
                        armRX = -60f;
                        break;
                    }
                    case "Attack": // ennemi : grand coup de haut en bas
                    {
                        float up = Ease(0f, 0.35f, t);
                        float down = Ease(0.38f, 0.5f, t);
                        armRX = Mathf.Lerp(Mathf.Lerp(0f, -170f, up), -30f, down);
                        torsoX += Mathf.Lerp(-10f * up, 18f, down);
                        break;
                    }
                    case "Stunned": // tête qui tourne
                    {
                        headZ = Mathf.Sin(actionTime * 18f) * 18f;
                        headX = 15f;
                        torsoX -= 12f;
                        armLX = 10f;
                        armRX = 10f;
                        break;
                    }
                }
                if (actionTime >= actionDuration)
                    action = "";
            }

            // ---- Coup reçu : le buste part en arrière
            if (hitTimer > 0f)
            {
                hitTimer -= dt;
                float k = Mathf.Clamp01(hitTimer / 0.25f);
                torsoX -= 18f * k;
                headX -= 10f * k;
            }

            hips.localPosition = hipsRestPos + hipsOffset;
            hips.localRotation = hipsRest * Quaternion.Euler(hipsX, 0f, 0f);
            torso.localRotation = torsoRest * Quaternion.Euler(torsoX, torsoY, 0f);
            head.localRotation = headRest * Quaternion.Euler(headX, 0f, headZ);
            armL.localRotation = armLRest * Quaternion.Euler(armLX, 0f, -6f);
            armR.localRotation = armRRest * Quaternion.Euler(armRX, armRY, 6f);
            legL.localRotation = legLRest * Quaternion.Euler(legLX, 0f, 0f);
            legR.localRotation = legRRest * Quaternion.Euler(legRX, 0f, 0f);
        }

        // 0 avant "from", 1 après "to", transition douce entre les deux.
        static float Ease(float from, float to, float t)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));
        }
    }
}
