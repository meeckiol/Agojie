using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Agojie.World
{
    /// <summary>
    /// Construit le NavMesh au lancement du niveau, sans package supplémentaire.
    /// Les ennemis (NavMeshAgent) en ont besoin pour marcher.
    /// Exclure les layers Player et Enemy, sinon leurs capsules font des trous dans le NavMesh.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(200f, 40f, 200f);
        [SerializeField] private LayerMask includedLayers = ~0;

        private NavMeshDataInstance instance;

        private void Awake()
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            Bounds bounds = new Bounds(transform.position, size);

            NavMeshBuilder.CollectSources(bounds, includedLayers, NavMeshCollectGeometry.PhysicsColliders,
                0, markups, sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds,
                Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);

            // Les agents créés avant le NavMesh doivent être reposés dessus.
            foreach (NavMeshAgent agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                if (agent.enabled) agent.Warp(agent.transform.position);
            }
        }

        private void OnDestroy()
        {
            instance.Remove();
        }
    }
}
