using System;
using UnityEngine;

/// <summary>
/// Controls the Campaign/Multiplayer choice popup in GameMenu.
/// It deliberately does not load a scene yet: the menu project is separate
/// from the gameplay project. A later handoff system will consume SelectedMode.
/// </summary>
public class GameModePopupController : MonoBehaviour
{
    private const string SelectedGameModeKey = "GameMenu_SelectedGameMode";

    public enum GameModeChoice
    {
        None,
        Campaign,
        Multiplayer
    }

    [Header("UI")]
    [Tooltip("The disabled GamemodePopupUI object under GameCanvas.")]
    [SerializeField] private GameObject gamemodePopupUI;

    [Header("Restaurant Selection")]
    [Tooltip("Usually found automatically as a child of GameManager. Assign it manually only if your hierarchy changes.")]
    [SerializeField] private RestaurantSelector restaurantSelector;

    /// <summary>The most recent mode chosen during this GameMenu session.</summary>
    public GameModeChoice SelectedMode { get; private set; } = GameModeChoice.None;

    /// <summary>The currently highlighted restaurant. 0 = Restaurant 1.</summary>
    public int SelectedRestaurantIndex => restaurantSelector != null ? restaurantSelector.SelectedRestaurantIndex : 0;

    /// <summary>The currently highlighted restaurant using its player-facing number.</summary>
    public int SelectedRestaurantNumber => restaurantSelector != null ? restaurantSelector.SelectedRestaurantNumber : 1;

    /// <summary>Future systems can subscribe to this when they need the selected mode.</summary>
    public event Action<GameModeChoice> OnModeSelected;

    private void Awake()
    {
        if (restaurantSelector == null)
            restaurantSelector = GetComponentInChildren<RestaurantSelector>();

        int savedMode = PlayerPrefs.GetInt(SelectedGameModeKey, (int)GameModeChoice.None);
        SelectedMode = Enum.IsDefined(typeof(GameModeChoice), savedMode)
            ? (GameModeChoice)savedMode
            : GameModeChoice.None;

        HidePopup();
    }

    /// <summary>Wire this to the existing Restaurant Play button.</summary>
    public void ShowPopup()
    {
        if (gamemodePopupUI == null)
        {
            Debug.LogError("[GameModePopupController] Assign GamemodePopupUI in the Inspector.");
            return;
        }

        gamemodePopupUI.SetActive(true);
    }

    /// <summary>Wire this to CancelButton.</summary>
    public void HidePopup()
    {
        if (gamemodePopupUI != null)
            gamemodePopupUI.SetActive(false);
    }

    /// <summary>Wire this to CampaignButton.</summary>
    public void ChooseCampaign()
    {
        ChooseMode(GameModeChoice.Campaign);
    }

    /// <summary>Wire this to MultiplayerButton.</summary>
    public void ChooseMultiplayer()
    {
        ChooseMode(GameModeChoice.Multiplayer);
    }

    private void ChooseMode(GameModeChoice choice)
    {
        SelectedMode = choice;
        PlayerPrefs.SetInt(SelectedGameModeKey, (int)choice);
        PlayerPrefs.Save();

        Debug.Log($"[GameModePopupController] Restaurant {SelectedRestaurantNumber} + {choice} selected.");
        OnModeSelected?.Invoke(choice);
        HidePopup();
    }
}
