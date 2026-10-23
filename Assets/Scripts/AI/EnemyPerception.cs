using System;
using UnityEngine;
using Agojie.Core;

namespace Agojie.AI
{
    public class EnemyPerception : MonoBehaviour
    {
        [Header("Vision Settings")]
        [SerializeField] private float viewDistance = 16f;
        [SerializeField] private float viewAngle = 90f; // Cone angle in degrees
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private LayerMask targetMask;     // Layer of player
        [SerializeField] private LayerMask obstacleMask;   // Walls, buildings, props

        [Header("Awareness & Stealth Detection")]
        [SerializeField] private float secondsToDetect = 1.5f;    // au bout du champ de vision, cible normale
        [SerializeField] private float detectionDecayRate = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float currentAwareness = 0f; // 0 = unaware, 1 = fully alerted

        public float Awareness => currentAwareness;
        public bool IsAlerted => currentAwareness >= 1f;
        public Transform CurrentTarget { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }

        public event Action<float> OnAwarenessChanged; // For HUD stealth meter above enemy head
        public event Action<Transform> OnTargetSpotted;
        public event Action OnTargetLost;
        public event Action<Vector3> OnNoiseHeard;

        private bool hasSpotted; // évite d'envoyer OnTargetSpotted à chaque frame

        private void Update()
        {
            ScanForTargets();
        }

        // Ennemi mort (script désactivé) : la jauge au-dessus de sa tête s'éteint.
        private void OnDisable()
        {
            currentAwareness = 0f;
            OnAwarenessChanged?.Invoke(currentAwareness);
        }

        private void ScanForTargets()
        {
            if (TryFindVisibleTarget(out Transform target, out float distance, out float visibility))
            {
                CurrentTarget = target;
                LastKnownPosition = target.position;

                // Plus la cible est proche et visible, plus la jauge monte vite.
                float closeness = 1f - Mathf.Clamp01(distance / viewDistance);
                float rate = (1f + closeness * 2f) / secondsToDetect * visibility;
                currentAwareness = Mathf.Clamp01(currentAwareness + rate * Time.deltaTime);
            }
            else if (currentAwareness > 0f)
            {
                currentAwareness = Mathf.Clamp01(currentAwareness - detectionDecayRate * Time.deltaTime);
                if (currentAwareness <= 0f)
                {
                    LoseTarget();
                }
            }

            OnAwarenessChanged?.Invoke(currentAwareness);

            if (IsAlerted && !hasSpotted && CurrentTarget != null)
            {
                hasSpotted = true;
                OnTargetSpotted?.Invoke(CurrentTarget);
            }
        }

        private bool TryFindVisibleTarget(out Transform target, out float distance, out float visibility)
        {
            target = null;
            distance = 0f;
            visibility = 1f;

            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Collider[] hits = Physics.OverlapSphere(eyePosition, viewDistance, targetMask);

            foreach (var hit in hits)
            {
                IStealthTarget stealth = hit.GetComponentInParent<IStealthTarget>();
                float vis = stealth != null ? stealth.Visibility : 1f;
                if (vis <= 0f) continue;

                // Haut du corps de la cible : accroupie derrière un muret, elle est cachée.
                Vector3 targetCenter = hit.bounds.center + Vector3.up * hit.bounds.extents.y * 0.6f;
                Vector3 toTarget = targetCenter - eyePosition;
                float dist = toTarget.magnitude;

                // Une cible discrète doit être plus proche pour être vue.
                if (dist > viewDistance * Mathf.Clamp01(vis)) continue;
                if (Vector3.Angle(transform.forward, toTarget) > viewAngle / 2f) continue;
                if (Physics.Raycast(eyePosition, toTarget.normalized, dist, obstacleMask)) continue;

                target = hit.transform;
                distance = dist;
                visibility = vis;
                return true;
            }
            return false;
        }

        private void LoseTarget()
        {
            hasSpotted = false;
            if (CurrentTarget != null)
            {
                CurrentTarget = null;
                OnTargetLost?.Invoke();
            }
        }

        /// <summary>Alerte immédiate (ex : un allié crie, le joueur frappe de face).</summary>
        public void Alert(Transform target)
        {
            if (target == null) return;
            CurrentTarget = target;
            LastKnownPosition = target.position;
            currentAwareness = 1f;
            OnAwarenessChanged?.Invoke(currentAwareness);
            if (!hasSpotted)
            {
                hasSpotted = true;
                OnTargetSpotted?.Invoke(target);
            }
        }

        public void HearNoise(Vector3 noisePosition, float noiseRange)
        {
            float distance = Vector3.Distance(transform.position, noisePosition);
            if (distance <= noiseRange)
            {
                LastKnownPosition = noisePosition;
                // Hearing a loud noise bumps suspicion to at least 50%
                currentAwareness = Mathf.Max(currentAwareness, 0.5f);
                OnAwarenessChanged?.Invoke(currentAwareness);
                OnNoiseHeard?.Invoke(noisePosition);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Debug visualization in Unity Scene View
            Gizmos.color = IsAlerted ? Color.red : (currentAwareness > 0f ? Color.yellow : Color.blue);
            Gizmos.DrawWireSphere(transform.position, viewDistance);

            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            Vector3 leftRayDirection = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
            Vector3 rightRayDirection = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;

            Gizmos.DrawRay(eyePos, leftRayDirection * viewDistance);
            Gizmos.DrawRay(eyePos, rightRayDirection * viewDistance);
        }
    }
}
