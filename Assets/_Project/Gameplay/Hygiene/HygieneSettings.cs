using UnityEngine;

// The shipped Resources asset is editable before Play Mode and shared by restaurants.
[CreateAssetMenu(menuName = "Dine In/Hygiene Settings")]
public sealed class HygieneSettings : ScriptableObject
{
    [Header("Overall dirt speed")]
    [Tooltip("Applies to all dirt sources. 1 = current balance, 0.5 = half as fast, 0 = no new dirt.")]
    [Range(0f, 5f)] public float dirtSpeed = 1f;
    [Header("Kitchen")]
    [Range(0f, 5f)] public float kitchenDirtMultiplier = 1f;
    [Range(0f, .2f)] public float dirtPerStationUse = .05f;
    [Header("Lobby surfaces and incidents")]
    [Range(0f, 2f)] public float lobbyDirtMultiplier = .35f;
    [Range(0f, .2f)] public float dirtPerLobbyIncident = .08f;
    [Range(0f, .2f)] public float seatingDirt = .04f;
    [Range(0f, .2f)] public float orderDirt = .025f;
    [Range(0f, .2f)] public float mealStartDirt = .08f;
    [Range(0f, .2f)] public float billDirt = .025f;
    [Tooltip("Dirt per diner per half-second eating sample.")]
    [Range(0f, .02f)] public float eatingDirt = .0015f;
    [Tooltip("Floor spill dirt per half-second eating sample.")]
    [Range(0f, .02f)] public float eatingSpillDirt = .003f;
    [Header("Walking marks")]
    [Range(0f, .1f)] public float walkingDirt = .0125f;
    [Tooltip("Metres between marks. Higher values produce fewer marks.")]
    [Range(.3f, 2f)] public float walkingSpacing = .75f;
    [Tooltip("Accumulated dirt required before floor marks start to appear.")]
    [Range(0f, .95f)] public float visibleFloorDirt = .1f;
    [Header("Cleaning (seconds of active service)")]
    [Range(.1f, 3600f)] public float immediateCleanSeconds = 5f;
    [Range(.1f, 3600f)] public float whileCookingCleanSeconds = 20f;
    [Range(.1f, 3600f)] public float forcedCleanSeconds = 30f;
    [Tooltip("1.1 means cooking takes 10% longer during cleaning.")]
    [Range(1f, 3f)] public float cleaningCookMultiplier = 1.1f;
    [Tooltip("Cleaning work only; walking to dirty areas adds travel time.")]
    [Range(.1f, 3600f)] public float lobbyCleanSeconds = 10f;
    [Range(.1f, 2f)] public float mopRadius = .8f;
    [Range(0f, 3600f)] public float boothShineSeconds = 90f;
    [Header("Post-cleaning protection")]
    [Tooltip("In-game hours without new lobby dirt after a fully successful floor pass.")]
    [Range(0f, 24f)] public float lobbyGraceHours = 2f;

    private static HygieneSettings current;
    public static HygieneSettings Current
    {
        get
        {
            if (current != null) return current;
            current = Resources.Load<HygieneSettings>("Hygiene/HygieneSettings");
            if (current == null)
            {
                Debug.LogError("[Hygiene] Missing Hygiene/HygieneSettings asset; using default tuning.");
                current = CreateInstance<HygieneSettings>();
                current.hideFlags = HideFlags.HideAndDontSave;
            }
            return current;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => current = null;
}
