using UnityEngine;

public class TutorialSceneWatcher : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorialManager;

    private bool traySeenForGroup;
    private bool moneySeenForGroup;

    private bool orderSubmittedReported;
    private bool billDeliveredReported;
    private bool paymentCollectedReported;
    private bool trayCleanedReported;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = GetComponent<TutorialManager>();

        if (tutorialManager == null)
            tutorialManager = TutorialManager.Instance;
    }

    private void Update()
    {
        if (tutorialManager == null)
            return;

        CustomerGroup group = tutorialManager.ActiveTutorialGroup;

        if (group == null)
        {
            if (tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.CollectPayment)
                tutorialManager.RegisterPaymentCollected(null);

            if (tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.CleanTray)
                tutorialManager.RegisterTrayCleaned(null);

            return;
        }

        FoodTray tray = FindFoodTrayForGroup(group);
        MoneyPickup money = FindMoneyPickupForGroup(group);

        bool hasTray = tray != null;
        bool hasMoney = money != null;

        if (hasTray)
            traySeenForGroup = true;

        if (hasMoney)
            moneySeenForGroup = true;

        // SubmitOrder -> ServeFood
        if (!orderSubmittedReported &&
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.SubmitOrder &&
            hasTray)
        {
            orderSubmittedReported = true;
            tutorialManager.RegisterOrderSubmitted(group);
        }

        // DeliverBill -> CollectPayment
        if (!billDeliveredReported &&
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.DeliverBill &&
            hasMoney)
        {
            billDeliveredReported = true;
            tutorialManager.RegisterBillDelivered(group);
        }

        // CollectPayment -> CleanTray
        if (!paymentCollectedReported &&
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.CollectPayment &&
            moneySeenForGroup && !hasMoney)
        {
            paymentCollectedReported = true;
            tutorialManager.RegisterPaymentCollected(group);
        }

        // CleanTray -> Complete
        if (!trayCleanedReported &&
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.CleanTray &&
            traySeenForGroup && !hasTray)
        {
            trayCleanedReported = true;
            tutorialManager.RegisterTrayCleaned(group);
        }
    }

    private FoodTray FindFoodTrayForGroup(CustomerGroup group)
    {
        if (group == null)
            return null;

        FoodTray[] all = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].Matches(group))
                return all[i];
        }

        return null;
    }

    private MoneyPickup FindMoneyPickupForGroup(CustomerGroup group)
    {
        if (group == null)
            return null;

        MoneyPickup[] all = FindObjectsByType<MoneyPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].Matches(group))
                return all[i];
        }

        return null;
    }

    public void ResetWatcher()
    {
        traySeenForGroup = false;
        moneySeenForGroup = false;

        orderSubmittedReported = false;
        billDeliveredReported = false;
        paymentCollectedReported = false;
        trayCleanedReported = false;
    }
}