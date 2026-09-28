using UnityEngine;

public partial class GroupSpawner
{
    [Header("Normal campaign customer introductions / additional Fast Food visitors")]
    [SerializeField] private CustomerTypeProfile[] campaignCustomers = new CustomerTypeProfile[0];
    public bool NormalFastFood => gameObject.scene.name == "Lobby2" && !CampaignSaveStore.ProtectedSession;
    public System.Collections.Generic.IReadOnlyList<CustomerTypeProfile> CampaignCustomers => campaignCustomers;

    public CustomerTypeProfile ProfileFor(CustomerGroup.CustomerType type)
    {
        foreach (var profile in campaignCustomers)
            if (profile != null && profile.customerType == type) return profile;
        return null;
    }

    private bool IsAdditionalTypeEligible(CustomerTypeProfile profile) => NormalFastFood &&
        profile != null && (int)profile.customerType >= 3 && profile.customerPrefab != null &&
        (GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1) >= Mathf.Max(2, profile.unlockDay);

    public bool CanOfferCampaignEncounter(CustomerGroup.CustomerType type, float takeoutRoll)
    {
        if (!IsCustomerTypeEnabled(type)) return false;
        var restaurant = FastFoodRestaurant.For(this);
        if (restaurant == null) return (int)type < 3;
        var profile = (int)type >= 3 ? ProfileFor(type) : null;
        if (profile != null && profile.assistedPriorityService && !restaurant.CanAdmitAssistedCustomer) return false;
        bool takeaway = profile != null && profile.overrideTakeawayChance
            ? takeoutRoll < profile.takeawayChance : restaurant.ShouldSpawnTakeout(takeoutRoll);
        if (!takeaway && restaurant.LargestAvailableTableCapacity < (profile != null && profile.family ? 3 : 1)) return false;
        return profile == null || !profile.family || !Application.isMobilePlatform || mobileMaxGroupSize >= 3;
    }
}
