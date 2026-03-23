using TMPro;
using UnityEngine;

public class TutorialPlaySessionManager : MonoBehaviour
{
    public static TutorialPlaySessionManager Instance { get; private set; }

    [Header("Session")]
    [SerializeField] private bool autoStartOnPlay = false;
    [SerializeField] private float durationSeconds = 240f;

    [Header("Targets")]
    [SerializeField] private int assignTarget = 3;
    [SerializeField] private int serveTarget = 3;
    [SerializeField] private int paymentTarget = 3;
    [SerializeField] private int cleanTarget = 3;

    [Header("Top UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text assignObjectiveText;
    [SerializeField] private TMP_Text serveObjectiveText;
    [SerializeField] private TMP_Text paymentObjectiveText;
    [SerializeField] private TMP_Text cleanObjectiveText;

    [Header("Result UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultBodyText;

    [Header("Optional")]
    [SerializeField] private GameObject gameplayHUDRoot;
    [SerializeField] private bool stopTimeWhenFinished = false;

    private float timeRemaining;
    private bool isRunning;
    private bool hasEnded;

    private int assignCount;
    private int serveCount;
    private int paymentCount;
    private int cleanCount;

    public bool IsRunning => isRunning;
    public bool HasEnded => hasEnded;

    public int AssignCount => assignCount;
    public int ServeCount => serveCount;
    public int PaymentCount => paymentCount;
    public int CleanCount => cleanCount;

    public int AssignTarget => assignTarget;
    public int ServeTarget => serveTarget;
    public int PaymentTarget => paymentTarget;
    public int CleanTarget => cleanTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    private void Start()
    {
        RefreshAllUI();

        if (autoStartOnPlay)
            StartSession();
    }

    private void Update()
    {
        if (!isRunning || hasEnded)
            return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining < 0f)
            timeRemaining = 0f;

        RefreshTimerUI();

        if (timeRemaining <= 0f)
            EndSession();
    }

    public void StartSession()
    {
        timeRemaining = durationSeconds;
        isRunning = true;
        hasEnded = false;

        assignCount = 0;
        serveCount = 0;
        paymentCount = 0;
        cleanCount = 0;

        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (gameplayHUDRoot != null)
            gameplayHUDRoot.SetActive(true);

        if (stopTimeWhenFinished)
            Time.timeScale = 1f;

        RefreshAllUI();

        Debug.Log("[TutorialPlaySessionManager] Session started.");
    }

    public void EndSession()
    {
        if (hasEnded)
            return;

        isRunning = false;
        hasEnded = true;

        if (stopTimeWhenFinished)
            Time.timeScale = 0f;

        string rating = CalculateRating();

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultTitleText != null)
            resultTitleText.text = "Tutorial Complete";

        if (resultBodyText != null)
        {
            resultBodyText.text =
                "Results\n\n" +
                $"Assigned Customers: {assignCount}/{assignTarget}\n" +
                $"Served Orders: {serveCount}/{serveTarget}\n" +
                $"Handled Payments: {paymentCount}/{paymentTarget}\n" +
                $"Cleaned Tables: {cleanCount}/{cleanTarget}\n\n" +
                $"Rating: {rating}";
        }

        Debug.Log("[TutorialPlaySessionManager] Session ended. Rating = " + rating);
    }

    public void RegisterAssign()
    {
        if (!CanRegister()) return;

        assignCount++;
        RefreshObjectivesUI();

        Debug.Log("[TutorialPlaySessionManager] Assign registered: " + assignCount);
    }

    public void RegisterServe()
    {
        if (!CanRegister()) return;

        serveCount++;
        RefreshObjectivesUI();

        Debug.Log("[TutorialPlaySessionManager] Serve registered: " + serveCount);
    }

    public void RegisterPayment()
    {
        if (!CanRegister()) return;

        paymentCount++;
        RefreshObjectivesUI();

        Debug.Log("[TutorialPlaySessionManager] Payment registered: " + paymentCount);
    }

    public void RegisterClean()
    {
        if (!CanRegister()) return;

        cleanCount++;
        RefreshObjectivesUI();

        Debug.Log("[TutorialPlaySessionManager] Clean registered: " + cleanCount);
    }

    private bool CanRegister()
    {
        return isRunning && !hasEnded;
    }

    private void RefreshAllUI()
    {
        RefreshTimerUI();
        RefreshObjectivesUI();
    }

    private void RefreshTimerUI()
    {
        if (timerText != null)
            timerText.text = FormatTime(timeRemaining);
    }

    private void RefreshObjectivesUI()
    {
        if (assignObjectiveText != null)
            assignObjectiveText.text = $"Assign Customers: {assignCount}/{assignTarget}";

        if (serveObjectiveText != null)
            serveObjectiveText.text = $"Serve Orders: {serveCount}/{serveTarget}";

        if (paymentObjectiveText != null)
            paymentObjectiveText.text = $"Handle Payments: {paymentCount}/{paymentTarget}";

        if (cleanObjectiveText != null)
            cleanObjectiveText.text = $"Clean Tables: {cleanCount}/{cleanTarget}";
    }

    private string FormatTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }

    private string CalculateRating()
    {
        int score = 0;

        if (assignCount >= assignTarget) score++;
        if (serveCount >= serveTarget) score++;
        if (paymentCount >= paymentTarget) score++;
        if (cleanCount >= cleanTarget) score++;

        switch (score)
        {
            case 4: return "S";
            case 3: return "A";
            case 2: return "B";
            case 1: return "C";
            default: return "D";
        }
    }
}