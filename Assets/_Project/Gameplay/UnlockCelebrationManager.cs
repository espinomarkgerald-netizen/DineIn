using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-400)]
public sealed class UnlockCelebrationManager : MonoBehaviour
{
    public static UnlockCelebrationManager Instance { get; private set; }

    private readonly Queue<UnlockPresentation> pending = new Queue<UnlockPresentation>();
    private readonly HashSet<string> queued = new HashSet<string>();
    private readonly HashSet<string> seen = new HashSet<string>();
    [Header("Presentation Scenes")]
    [Tooltip("Unlocks wait in the queue everywhere else and are only presented in these scenes.")]
    [SerializeField] private string[] presentationSceneNames = { "Lobby1" };

    private UnlockCelebrationUI view;
    private bool presenting;
    private bool hasActivePresentation;
    private UnlockPresentation activePresentation;
    private int lastScannedDay = -1;

    public static UnlockCelebrationManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;
        GameObject root = new GameObject("UnlockCelebrationManager");
        return root.AddComponent<UnlockCelebrationManager>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        UnlockManager.OnEquipmentUnlocked += QueueEquipment;
        UnlockManager.OnRecipeUnlocked += QueueRecipe;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        UnlockManager.OnEquipmentUnlocked -= QueueEquipment;
        UnlockManager.OnRecipeUnlocked -= QueueRecipe;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return WaitForInitialSaveLoad();
        HandlePresentationSceneReady();
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;
        data.seenUnlockCelebrationIDs.Clear();
        data.seenUnlockCelebrationIDs.AddRange(seen);
    }

    public void ApplySaveData(GameSaveData data)
    {
        seen.Clear();
        if (data?.seenUnlockCelebrationIDs == null)
            return;
        for (int i = 0; i < data.seenUnlockCelebrationIDs.Count; i++)
        {
            string id = data.seenUnlockCelebrationIDs[i];
            if (!string.IsNullOrWhiteSpace(id))
                seen.Add(id);
        }
        RemoveSeenFromPendingQueue();
        TryPresentNext();
    }

    public void QueueEquipment(string itemID)
    {
        Equipment equipment = FindEquipment(itemID);
        if (equipment == null)
            return;
        Queue(new UnlockPresentation(
            "equipment:" + itemID,
            equipment.displayName,
            string.IsNullOrWhiteSpace(equipment.description)
                ? "A new restaurant item is now available."
                : equipment.description,
            "Available in Computer  >  Equipment",
            equipment.sprite));
    }

    public void QueueRecipe(string recipeID)
    {
        Recipe recipe = FindRecipe(recipeID);
        if (recipe == null)
            return;
        Queue(new UnlockPresentation(
            "recipe:" + recipeID,
            recipe.recipeName,
            string.IsNullOrWhiteSpace(recipe.descriptionText)
                ? "A new menu item is now available."
                : recipe.descriptionText,
            "Available in Computer  >  Menu",
            recipe.sprite));
    }

    public void DebugReplayEquipment(string itemID)
    {
        seen.Remove("equipment:" + itemID);
        queued.Remove("equipment:" + itemID);
        QueueEquipment(itemID);
    }

    private void QueueCurrentDayUnlocks()
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        if (lastScannedDay == day)
            return;
        lastScannedDay = day;

        EquipmentManager equipment = EquipmentManager.Instance;
        if (equipment?.AllEquipment != null)
        {
            for (int i = 0; i < equipment.AllEquipment.Count; i++)
            {
                Equipment item = equipment.AllEquipment[i];
                if (item != null && item.dayToUnlock == day)
                    QueueEquipment(item.itemID);
            }
        }

        IReadOnlyList<Recipe> recipes = RecipeManager.AllRecipesStatic;
        if (recipes == null)
            return;
        for (int i = 0; i < recipes.Count; i++)
        {
            if (recipes[i] != null && recipes[i].dayToUnlock == day)
                QueueRecipe(recipes[i].recipeID);
        }
    }

    private void Queue(UnlockPresentation presentation)
    {
        if (string.IsNullOrWhiteSpace(presentation.id) || seen.Contains(presentation.id) ||
            !queued.Add(presentation.id))
            return;
        pending.Enqueue(presentation);
        TryPresentNext();
    }

    private void TryPresentNext()
    {
        if (!presenting && pending.Count > 0)
            StartCoroutine(PresentWhenReady());
    }

    private IEnumerator PresentWhenReady()
    {
        presenting = true;
        while (!CanPresentInCurrentScene() ||
               GameplayUIBlocker.IsBlocked() ||
               (GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive))
            yield return new WaitForSecondsRealtime(0.25f);

        if (view == null)
        {
            UnlockCelebrationUI prefab = Resources.Load<UnlockCelebrationUI>("UI/UnlockCelebrationUI");
            if (prefab != null)
            {
                view = Instantiate(prefab);
                DontDestroyOnLoad(view.gameObject);
            }
        }

        if (view == null)
        {
            Debug.LogWarning("[UnlockCelebration] Editable UI prefab is missing.");
            presenting = false;
            yield break;
        }

        UnlockPresentation presentation = pending.Dequeue();
        activePresentation = presentation;
        hasActivePresentation = true;
        view.Show(presentation, () =>
        {
            seen.Add(presentation.id);
            queued.Remove(presentation.id);
            GameSaveManager.Instance?.RequestSave();
            hasActivePresentation = false;
            presenting = false;
            TryPresentNext();
        });
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsPresentationScene(scene.name))
        {
            if (view != null && view.gameObject.activeSelf)
            {
                view.HideForSceneTransition();
                if (hasActivePresentation)
                    pending.Enqueue(activePresentation);
                hasActivePresentation = false;
                presenting = false;
                TryPresentNext();
            }
            return;
        }

        StartCoroutine(HandlePresentationSceneLoaded());
    }

    private IEnumerator HandlePresentationSceneLoaded()
    {
        yield return null;
        yield return WaitForInitialSaveLoad();
        HandlePresentationSceneReady();
    }

    private IEnumerator WaitForInitialSaveLoad()
    {
        while (GameSaveManager.Instance != null &&
               (!GameSaveManager.Instance.HasCompletedInitialLoad || GameSaveManager.Instance.IsApplyingSave))
            yield return null;
    }

    private void HandlePresentationSceneReady()
    {
        if (!CanPresentInCurrentScene())
            return;
        QueueCurrentDayUnlocks();
        TryPresentNext();
    }

    private bool CanPresentInCurrentScene()
    {
        Scene active = SceneManager.GetActiveScene();
        return active.IsValid() && active.isLoaded && IsPresentationScene(active.name);
    }

    private bool IsPresentationScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || presentationSceneNames == null)
            return false;
        for (int i = 0; i < presentationSceneNames.Length; i++)
        {
            if (string.Equals(presentationSceneNames[i], sceneName, System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private void RemoveSeenFromPendingQueue()
    {
        if (pending.Count == 0)
            return;

        int count = pending.Count;
        for (int i = 0; i < count; i++)
        {
            UnlockPresentation presentation = pending.Dequeue();
            if (seen.Contains(presentation.id))
            {
                queued.Remove(presentation.id);
                continue;
            }
            pending.Enqueue(presentation);
        }
    }

    private static Equipment FindEquipment(string itemID)
    {
        if (EquipmentManager.Instance?.AllEquipment == null)
            return null;
        for (int i = 0; i < EquipmentManager.Instance.AllEquipment.Count; i++)
        {
            Equipment item = EquipmentManager.Instance.AllEquipment[i];
            if (item != null && item.itemID == itemID)
                return item;
        }
        return null;
    }

    private static Recipe FindRecipe(string recipeID)
    {
        IReadOnlyList<Recipe> recipes = RecipeManager.AllRecipesStatic;
        if (recipes == null)
            return null;
        for (int i = 0; i < recipes.Count; i++)
        {
            if (recipes[i] != null && recipes[i].recipeID == recipeID)
                return recipes[i];
        }
        return null;
    }
}
