using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Editable portrait-style card used by the HR department rails.</summary>
public sealed class ManagementEmployeeCardUI : MonoBehaviour
{
    [SerializeField] private Image accent;
    [SerializeField] private Image avatarBackground;
    [SerializeField] private TMP_Text avatarInitial;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text starsText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text proText;
    [SerializeField] private TMP_Text conText;
    [SerializeField] private TMP_Text salaryText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TMP_Text secondaryLabel;

    public EmployeeData Employee { get; private set; }
    public Button PrimaryButton => primaryButton;
    public Button SecondaryButton => secondaryButton;

    public void ConfigureReferences(
        Image configuredAccent,
        Image configuredAvatarBackground,
        TMP_Text configuredAvatarInitial,
        TMP_Text configuredName,
        TMP_Text configuredRole,
        TMP_Text configuredStars,
        TMP_Text configuredStats,
        TMP_Text configuredPro,
        TMP_Text configuredCon,
        TMP_Text configuredSalary,
        TMP_Text configuredStatus,
        Button configuredPrimary,
        TMP_Text configuredPrimaryLabel,
        Button configuredSecondary,
        TMP_Text configuredSecondaryLabel)
    {
        accent = configuredAccent;
        avatarBackground = configuredAvatarBackground;
        avatarInitial = configuredAvatarInitial;
        nameText = configuredName;
        roleText = configuredRole;
        starsText = configuredStars;
        statsText = configuredStats;
        proText = configuredPro;
        conText = configuredCon;
        salaryText = configuredSalary;
        statusText = configuredStatus;
        primaryButton = configuredPrimary;
        primaryLabel = configuredPrimaryLabel;
        secondaryButton = configuredSecondary;
        secondaryLabel = configuredSecondaryLabel;
    }

    public void Bind(
        EmployeeData employee,
        SalaryConfig salaryConfig,
        string status,
        string primaryAction,
        UnityAction onPrimary,
        bool primaryEnabled,
        string secondaryAction,
        UnityAction onSecondary,
        bool secondaryEnabled)
    {
        Employee = employee;
        string employeeName = employee != null ? employee.employeeName : "Empty Slot";
        EmployeeRole role = employee != null ? employee.role : EmployeeRole.Host;

        if (nameText != null) nameText.text = employeeName;
        if (roleText != null) roleText.text = employee != null ? role.ToString().ToUpperInvariant() : "AVAILABLE";
        if (avatarInitial != null)
            avatarInitial.text = employee != null && !string.IsNullOrWhiteSpace(employeeName)
                ? employeeName.Substring(0, 1).ToUpperInvariant()
                : "+";
        if (starsText != null)
            starsText.text = employee != null
                ? new string('★', Mathf.Clamp(employee.stars, 1, 5)) + new string('☆', 5 - Mathf.Clamp(employee.stars, 1, 5))
                : "☆☆☆☆☆";
        if (statsText != null)
            statsText.text = employee != null
                ? $"SPEED  {employee.speed}%\nACCURACY  {employee.accuracy}%\nRELIABILITY  {employee.reliability}%"
                : "Hire an applicant to fill this role.";
        if (proText != null) proText.text = employee != null ? "+ " + employee.GetPrimaryPro() : "+ Open position";
        if (conText != null) conText.text = employee != null ? "− " + employee.GetPrimaryCon() : string.Empty;
        if (salaryText != null)
            salaryText.text = employee != null && salaryConfig != null
                ? "₱" + employee.GetSalary(salaryConfig) + " / DAY"
                : "NO PAYROLL";
        if (statusText != null) statusText.text = status ?? string.Empty;

        Color roleColor = GetRoleColor(role);
        if (accent != null) accent.color = employee != null ? roleColor : new Color(0.52f, 0.58f, 0.65f);
        if (avatarBackground != null) avatarBackground.color = employee != null
            ? Color.Lerp(roleColor, Color.white, 0.52f)
            : new Color(0.82f, 0.85f, 0.89f);

        ConfigureButton(primaryButton, primaryLabel, primaryAction, onPrimary, primaryEnabled);
        ConfigureButton(secondaryButton, secondaryLabel, secondaryAction, onSecondary, secondaryEnabled);
    }

    private static void ConfigureButton(
        Button button,
        TMP_Text label,
        string action,
        UnityAction callback,
        bool enabled)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        if (callback != null)
            button.onClick.AddListener(callback);
        button.interactable = enabled && callback != null;
        button.gameObject.SetActive(!string.IsNullOrWhiteSpace(action));
        if (label != null) label.text = action ?? string.Empty;
    }

    private static Color GetRoleColor(EmployeeRole role)
    {
        switch (role)
        {
            case EmployeeRole.Host: return new Color(0.20f, 0.64f, 0.88f);
            case EmployeeRole.Waiter: return new Color(0.25f, 0.72f, 0.56f);
            case EmployeeRole.Cashier: return new Color(0.95f, 0.62f, 0.20f);
            case EmployeeRole.Busser: return new Color(0.62f, 0.48f, 0.86f);
            case EmployeeRole.Chef: return new Color(0.91f, 0.34f, 0.28f);
            case EmployeeRole.Barista: return new Color(0.56f, 0.34f, 0.20f);
            default: return new Color(0.22f, 0.55f, 0.78f);
        }
    }
}
