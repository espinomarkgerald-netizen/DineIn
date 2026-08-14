using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Guarantees Lobby1 has active navigation data in a standalone player. This
/// rebuilds the scene's authored NavMeshSurfaces once at load, then places any
/// already-created agents on the resulting mesh.
/// </summary>
public static class LobbyRuntimeNavMeshBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Application.isEditor || scene.name != "Lobby1")
            return;

        EnsureLobbyNavMesh();
    }

    /// <summary>
    /// Also called by LobbyAutonomousService's early Awake. The scene callback
    /// covers direct scene loads; the service call makes this happen before the
    /// character movement scripts start.
    /// </summary>
    public static void EnsureLobbyNavMesh()
    {
        if (Application.isEditor || SceneManager.GetActiveScene().name != "Lobby1")
            return;

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] != null && surfaces[i].isActiveAndEnabled)
                surfaces[i].BuildNavMesh();
        }

        NavMeshAgent[] agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent == null || !agent.enabled || agent.isOnNavMesh)
                continue;

            if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 100f, agent.areaMask))
                agent.Warp(hit.position);
        }

        Debug.Log($"[LobbyRuntimeNavMeshBootstrap] Built {surfaces.Length} Lobby1 NavMesh surface(s); active agents: {agents.Length}.");
    }
}
