using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Agojie.Audio;
using Agojie.Core;
using Agojie.Quests;

namespace Agojie.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyPerception))]
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private EnemyState currentState = EnemyState.Patrol;

        [Header("Patrol Settings")]
        [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
        [SerializeField] private float patrolSpeed = 2.0f;
        [SerializeField] private float waypointWaitTime = 2.5f;

        [Header("Chase Settings")]
        [SerializeField] private float chaseSpeed = 4.8f;
        [SerializeField] private float attackRange = 1.8f;

        [Header("Combat Settings")]
        [SerializeField] private int attackDamage = 15;
        [SerializeField] private float attackCooldown = 1.6f;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRadius = 1.0f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Quest")]
        [SerializeField] private string questTargetId; // ex : "Capitaine" pour une mission d'assassinat

        [Header("Animation")]
        [SerializeField] private Animator animator;

        private NavMeshAgent agent;
        private EnemyPerception perception;
        private HealthComponent health;

        private int currentWaypointIndex = 0;
        private float waypointTimer = 0f;
        private float stunTimer = 0f;
        private float lastAttackTime = -99f;
        private bool isAttacking = false;

        public EnemyState CurrentState => currentState;
        // Prévient l'animation : "Attack", "Stunned".
        public event System.Action<string> OnAction;
        public bool IsDead => currentState == EnemyState.Dead;
        // Assassinat possible tant que l'ennemi n'a pas repéré le joueur.
        public bool CanBeAssassinated => !IsDead && !perception.IsAlerted;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            perception = GetComponent<EnemyPerception>();
            health = GetComponent<HealthComponent>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnEnable()
        {
            perception.OnTargetSpotted += HandleTargetSpotted;
            perception.OnTargetLost += HandleTargetLost;
            perception.OnNoiseHeard += HandleNoiseHeard;
            health.OnDamageTaken += HandleDamageTaken;
            health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            perception.OnTargetSpotted -= HandleTargetSpotted;
            perception.OnTargetLost -= HandleTargetLost;
            perception.OnNoiseHeard -= HandleNoiseHeard;
            health.OnDamageTaken -= HandleDamageTaken;
            health.OnDeath -= HandleDeath;
        }

        private void Start()
        {
            SetState(EnemyState.Patrol);
        }

        private void Update()
        {
            if (IsDead) return;

            UpdateAnimator();

            if (currentState == EnemyState.Stunned)
            {
                UpdateStunned();
                return;
            }

            // Sans NavMesh, l'agent ne peut rien faire (et Unity lèverait des erreurs).
            if (!agent.isOnNavMesh) return;

            switch (currentState)
            {
                case EnemyState.Patrol:
                    UpdatePatrol();
                    break;
                case EnemyState.Suspicious:
                    UpdateSuspicious();
                    break;
                case EnemyState.Chase:
                    UpdateChase();
                    break;
                case EnemyState.Attack:
                    UpdateAttack();
                    break;
            }
        }

        private void SetState(EnemyState newState)
        {
            currentState = newState;
            waypointTimer = 0f;

            switch (currentState)
            {
                case EnemyState.Patrol:
                    SetStopped(false);
                    agent.speed = patrolSpeed;
                    MoveToNextWaypoint();
                    break;
                case EnemyState.Suspicious:
                    SetStopped(false);
                    agent.speed = patrolSpeed;
                    MoveTo(perception.LastKnownPosition);
                    break;
                case EnemyState.Chase:
                    SetStopped(false);
                    agent.speed = chaseSpeed;
                    break;
                case EnemyState.Attack:
                case EnemyState.Stunned:
                    SetStopped(true);
                    break;
                case EnemyState.Dead:
                    SetStopped(true);
                    agent.enabled = false;
                    break;
            }
        }

        // Les appels NavMesh plantent si l'agent n'est pas posé sur le NavMesh.
        private void MoveTo(Vector3 destination)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.SetDestination(destination);
        }

        private void SetStopped(bool stopped)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = stopped;
        }

        private void UpdatePatrol()
        {
            if (patrolWaypoints.Count == 0) return;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                waypointTimer += Time.deltaTime;
                if (waypointTimer >= waypointWaitTime)
                {
                    waypointTimer = 0f;
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Count;
                    MoveToNextWaypoint();
                }
            }
        }

        private void MoveToNextWaypoint()
        {
            if (patrolWaypoints.Count > 0 && patrolWaypoints[currentWaypointIndex] != null)
            {
                MoveTo(patrolWaypoints[currentWaypointIndex].position);
            }
        }

        private void UpdateSuspicious()
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                // Arrived at suspicious position, looking around
                waypointTimer += Time.deltaTime;
                transform.Rotate(0f, 60f * Time.deltaTime, 0f);
                if (waypointTimer >= 4.0f)
                {
                    SetState(EnemyState.Patrol);
                }
            }
        }

        private void UpdateChase()
        {
            if (perception.CurrentTarget == null)
            {
                SetState(EnemyState.Suspicious);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, perception.CurrentTarget.position);

            if (distToTarget <= attackRange)
            {
                SetState(EnemyState.Attack);
            }
            else
            {
                MoveTo(perception.CurrentTarget.position);
            }
        }

        private void UpdateAttack()
        {
            if (perception.CurrentTarget == null)
            {
                SetState(EnemyState.Suspicious);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, perception.CurrentTarget.position);

            // Turn smoothly to face target
            Vector3 direction = (perception.CurrentTarget.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
            }

            if (distToTarget > attackRange && !isAttacking)
            {
                SetState(EnemyState.Chase);
                return;
            }

            if (Time.time >= lastAttackTime + attackCooldown && !isAttacking)
            {
                StartCoroutine(PerformAttackRoutine());
            }
        }

        private void UpdateStunned()
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                SetState(perception.CurrentTarget != null ? EnemyState.Chase : EnemyState.Suspicious);
            }
        }

        /// <summary>Étourdit l'ennemi : il arrête d'attaquer pendant "duration" secondes.</summary>
        public void Stun(float duration)
        {
            if (IsDead) return;
            StopAllCoroutines(); // annule une attaque en cours
            isAttacking = false;
            stunTimer = duration;
            SetState(EnemyState.Stunned);
            if (animator != null) animator.SetTrigger("Stunned");
            OnAction?.Invoke("Stunned");
        }

        private IEnumerator PerformAttackRoutine()
        {
            isAttacking = true;
            lastAttackTime = Time.time;

            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }
            OnAction?.Invoke("Attack");
            GameAudio.Play(Sfx.Swing, transform.position, 0.5f);

            // Attack wind-up anticipation
            yield return new WaitForSeconds(0.4f);

            // Mort ou étourdi pendant l'élan : le coup ne part pas.
            if (currentState != EnemyState.Attack)
            {
                isAttacking = false;
                yield break;
            }

            // Execute attack hitbox check
            Vector3 center = attackPoint != null ? attackPoint.position : (transform.position + transform.forward * 1.2f + Vector3.up);
            Collider[] hits = Physics.OverlapSphere(center, attackRadius, playerLayer);

            foreach (var hit in hits)
            {
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    Vector3 hitDirection = (hit.transform.position - transform.position).normalized;
                    damageable.TakeDamage(attackDamage, hit.transform.position, hitDirection);
                    break; // un seul coup par attaque
                }
            }

            // Attack recovery time
            yield return new WaitForSeconds(0.5f);
            isAttacking = false;
        }

        private void HandleTargetSpotted(Transform target)
        {
            if (currentState != EnemyState.Dead && currentState != EnemyState.Attack && currentState != EnemyState.Stunned)
            {
                if (currentState != EnemyState.Chase)
                    GameAudio.Play(Sfx.Alert, transform.position, 0.8f); // "Elle est là !"
                SetState(EnemyState.Chase);
            }
        }

        private void HandleTargetLost()
        {
            if (currentState == EnemyState.Chase)
            {
                SetState(EnemyState.Suspicious);
            }
        }

        private void HandleNoiseHeard(Vector3 position)
        {
            if (currentState == EnemyState.Patrol || currentState == EnemyState.Suspicious)
            {
                SetState(EnemyState.Suspicious); // va voir d'où vient le bruit
            }
        }

        private void HandleDamageTaken(Vector3 hitPoint, Vector3 hitDirection)
        {
            if (animator != null)
            {
                animator.SetTrigger("TakeHit");
            }

            // If attacked from stealth, immediately turn and investigate
            if (currentState == EnemyState.Patrol || currentState == EnemyState.Suspicious)
            {
                perception.HearNoise(hitPoint - hitDirection * 2f, 20f);
                SetState(perception.CurrentTarget != null ? EnemyState.Chase : EnemyState.Suspicious);
            }
        }

        private void HandleDeath()
        {
            StopAllCoroutines();
            SetState(EnemyState.Dead);
            perception.enabled = false; // un mort ne voit plus rien
            GameAudio.Play(Sfx.Death, transform.position, 0.6f);
            if (animator != null)
            {
                animator.SetTrigger("Die");
            }
            else
            {
                // Pas encore d'animation : on couche le corps au sol.
                transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
                transform.position += Vector3.up * 0.4f;
            }

            // Disable colliders so player doesn't bump into corpse
            foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

            if (!string.IsNullOrEmpty(questTargetId))
            {
                QuestManager.Instance?.ReportProgress(QuestGoalType.AssassinateTarget, questTargetId);
            }

            Destroy(gameObject, 10f); // Clean up corpse after 10s
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            float currentSpeed = agent.enabled ? agent.velocity.magnitude : 0f;
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("IsAlerted", perception.IsAlerted);
        }

        private void OnDrawGizmosSelected()
        {
            if (attackPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
            }
        }
    }
}
