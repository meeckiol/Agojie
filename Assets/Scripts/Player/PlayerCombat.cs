using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Agojie.AI;
using Agojie.Audio;
using Agojie.Core;
using Agojie.Quests;

namespace Agojie.Player
{
    // Combat :
    //  clic gauche = attaque légère (combo de 3 coups, le 3e frappe plus fort)
    //  clic droit  = attaque lourde (étourdit l'ennemi)
    //  F           = assassinat furtif sur un ennemi qui ne t'a pas repéré
    // Les dégâts passent par IDamageable, comme l'IA des ennemis.
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Zone de frappe")]
        public Transform attackPoint;   // objet vide placé devant le joueur
        public float attackRadius = 0.8f;
        public LayerMask enemyLayers;

        [Header("Attaques")]
        public int lightDamage = 20;
        public int heavyDamage = 45;
        public float lightDuration = 0.5f;
        public float heavyDuration = 1f;
        public float comboWindow = 0.8f;      // temps max entre deux coups du combo
        public float heavyStunDuration = 1.2f;

        [Header("Assassinat furtif (F)")]
        public float assassinateRange = 1.8f;
        public float assassinateDuration = 0.9f;

        [Header("Ressenti des coups")]
        public float hitStopDuration = 0.06f; // micro-pause quand un coup touche
        public float shakeStrength = 0.08f;

        public Animator animator; // optionnel

        // Prévient l'animation : "Attack1", "Attack2", "Attack3", "HeavyAttack", "Assassinate".
        public event System.Action<string> OnAction;

        PlayerMovement movement;
        HealthComponent health;
        bool isAttacking;
        int comboStep;
        float lastAttackTime;

        // Quand ne peut-on pas attaquer ?
        bool Busy => isAttacking || Time.timeScale == 0f || health.IsDead
            || (movement != null && (movement.IsClimbing || movement.IsDodging))
            || (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue);

        void Start()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<HealthComponent>();
        }

        void Update()
        {
            if (Busy)
                return;
            // Trop d'attente : le combo repart de zéro.
            if (Time.time - lastAttackTime > comboWindow)
                comboStep = 0;

            if (Input.GetKeyDown(KeyCode.F))
                TryAssassinate();
            else if (Input.GetMouseButtonDown(0))
                StartCoroutine(Attack(false));
            else if (Input.GetMouseButtonDown(1))
                StartCoroutine(Attack(true));
        }

        // ---------- Attaques classiques ----------
        IEnumerator Attack(bool heavy)
        {
            isAttacking = true;
            LockMovement(true); // on ne glisse pas en frappant

            float duration = heavy ? heavyDuration : lightDuration;
            int damage = heavy ? heavyDamage : lightDamage;
            if (heavy)
                comboStep = 0;
            else
            {
                comboStep = comboStep % 3 + 1;                   // 1 -> 2 -> 3 -> 1...
                if (comboStep == 3)
                    damage = Mathf.RoundToInt(damage * 1.5f);    // le 3e coup fait plus mal
            }
            string actionName = heavy ? "HeavyAttack" : "Attack" + comboStep;
            if (animator != null)
                animator.SetTrigger(actionName);
            OnAction?.Invoke(actionName);
            GameAudio.Play(Sfx.Swing, transform.position, heavy ? 0.8f : 0.55f);

            // Le coup touche au milieu de l'animation, pas au début.
            yield return new WaitForSeconds(duration * 0.4f);
            if (DealDamage(damage, heavy))
            {
                HitFeedback(heavy || comboStep == 3 ? 1.6f : 1f);
                GameAudio.Play(heavy ? Sfx.HeavyHit : Sfx.Hit, attackPoint.position);
            }
            yield return new WaitForSeconds(duration * 0.6f);

            lastAttackTime = Time.time;
            LockMovement(false);
            isAttacking = false;
        }

        // Renvoie true si au moins un ennemi a été touché.
        bool DealDamage(int damage, bool heavy)
        {
            Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackRadius, enemyLayers);
            // Un ennemi peut avoir plusieurs colliders : on ne le frappe qu'une fois.
            HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
            bool touched = false;
            foreach (Collider c in hits)
            {
                IDamageable target = c.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || target == (IDamageable)health || !alreadyHit.Add(target))
                    continue;
                Vector3 direction = (c.transform.position - transform.position).normalized;
                target.TakeDamage(damage, c.ClosestPoint(attackPoint.position), direction);
                touched = true;

                EnemyController enemy = c.GetComponentInParent<EnemyController>();
                if (heavy && enemy != null && !enemy.IsDead)
                    enemy.Stun(heavyStunDuration);
            }
            return touched;
        }

        // ---------- Assassinat furtif ----------
        void TryAssassinate()
        {
            EnemyController target = FindAssassinationTarget();
            if (target != null)
                StartCoroutine(Assassinate(target));
        }

        EnemyController FindAssassinationTarget()
        {
            EnemyController best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider c in Physics.OverlapSphere(transform.position, assassinateRange, enemyLayers))
            {
                EnemyController enemy = c.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.CanBeAssassinated)
                    continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }
            return best;
        }

        IEnumerator Assassinate(EnemyController enemy)
        {
            isAttacking = true;
            LockMovement(true);

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(toEnemy);
            if (animator != null)
                animator.SetTrigger("Assassinate");
            OnAction?.Invoke("Assassinate");

            yield return new WaitForSeconds(assassinateDuration * 0.5f);
            IDamageable victim = enemy != null ? enemy.GetComponent<IDamageable>() : null;
            if (victim != null && !victim.IsDead)
            {
                victim.TakeDamage(victim.MaxHealth, enemy.transform.position + Vector3.up, toEnemy.normalized);
                HitFeedback(2.5f);
                GameAudio.Play(Sfx.Assassinate, enemy.transform.position);
            }
            yield return new WaitForSeconds(assassinateDuration * 0.5f);

            LockMovement(false);
            isAttacking = false;
        }

        // ---------- Utilitaires ----------
        void LockMovement(bool locked)
        {
            if (movement != null && !health.IsDead)
                movement.canMove = !locked;
        }

        // Tremblement de caméra + micro-pause : le coup "pèse".
        void HitFeedback(float strength)
        {
            ThirdPersonCamera.Shake(shakeStrength * strength, 0.12f);
            StartCoroutine(HitStop(hitStopDuration * strength));
        }

        IEnumerator HitStop(float duration)
        {
            if (duration <= 0f || Time.timeScale != 1f)
                yield break;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            if (Time.timeScale == 0.05f) // ne pas annuler une mise en pause
                Time.timeScale = 1f;
        }

        // Affiche la zone de frappe dans l'éditeur (sphère rouge).
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, assassinateRange);
            if (attackPoint == null)
                return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
    }
}
