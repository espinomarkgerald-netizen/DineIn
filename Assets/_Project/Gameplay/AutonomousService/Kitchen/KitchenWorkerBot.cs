using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives a kitchen worker visual activity while real KitchenManager orders cook.
/// KitchenManager remains the authority for timing and tray creation; this class
/// only selects work points and delegates movement to AutonomousStaffBot.
/// </summary>
[DefaultExecutionOrder(11001)]
public class KitchenWorkerBot : MonoBehaviour
{
    [Header("Employee Assignment")]
    [Tooltip("Only the employee made Active for this role appears and works during the shift.")]
    [SerializeField] private EmployeeRole employeeRole = EmployeeRole.Chef;

    [Header("Stations")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private Transform[] workPoints;
    [SerializeField] private float waitAtPoint = 1.5f;
    [Tooltip("Only use a registered hygiene station when it is near this worker's authored work point.")]
    [SerializeField, Min(0f)] private float maximumStationDetour = 3f;

    [Header("Order Preparation Pose")]
    [SerializeField, Min(.3f)] private float preparationCycleSeconds = 1.1f;
    [SerializeField, Range(.15f, .5f)] private float workSurfaceHeightFraction = .3f;
    [SerializeField, Range(.1f, .45f)] private float workReachFraction = .28f;
    [SerializeField, Range(.04f, .2f)] private float handSpacingFraction = .1f;
    [Tooltip("Fast Food preparation products handled at this station. Assembler accepts every order.")]
    [SerializeField] private ItemTypeKitchen[] stationProducts;

    [Header("Optional Pianist Ambience")]
    [SerializeField] private AudioSource performanceAudio;
    [SerializeField] private ParticleSystem musicNotes;
    [SerializeField, Min(10)] private float reactionInterval = 40f;
    [SerializeField, Min(1)] private float reactionRadius = 35f;
    [SerializeField] private Transform[] performanceHands;
    [SerializeField, Range(0f,20f)] private float keyTapDegrees = 8f;
    private Quaternion[] handRest;
    private bool handsPerforming;
    private float nextMusicReaction;
    public bool IsPerforming => employeeRole == EmployeeRole.Pianist && isActiveAndEnabled && HasWork && homePoint != null && Vector3.Distance(transform.position, homePoint.position) < 1f;

    [Header("Navigation")]
    [SerializeField, Range(0, 99)] private int avoidancePriority = 60;

    private readonly HashSet<int> activeOrders = new HashSet<int>();
    private AutonomousStaffBot staffBot;
    private KitchenManager kitchenManager;
    private int currentIndex;
    private int hygieneStationIndex;
    private bool subscribed;
    private Animator workAnimator;
    private Avatar workAvatar;
    private Transform leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand;
    private float bodyHeight;

    public EmployeeRole EmployeeRole => employeeRole;

    private void Awake()
    {
        MultiplayerWorldRegistry.Track(this);
        staffBot = GetComponent<AutonomousStaffBot>();
        if (staffBot == null)
            staffBot = gameObject.AddComponent<AutonomousStaffBot>();

        staffBot.ConfigureHome(homePoint, avoidancePriority);
    }

    private void OnEnable()
    {
        ConfigureEmployeePerformance();
        BindKitchenManager();
    }

    private void ConfigureEmployeePerformance()
    {
        if (staffBot == null || EmployeeManager.Instance == null)
            return;
        staffBot.ConfigurePerformance(EmployeeManager.Instance.GetAssignedEmployee(employeeRole));
    }

    private void OnDisable()
    {
        RestorePerformanceHands();
        UnbindKitchenManager();
        activeOrders.Clear();
        if (performanceAudio != null) performanceAudio.Stop();
        if (musicNotes != null) musicNotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (employeeRole == EmployeeRole.Pianist)
        {
            if (musicNotes != null) { if(IsPerforming && !musicNotes.isPlaying)musicNotes.Play(); else if(!IsPerforming && musicNotes.isPlaying)musicNotes.Stop(); }
            if(IsPerforming && Time.time >= nextMusicReaction)
            {
                nextMusicReaction=Time.time+reactionInterval;
                var groups=FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None);
                foreach(var group in groups)
                {
                    if(group.gameObject.scene!=gameObject.scene || group.state!=CustomerGroup.GroupState.Eating || group.members.Count == 0 || group.members[0] == null || Vector3.Distance(group.members[0].transform.position,transform.position)>reactionRadius)continue;
                    if(group.members.Count>0 && group.members[0]!=null) group.members[0].PlayProceduralReaction(CustomerProceduralReaction.Positive);
                    break;
                }
            }
        }
        if (employeeRole == EmployeeRole.Pianist && performanceAudio != null)
        {
            bool performing = IsPerforming;
            if (performing && !performanceAudio.isPlaying) performanceAudio.Play();
            else if (!performing && performanceAudio.isPlaying) performanceAudio.Stop();
        }
        if (MultiplayerRestaurantBridge.IsObserver) return;
        if (!HasWork || staffBot == null || staffBot.IsBusy)
            return;

        staffBot.StartTask(WorkWhileOrdersAreActive());
    }

    private void LateUpdate()
    {
        if (employeeRole != EmployeeRole.Pianist && activeOrders.Count > 0 &&
            staffBot != null && staffBot.CurrentState == AutonomousStaffBot.StaffState.Working)
            ApplyPreparationPose();

        if(performanceHands==null || performanceHands.Length==0)return;
        if(IsPerforming)
        {
            if(!handsPerforming){handRest=new Quaternion[performanceHands.Length];for(int i=0;i<performanceHands.Length;i++)if(performanceHands[i]!=null)handRest[i]=performanceHands[i].localRotation;handsPerforming=true;}
            for(int i=0;i<performanceHands.Length;i++)if(performanceHands[i]!=null)performanceHands[i].localRotation=handRest[i]*Quaternion.Euler(Mathf.Sin(Time.time*5f+i*2f)*keyTapDegrees,0,0);
        }
        else RestorePerformanceHands();
    }
    private void ApplyPreparationPose()
    {
        if (workAnimator == null) workAnimator = GetComponentInChildren<Animator>(true);
        if (workAnimator == null || !workAnimator.isHuman) return;
        if (workAnimator.avatar != workAvatar || leftHand == null || rightHand == null)
        {
            workAvatar = workAnimator.avatar;
            leftUpper = workAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = workAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = workAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper = workAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = workAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = workAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            var appearance = GetComponent<DineIn.Appearance.CharacterAppearance>();
            bodyHeight = appearance != null && appearance.TryGetBodyBounds(out var bounds)
                ? Mathf.Clamp(bounds.size.y, .7f, 5f) : 3f;
        }
        if (leftHand == null || rightHand == null) return;

        float phase = Time.time * Mathf.PI * 2f / preparationCycleSeconds;
        Vector3 center = transform.position + Vector3.up * bodyHeight * workSurfaceHeightFraction
            + transform.forward * bodyHeight * workReachFraction;
        Vector3 leftTarget = center - transform.right * bodyHeight * handSpacingFraction
            + transform.forward * bodyHeight * (.035f * Mathf.Sin(phase));
        Vector3 rightTarget = center + transform.right * bodyHeight * handSpacingFraction
            + transform.forward * bodyHeight * (.035f * Mathf.Sin(phase + Mathf.PI));
        Quaternion leftRotation = leftHand.rotation, rightRotation = rightHand.rotation;
        AlienProceduralAnimation.ApplyCcdArm(leftUpper, leftLower, leftHand, leftTarget, 1f, 12);
        AlienProceduralAnimation.ApplyCcdArm(rightUpper, rightLower, rightHand, rightTarget, 1f, 12);
        leftHand.rotation = leftRotation;
        rightHand.rotation = rightRotation;
    }

    private void RestorePerformanceHands()
    {
        if(!handsPerforming)return;
        for(int i=0;i<performanceHands.Length;i++)if(performanceHands[i]!=null)performanceHands[i].localRotation=handRest[i];
        handsPerforming=false;
    }

    private bool HasWork => employeeRole == EmployeeRole.Pianist
        ? GameDayManager.Instance != null && GameDayManager.Instance.ShiftRunning
        : activeOrders.Count > 0 || HasCookingWork || HygieneManager.Instance?.State.Cleaning == true;

    private bool HasCookingWork
    {
        get
        {
            var cooking = FastFoodCookingController.Instance;
            if (cooking == null || !cooking.Active || !FastFoodScene.Contains(gameObject.scene)) return false;
            var mode = employeeRole == EmployeeRole.GrillStation ? FastFoodStationMode.Grill :
                employeeRole == EmployeeRole.FryStation ? FastFoodStationMode.Fry : FastFoodStationMode.Assembler;
            if (mode == FastFoodStationMode.Assembler) return cooking.State.Tickets.Exists(t => t.active && !t.submitted && !t.player);
            return cooking.State.Portions.Exists(p => !p.player && FastFoodCookingState.Station(p.recipe) == mode && p.stage != FastFoodCookingStage.Complete);
        }
    }

    private void BindKitchenManager()
    {
        if (subscribed || employeeRole == EmployeeRole.Pianist)
            return;

        kitchenManager = FindFirstObjectByType<KitchenManager>();
        if (kitchenManager == null)
        {
            Debug.LogError($"[KitchenWorkerBot] {name} could not find KitchenManager.", this);
            return;
        }

        kitchenManager.OrderStarted += HandleOrderStarted;
        kitchenManager.OrderFinished += HandleOrderFinished;
        kitchenManager.OrderForecastChanged += HandleForecastChanged;
        subscribed = true;
        var forecasts = new List<KitchenManager.OrderForecast>();
        kitchenManager.CopyActiveForecasts(forecasts);
        foreach (var forecast in forecasts) HandleForecastChanged(forecast);
    }

    private void UnbindKitchenManager()
    {
        if (subscribed && kitchenManager != null)
        {
            kitchenManager.OrderStarted -= HandleOrderStarted;
            kitchenManager.OrderFinished -= HandleOrderFinished;
            kitchenManager.OrderForecastChanged -= HandleForecastChanged;
        }

        subscribed = false;
        kitchenManager = null;
    }

    private void HandleOrderStarted(CustomerGroup group, int orderNumber)
    {
        if (FastFoodScene.Contains(gameObject.scene) && employeeRole != EmployeeRole.FastFoodAssembler)
        {
            if (group == null || group.currentOrder == null || stationProducts == null) return;
            bool matches = group.currentOrder.ResolveProducts().Exists(product => product != null &&
                (FastFoodCookingController.Instance != null && (employeeRole == EmployeeRole.GrillStation || employeeRole == EmployeeRole.FryStation)
                    ? FastFoodCookingState.Station(product) == (employeeRole == EmployeeRole.GrillStation ? FastFoodStationMode.Grill : FastFoodStationMode.Fry)
                    : System.Array.IndexOf(stationProducts, product.kitchenItemType) >= 0));
            if (!matches) return;
        }
        activeOrders.Add(orderNumber);
    }

    private void HandleOrderFinished(CustomerGroup group, int orderNumber, bool succeeded)
    {
        activeOrders.Remove(orderNumber);
    }

    private void HandleForecastChanged(KitchenManager.OrderForecast forecast)
    {
        // Takeout orders can be accepted while paused, then released without a new OrderStarted event.
        if (forecast.State == KitchenManager.ForecastState.Cooking && !forecast.IsPaused && !forecast.AwaitingSpawn)
            HandleOrderStarted(forecast.Group, forecast.OrderNumber);
        else
            activeOrders.Remove(forecast.OrderNumber);
    }

    private IEnumerator WorkWhileOrdersAreActive()
    {
        if (workPoints == null || workPoints.Length == 0 || staffBot == null)
            yield break;

        currentIndex = Mathf.Clamp(currentIndex, 0, workPoints.Length - 1);

        while (HasWork)
        {
            Transform target = FindNextWorkPoint();

            if (target == null)
            {
                Debug.LogError($"[KitchenWorkerBot] {name} has no valid work points.", this);
                yield break;
            }

            Vector3 approach = target.position;
            Transform equipment = null;
            bool registeredStation = employeeRole != EmployeeRole.Pianist && !FastFoodScene.Contains(gameObject.scene) && HygieneManager.Instance != null && HygieneManager.Instance.TryGetWorkStation(
                employeeRole, ref hygieneStationIndex, transform.position, out equipment, out approach);
            if (registeredStation && Vector3.Distance(approach, target.position) > maximumStationDetour)
                registeredStation = false;
            if (registeredStation) yield return staffBot.MoveWithin(approach, .25f, .5f, 12f);
            else yield return staffBot.MoveTo(target);

            if (staffBot.LastMoveSucceeded && HasWork)
            {
                yield return staffBot.FaceTowards(registeredStation && equipment != null
                    ? equipment.position : target.position + target.forward);
                bool wasCleaning = HygieneManager.Instance?.State.Cleaning == true;
                yield return staffBot.WorkFor(waitAtPoint);
                if (!wasCleaning && activeOrders.Count > 0) HygieneManager.Instance?.RecordKitchenUse(registeredStation ? equipment : target, autonomous: true);
            }
        }
    }

    private Transform FindNextWorkPoint()
    {
        for (int inspected = 0; inspected < workPoints.Length; inspected++)
        {
            Transform target = workPoints[currentIndex];
            currentIndex = (currentIndex + 1) % workPoints.Length;

            if (target != null)
                return target;
        }

        return null;
    }
}
