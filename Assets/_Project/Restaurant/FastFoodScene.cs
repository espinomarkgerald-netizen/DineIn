using UnityEngine.SceneManagement;

/// <summary>The authored fast-food scenes. Campaign save routing stays separate.</summary>
public static class FastFoodScene
{
    public const string Tutorial = "Lobby2Tutorial";
    public static bool Contains(Scene scene) => scene.IsValid() && Contains(scene.name);
    public static bool Contains(string name) => name == "Lobby2" || name == Tutorial;
}
