using UnityEngine;

[CreateAssetMenu(
    fileName = "CasualDiningPolishSettings",
    menuName = "Dine In/Casual Dining Polish Settings")]
public sealed class CasualDiningPolishSettings : ScriptableObject
{
    [Header("Supplier Market")]
    [Range(1, 5)] public int minimumDailyPriceChanges = 1;
    [Range(1, 5)] public int maximumDailyPriceChanges = 3;
    [Range(1, 50)] public int minimumPriceChangePercent = 5;
    [Range(1, 50)] public int maximumPriceChangePercent = 15;
    [Range(0f, 1f)] public float rareMarketEventChance = 0.1f;
    [Range(1, 50)] public int rarePriceChangePercent = 20;
    [Range(0.25f, 1f)] public float minimumPriceMultiplier = 0.6f;
    [Range(1f, 3f)] public float maximumPriceMultiplier = 1.6f;

    [Header("Restaurant Rating")]
    [Range(0f, 1f)] public float ratingSmoothing = 0.25f;
    [Range(1, 25)] public int maximumDailyRatingChange = 8;
    [Range(0, 100)] public int startingRatingScore = 60;

    [Header("Employees")]
    [Min(1)] public int baseExperiencePerShift = 10;
    [Min(10)] public int firstPromotionExperience = 150;
    [Min(0)] public int promotionExperienceGrowth = 75;
    [Range(0, 10)] public int statPointsPerPromotion = 3;
    [Min(1)] public int applicantRefreshDays = 7;

    [Header("Newspaper")]
    public string newspaperName = "THE GALACTIC GAZETTE";
    public string alienReporterName = "Xylo-7, Alien Correspondent";
    [Range(1, 15)] public int recentTemplateExclusionDays = 5;
    [Min(0.1f)] public float openingAnimationSeconds = 0.78f;

    [Header("Newspaper Audio")]
    [Tooltip("Optional authored clips. Quiet procedural paper sounds are used when these are empty.")]
    public AudioClip paperRustleSound;
    public AudioClip paperSlapSound;
    public AudioClip pageTurnSound;
    [Range(0f, 1f)] public float newspaperSoundVolume = 0.22f;
    public bool useProceduralPaperSoundsWhenClipsAreMissing = true;

    [TextArea(2, 5)]
    public string[] approvalHeadlines =
    {
        "ALIEN EYES REMAIN FIXED ON EARTH'S DINER",
        "HUMAN HOSPITALITY FACES ANOTHER GALACTIC REVIEW",
        "COSMIC DINERS DELIVER THEIR VERDICT",
        "EARTH RESTAURANT SHIFTS THE APPROVAL NEEDLE",
        "THE FLEET IS TALKING ABOUT YESTERDAY'S SERVICE",
        "DINER PERFORMANCE ECHOES ACROSS THE STARS"
    };

    [TextArea(2, 5)]
    public string[] positiveStories =
    {
        "The humans pleased {happy} visitors and kept the dining room moving. Alien approval now stands at {approval}%.",
        "A confident service pleased {happy} guests. The fleet records an approval reading of {approval}%.",
        "Earth's crew showed welcome improvement, earning smiles from {happy} alien diners. Approval is now {approval}%.",
        "The restaurant gave the fleet something pleasant to discuss: {happy} happy customers and {approval}% approval.",
        "Galactic tables reported a capable human crew, with {happy} visitors leaving happy and approval reaching {approval}%.",
        "Service signals were favorable across the fleet after {happy} satisfied guests pushed approval to {approval}%."
    };

    [TextArea(2, 5)]
    public string[] negativeStories =
    {
        "The fleet registered {angry} angry visitors after yesterday's service. Alien approval now stands at {approval}%.",
        "Patience wore thin for {angry} diners. Galactic observers reduced their confidence to {approval}% approval.",
        "Yesterday's operation left {angry} customers dissatisfied. The human restaurant now holds {approval}% approval.",
        "Alien reviewers found too many service cracks, with {angry} angry guests and approval at {approval}%.",
        "The fleet heard {angry} complaints from the dining room, leaving human approval at {approval}%.",
        "Galactic confidence weakened after {angry} guests left angry; approval now reads {approval}%."
    };

    [TextArea(2, 5)]
    public string[] neutralStories =
    {
        "The restaurant produced a mixed but recoverable day. Fleet approval currently reads {approval}%.",
        "Alien diners saw both strengths and weaknesses yesterday. Approval remains at {approval}%.",
        "The humans survived another inspection with an approval reading of {approval}%.",
        "Galactic opinion remains cautious as the restaurant enters a new preparation phase at {approval}% approval.",
        "The dining room produced neither triumph nor collapse, and fleet approval sits at {approval}%.",
        "Yesterday gave alien observers a balanced report, holding approval near {approval}%."
    };

    [TextArea(2, 5)]
    public string[] positiveCustomerQuotes =
    {
        "The plates arrived, the credits were counted correctly, and I would dock here again.",
        "These humans may be strange, but they understand a satisfying meal.",
        "Our table left smiling. Tell the kitchen to repeat that performance.",
        "Fast service and a warm booth—surprisingly civilized for Earth.",
        "The crew handled our order with care. I sent the location to my whole moon colony.",
        "A smooth meal from greeting to payment. The humans are learning.",
        "I expected chaos and received excellent service instead.",
        "The restaurant respected our time, our order, and our appetite.",
        "Our group left happier than when our saucer landed.",
        "Correct food, clean table, friendly crew—an easy recommendation.",
        "Yesterday's shift was the kind of Earth story aliens enjoy sharing.",
        "I would reserve the same booth on my next planetary visit.",
        "The meal arrived without drama, which is high praise for a human restaurant.",
        "A fine service cycle. Even our strictest hatchling approved.",
        "The staff worked like a coordinated starship crew."
    };

    [TextArea(2, 5)]
    public string[] negativeCustomerQuotes =
    {
        "I crossed three star systems and still could not get proper service.",
        "The humans need more hands, more stock, or a much better plan.",
        "My patience expired before the restaurant solved the problem.",
        "A diner should remember the meal, not the mistake.",
        "The queue moved slower than an asteroid with no thrusters.",
        "Our table received confusion when it should have received dinner.",
        "I left with credits in my pocket and disappointment in all three stomachs.",
        "The restaurant opened its doors before its crew was ready.",
        "One preventable mistake became the story of our entire visit.",
        "The humans noticed the problem only after our patience was gone.",
        "No alien should have to negotiate for basic service.",
        "I wanted a meal, not a live demonstration of poor preparation.",
        "The fleet deserves better than yesterday's disorder.",
        "Our group will wait for proof of improvement before returning.",
        "This diner needs stronger staffing and a much sharper plan."
    };

    [TextArea(2, 5)]
    public string[] neutralCustomerQuotes =
    {
        "The meal was acceptable, but the crew can still move faster.",
        "I might return after the humans make a few adjustments.",
        "Not a disaster, not a triumph—just another Earth dinner.",
        "The restaurant works, though the details still need attention.",
        "The crew completed the job, but nothing convinced me to celebrate.",
        "Our visit was ordinary by galactic standards and unusual only because humans served it.",
        "A few faster decisions would turn this acceptable diner into a good one.",
        "The meal passed inspection, while the service still has room to grow.",
        "I left satisfied enough, though not yet impressed.",
        "The humans are close to a better review if they fix the small delays.",
        "Nothing went terribly wrong, but the crew can aim higher.",
        "An adequate stop between star systems, with clear potential.",
        "The food did its job; the service should become more confident.",
        "Our group would consider another visit after a little polishing.",
        "A middle-of-the-orbit result: steady, safe, and forgettable."
    };

    [TextArea(2, 5)]
    public string[] positiveAdvice =
    {
        "Alien Boss advice: protect yesterday's strengths and prepare enough stock for the next crowd.",
        "Alien Boss advice: keep the same role coverage and look for one small improvement before opening.",
        "Alien Boss advice: maintain this service standard while watching costs and expiring ingredients.",
        "Alien Boss advice: repeat what worked, then use preparation time to remove the next bottleneck.",
        "Alien Boss advice: keep yesterday's discipline and prepare for a busier galactic crowd.",
        "Alien Boss advice: celebrate briefly, then verify staffing, stock, and prices before opening."
    };

    private void OnValidate()
    {
        minimumDailyPriceChanges = Mathf.Clamp(minimumDailyPriceChanges, 1, 5);
        maximumDailyPriceChanges = Mathf.Clamp(
            maximumDailyPriceChanges,
            minimumDailyPriceChanges,
            5);
        maximumPriceChangePercent = Mathf.Max(
            minimumPriceChangePercent,
            maximumPriceChangePercent);
        maximumPriceMultiplier = Mathf.Max(minimumPriceMultiplier, maximumPriceMultiplier);
        applicantRefreshDays = Mathf.Max(1, applicantRefreshDays);
        openingAnimationSeconds = Mathf.Max(0.1f, openingAnimationSeconds);
    }
}
