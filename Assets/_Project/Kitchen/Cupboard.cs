using UnityEngine;
using TMPro;
using System.Collections;

public class Cupboard : MonoBehaviour {
    public static Cupboard activeCupboard;

    public Transform standPoint;

    [Header("UI Setup")]
    public GameObject cupboardMenuPanel;

    [Header("UI Text Elements")]
    public TextMeshProUGUI bunsText;
    public TextMeshProUGUI meatText;
    public TextMeshProUGUI cheeseText;
    public TextMeshProUGUI chickenText;
    public TextMeshProUGUI friesText;

    // --- NEW: RESTOCK UI BUTTONS ---
    [Header("Restock Buttons")]
    public GameObject restockBunsBtn;
    public GameObject restockMeatBtn;
    public GameObject restockCheeseBtn;
    public GameObject restockChickenBtn;
    public GameObject restockFriesBtn;

    // --- NEW: BACKROOM LOCATIONS & BOX ---
    [Header("Restock System Options")]
    public Transform[] backroomPoints;
    public GameObject cardboardBoxPrefab;

    [Header("Out of Stock Feedback")]
    public TextMeshProUGUI feedbackText;
    public CanvasGroup feedbackCanvasGroup;
    private Coroutine activeFeedbackCoroutine;
    private Vector3 feedbackOriginalPos;

    [Header("Ingredient Prefabs")]
    public GameObject bunsPrefab;
    public GameObject rawMeatPrefab;
    public GameObject cheesePrefab;
    public GameObject rawChickenPrefab;
    public GameObject rawFriesPrefab;

    [Header("Max Limits (Used for Restocking)")]
    public int maxBuns = 5;
    public int maxMeat = 5;
    public int maxCheese = 5;
    public int maxChicken = 6;
    public int maxFries = 5;

    // The live counts
    private int bunsCount, meatCount, cheeseCount, chickenCount, friesCount;
    private PlayerHolding interactingPlayer;

    void Start() {
        if (cupboardMenuPanel != null) cupboardMenuPanel.SetActive(false);

        if (feedbackCanvasGroup != null) {
            feedbackCanvasGroup.alpha = 0f;
            feedbackOriginalPos = feedbackText.transform.localPosition;
        }

        // Set initial stock to the max
        bunsCount = maxBuns;
        meatCount = maxMeat;
        cheeseCount = maxCheese;
        chickenCount = maxChicken;
        friesCount = maxFries;
    }

    public void Interact(PlayerHolding player) {
        if (player.heldObject == null) {
            interactingPlayer = player;
            UpdateUI();
            cupboardMenuPanel.SetActive(true);
            activeCupboard = this;
        } else {
            Debug.Log("Hands are full! Drop your item before opening the cupboard.");
        }
    }

    // --- BUTTON TRIGGERS ---
    public void Button_SpawnBuns() {
        if (bunsCount > 0) { if (SpawnItem(bunsPrefab)) { bunsCount--; UpdateUI(); } } else { ShowFeedback("Out of Stock!"); }
    }
    public void Button_SpawnMeat() {
        if (meatCount > 0) { if (SpawnItem(rawMeatPrefab)) { meatCount--; UpdateUI(); } } else { ShowFeedback("Out of Stock!"); }
    }
    public void Button_SpawnCheese() {
        if (cheeseCount > 0) { if (SpawnItem(cheesePrefab)) { cheeseCount--; UpdateUI(); } } else { ShowFeedback("Out of Stock!"); }
    }
    public void Button_SpawnChicken() {
        if (chickenCount > 0) { if (SpawnItem(rawChickenPrefab)) { chickenCount--; UpdateUI(); } } else { ShowFeedback("Out of Stock!"); }
    }
    public void Button_SpawnFries() {
        if (friesCount > 0) { if (SpawnItem(rawFriesPrefab)) { friesCount--; UpdateUI(); } } else { ShowFeedback("Out of Stock!"); }
    }

    // --- NEW: RESTOCK TRIGGERS ---
    public void TriggerRestock_Buns() { StartCoroutine(PerformRestock("Buns", maxBuns)); }
    public void TriggerRestock_Meat() { StartCoroutine(PerformRestock("Meat", maxMeat)); }
    public void TriggerRestock_Cheese() { StartCoroutine(PerformRestock("Cheese", maxCheese)); }
    public void TriggerRestock_Chicken() { StartCoroutine(PerformRestock("Chicken", maxChicken)); }
    public void TriggerRestock_Fries() { StartCoroutine(PerformRestock("Fries", maxFries)); }


    public void CloseMenu() {
        if (cupboardMenuPanel != null) cupboardMenuPanel.SetActive(false);
        interactingPlayer = null;
        if (activeCupboard == this) activeCupboard = null;
    }

    private bool SpawnItem(GameObject prefab) {
        if (interactingPlayer == null || interactingPlayer.heldObject != null) return false;
        GameObject newItem = Instantiate(prefab);
        newItem.name = prefab.name;
        interactingPlayer.PickUp(newItem);
        CloseMenu();

        // --- ADD THIS ONE LINE HERE! ---
        PerformanceManager.AddIngredientUsed();

        return true;
    }

    private void UpdateUI() {
        if (bunsText != null) bunsText.text = "Buns (" + bunsCount + ")";
        if (meatText != null) meatText.text = "Meat (" + meatCount + ")";
        if (cheeseText != null) cheeseText.text = "Cheese (" + cheeseCount + ")";
        if (chickenText != null) chickenText.text = "Chicken (" + chickenCount + ")";
        if (friesText != null) friesText.text = "Fries (" + friesCount + ")";

        // Show restock buttons ONLY if the stock is exactly 0
        if (restockBunsBtn != null) restockBunsBtn.SetActive(bunsCount <= 0);
        if (restockMeatBtn != null) restockMeatBtn.SetActive(meatCount <= 0);
        if (restockCheeseBtn != null) restockCheeseBtn.SetActive(cheeseCount <= 0);
        if (restockChickenBtn != null) restockChickenBtn.SetActive(chickenCount <= 0);
        if (restockFriesBtn != null) restockFriesBtn.SetActive(friesCount <= 0);
    }

    // --- THE MASTER RESTOCK SEQUENCE ---
    private IEnumerator PerformRestock(string ingredient, int restockAmount) {
        if (backroomPoints == null || backroomPoints.Length == 0) {
            Debug.LogError("Dawg, you forgot to assign Backroom Points in the Inspector!");
            yield break;
        }

        // Save local references before we close the menu
        PlayerHolding player = interactingPlayer;
        KitchenPlayerMovement movement = player.GetComponent<KitchenPlayerMovement>();

        // 1. Close the menu & Lock the mouse!
        CloseMenu();
        movement.isBusy = true;

        // 2. Pick a random backroom point and walk there
        Transform targetPoint = backroomPoints[Random.Range(0, backroomPoints.Length)];
        movement.MoveToTarget(targetPoint.position);

        // 3. Wait until the player is standing in the backroom
        while (Vector3.Distance(player.transform.position, targetPoint.position) > 1.5f) {
            yield return null;
        }

        // 4. Give them the box and wait half a second
        if (cardboardBoxPrefab != null) {
            GameObject box = Instantiate(cardboardBoxPrefab);
            player.PickUp(box);
        }
        yield return new WaitForSeconds(0.5f);

        // 5. Walk back to the cupboard
        movement.MoveToTarget(standPoint.position);

        while (Vector3.Distance(player.transform.position, standPoint.position) > 1.5f) {
            yield return null;
        }

        // 6. Destroy the box
        if (player.heldObject != null) {
            GameObject boxToDestroy = player.heldObject;
            player.heldObject = null;
            Destroy(boxToDestroy);
        }

        // 7. Refill the stock
        if (ingredient == "Buns") bunsCount = restockAmount;
        else if (ingredient == "Meat") meatCount = restockAmount;
        else if (ingredient == "Cheese") cheeseCount = restockAmount;
        else if (ingredient == "Chicken") chickenCount = restockAmount;
        else if (ingredient == "Fries") friesCount = restockAmount;

        // 8. Pop up the feedback and unlock the mouse!
        ShowFeedback(ingredient + " Restocked!");
        movement.isBusy = false;
    }

    private void ShowFeedback(string message) {
        if (feedbackText == null || feedbackCanvasGroup == null) return;
        if (activeFeedbackCoroutine != null) StopCoroutine(activeFeedbackCoroutine);
        activeFeedbackCoroutine = StartCoroutine(AnimateFeedback(message));
    }

    private IEnumerator AnimateFeedback(string message) {
        feedbackText.text = message;
        feedbackCanvasGroup.alpha = 1f;
        feedbackText.transform.localPosition = feedbackOriginalPos;
        Vector3 endPos = feedbackOriginalPos + new Vector3(0, 50f, 0);
        float duration = 1.0f;
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;
            if (percent > 0.5f) feedbackCanvasGroup.alpha = 1f - ((percent - 0.5f) * 2f);
            feedbackText.transform.localPosition = Vector3.Lerp(feedbackOriginalPos, endPos, percent);
            yield return null;
        }
        feedbackCanvasGroup.alpha = 0f;
        feedbackText.transform.localPosition = feedbackOriginalPos;
    }
}