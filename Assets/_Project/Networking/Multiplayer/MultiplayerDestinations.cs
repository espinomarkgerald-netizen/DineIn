// Supported multiplayer destinations and their existing result contracts.
public static class MultiplayerDestinations
{
    public const string RestaurantKey = "RestaurantType", SceneKey = "restaurant.scene";
    public const string CasualScene = "Lobby1 Multiplayer", FastFoodScene = "Lobby2 Multiplayer";
    public const string CasualRules = "casual-session-2", FastFoodRules = "fastfood-session-2";

    public static bool TryGet(string restaurant, out string sceneName, out string rulesVersion)
    {
        sceneName = rulesVersion = null;
        if (restaurant == "CasualDining") { sceneName = CasualScene; rulesVersion = CasualRules; return true; }
        if (restaurant == "FastFood") { sceneName = FastFoodScene; rulesVersion = FastFoodRules; return true; }
        return false;
    }

    public static bool Matches(string restaurant, string sceneName, string rulesVersion) =>
        TryGet(restaurant, out var expectedScene, out var expectedRules)
        && sceneName == expectedScene && rulesVersion == expectedRules;
}
