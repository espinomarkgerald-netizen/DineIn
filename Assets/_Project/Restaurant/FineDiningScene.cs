using UnityEngine.SceneManagement;

/// <summary>The Fine Dining restaurant scenes and their separate gameplay contexts.</summary>
public static class FineDiningScene
{
    public const string Career = "Lobby3";
    public const string Tutorial = "Lobby3Tutorial";
    public const string Multiplayer = "Lobby3 Multiplayer";
    public static bool Contains(Scene scene) => scene.IsValid() && Contains(scene.name);
    public static bool Contains(string name) => name == Career || name == Tutorial || name == Multiplayer;
}
