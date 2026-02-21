using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class PlayfabMenuUIBinder : MonoBehaviour
{
    [Header("UI - Shared")]
    public TMP_Text messageText;

    [Header("LOGIN")]
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;
    public Button loginButton;

    [Header("REGISTER")]
    public TMP_InputField registerUsernameInput;
    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public Button registerButton;

    [Header("CUSTOMIZATION")]
    public Button saveCustomizationButton;

    [Header("ACCOUNT UI (After Login)")]
    public GameObject loginPanel;
    public GameObject registerPanel;
    public GameObject accountPanel;

    public TMP_Text accountUsernameText;
    public TMP_Text accountPlayFabIdText;
    public Button signOutButton;

    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "MainMenu";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryBind(); // bind when this object becomes active again
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == menuSceneName)
            TryBind();
    }

    private void TryBind()
    {
        if (PlayfabManager.Instance == null)
        {
            Debug.LogWarning("[PlayfabMenuUIBinder] PlayfabManager.Instance is null (not ready yet).");
            return;
        }

        PlayfabManager.Instance.BindMenuUI(
            messageText,
            loginUsernameInput,
            loginPasswordInput,
            loginButton,
            registerUsernameInput,
            registerEmailInput,
            registerPasswordInput,
            registerButton,
            saveCustomizationButton,
            loginPanel,
            registerPanel,
            accountPanel,
            accountUsernameText,
            accountPlayFabIdText,
            signOutButton
        );

        Debug.Log("[PlayfabMenuUIBinder] ✅ Bound UI to PlayfabManager.");
    }
}
