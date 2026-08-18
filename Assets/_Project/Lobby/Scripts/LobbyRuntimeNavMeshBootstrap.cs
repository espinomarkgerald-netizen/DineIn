/// <summary>
/// Compatibility shim retained for older callers. Lobby1 uses its authored,
/// serialized NavMesh data in both the Editor and player builds.
/// </summary>
public static class LobbyRuntimeNavMeshBootstrap
{
    /// <summary>Kept so older code can compile without triggering a runtime rebake.</summary>
    public static void EnsureLobbyNavMesh()
    {
        // Intentionally empty. Runtime rebuilding made builds use a different
        // navigation world from Editor Play Mode and baked rotated road/wall
        // surfaces as floors.
    }
}
