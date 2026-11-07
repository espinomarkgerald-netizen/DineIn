using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CustomerGreetBubbleUI : MonoBehaviour
{
    internal const float GreetingInteractRadius = 3.4f;
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;

    [Header("Sprites")]
    [SerializeField] private Sprite greetSprite;
    [SerializeField] private Sprite assignSprite;
    [SerializeField, Min(16)] private float partyTagFontSize=20;
    [SerializeField, Min(48)] private float partyTagMinimumHeight=78;

    private CustomerGroup group;
    private GameplayActionButtonSkin actionSkin;
    private bool seatingArtwork;
    private Sprite partyTagArtwork;

    public void Init(CustomerGroup g)
    {
        group = g;
        if (button == null) button = GetComponent<Button>();
        EnsureActionPresentation();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            PlayerTaskBubbleFocus.Bind(button.gameObject, group);
        }

        Refresh();
    }

    public void BindArtwork(Button action,TMP_Text caption,Image artwork,Sprite greet,Sprite seat)
    {button=action;label=caption;icon=artwork;greetSprite=greet;assignSprite=seat;}

    private void EnsureActionPresentation()
    {
        var art=GameplayActionButtonArt.Shared;if(art==null)return;
        partyTagArtwork=Resources.Load<CasualDiningPolishSettings>("CasualDining/CasualDiningPolishSettings")?.newspaperPartyTag;
        greetSprite=art.Find("IconGreetCustomer");assignSprite=art.Find("IconSeatCustomer");
        if(icon==null)icon=transform.Find("TableIcon")?.GetComponent<Image>();
        if(icon==null){var node=new GameObject("Reception icon",typeof(RectTransform),typeof(Image));node.transform.SetParent(transform,false);icon=node.GetComponent<Image>();}
        if(label==null){var node=new GameObject("Reception action",typeof(RectTransform),typeof(TextMeshProUGUI));node.transform.SetParent(transform,false);label=node.GetComponent<TMP_Text>();label.font=art.headingFont;label.raycastTarget=false;}
    }

    private void RefreshPartyTag()
    {
        if(group==null || string.IsNullOrEmpty(group.FineDiningReservationId))return;
        var tag=transform.Find("Details") as RectTransform;
        if(tag==null){tag=new GameObject("Details",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();tag.SetParent(transform,false);}
        var background=tag.GetComponent<Image>();background.enabled=true;background.raycastTarget=false;background.color=new Color(1,.97f,.9f);
        if(partyTagArtwork!=null || CustomerDrawerArt.Shared!=null){background.sprite=partyTagArtwork!=null?partyTagArtwork:CustomerDrawerArt.Shared.Find("PortraitTile");background.type=Image.Type.Sliced;background.pixelsPerUnitMultiplier=2;}
        var text=tag.GetComponentInChildren<TMP_Text>(true);
        if(text==null){var node=new GameObject("ReservationDetails",typeof(RectTransform),typeof(TextMeshProUGUI));node.transform.SetParent(tag,false);text=node.GetComponent<TMP_Text>();}
        var rect=(RectTransform)transform;float unit=Mathf.Max(.01f,rect.rect.height/96f);
        background.pixelsPerUnitMultiplier=2/unit;
        tag.anchorMin=tag.anchorMax=new Vector2(.5f,0);tag.pivot=new Vector2(.5f,1);tag.anchoredPosition=new Vector2(0,-8*unit);tag.sizeDelta=new Vector2(rect.rect.width,partyTagMinimumHeight*unit);
        text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(8,4)*unit;text.rectTransform.offsetMax=new Vector2(-8,-4)*unit;
        text.font=CustomerDrawerArt.Shared!=null?CustomerDrawerArt.Shared.bodyFont:GameplayActionButtonArt.Shared.bodyFont;text.fontSize=partyTagFontSize*unit;text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.06f,.14f,.25f);text.raycastTarget=false;
        text.text=group.ReceptionPartyCaption;tag.gameObject.SetActive(true);text.gameObject.SetActive(true);text.enabled=true;
        tag.sizeDelta=new Vector2(tag.sizeDelta.x,Mathf.Max(partyTagMinimumHeight*unit,text.GetPreferredValues(text.text,tag.rect.width-16*unit,0).y+8*unit));
        GetComponentInParent<UIFollowWorldPoint>()?.RefreshVisualBounds();
    }

    public void Refresh()
    {
        if (group == null) return;
        if (label == null || icon == null) return;

        if (!group.hasBeenGreeted)
        {
            label.text = "Greet Customer";
            icon.sprite = greetSprite;
        }
        else
        {
            label.text = "Seat Table";
            icon.sprite = assignSprite;
        }
        if (button != null && (actionSkin == null || seatingArtwork != group.hasBeenGreeted))
        {
            actionSkin = GameplayActionButtonSkin.Apply(button, string.Empty, label.text,
                group.hasBeenGreeted ? "Green" : "Cyan", customIcon: icon.sprite);
            actionSkin?.UseCaption(label);
            seatingArtwork = group.hasBeenGreeted;
            PlayerTaskBubbleFocus.Bind(button.gameObject, group)?.RefreshPresentation();
        }
        RefreshPartyTag();
    }

    private void OnClick()
    {
        if (group == null || !group.ReadyForReceptionInteraction) return;
        var session = MultiplayerSessionManager.Instance;
        if (session != null && session.IsMultiplayerSession)
        {
            var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
            var claims = session.GetComponent<MultiplayerTaskClaims>();
            var controller = session.LocalManager != null
                ? session.LocalManager.GetComponent<RoleBasedAssignController>() : null;
            if (customer == null || claims == null || controller == null) return;
            if (!claims.IsClaimedBy($"Customer:{customer.photonView.ViewID}:GreetSeat", session.LocalActorNumber))
            {
                MultiplayerCustomerInteractionBridge.TryHandle(controller, group, Camera.main);
                return;
            }
        }
        if (MultiplayerCustomerInteractionBridge.TryHandleGreetAction(group)) return;
        if (!TutorialCustomerFlowBridge.AllowsCustomerAction(group,
                group.hasBeenGreeted ? "Customer.SeatModeStarted" : "Customer.Greeted")) return;

        if (!group.hasBeenGreeted)
        {
            if (!RestaurantTaskClaim.TryClaimPlayer(group))
            {
                ShowClaimWarning();
                return;
            }

            if (!group.TryClaimReceptionForPlayer())
            {
                RestaurantTaskClaim.ReleasePlayer(group);
                ShowClaimWarning();
                return;
            }

            ManagerPlayer movementManager = ManagerPlayer.Active;
            if (movementManager != null && movementManager.Movement != null)
            {
                CustomerGroup targetGroup = group;
                Transform greetingStandPoint = FindClosestCustomer(
                    targetGroup,
                    movementManager.transform.position);

                if (greetingStandPoint == null)
                {
                    targetGroup.ReleasePlayerReceptionTask();
                    RestaurantTaskClaim.ReleasePlayer(targetGroup);
                    ShowClaimWarning();
                    return;
                }

                CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(targetGroup, false);

                movementManager.Movement.UI_MoveToAction(
                    greetingStandPoint,
                    GreetingInteractRadius,
                    () =>
                    {
                        if (targetGroup == null) return;

                        // Preserve the original, proven greet flow: mark the
                        // group, then refresh this same action into Seat Table.
                        targetGroup.MarkGreeted();
                        HostSpeechBubbleSpawner.Instance?.HideImmediate();
                        CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(targetGroup, true);
                        Debug.Log($"Greeted group: {targetGroup.name}");
                    },
                    () =>
                    {
                        if (targetGroup == null) return;
                        targetGroup.ReleasePlayerReceptionTask();
                        RestaurantTaskClaim.ReleasePlayer(targetGroup);
                        CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(targetGroup, true);
                    });
                return;
            }

            group.MarkGreeted();
            HostSpeechBubbleSpawner.Instance?.HideImmediate();
            Refresh();

            Debug.Log($"Greeted group: {group.name}");
            return;
        }

        if (!group.IsReceptionClaimedByPlayer)
        {
            // Recover safely if scripts were reloaded between Greet and Assign:
            // the greeted state lives on the group, so the same player can
            // re-establish ownership as long as the receptionist did not take it.
            if (!RestaurantTaskClaim.TryClaimPlayer(group) ||
                !group.TryClaimReceptionForPlayer())
            {
                RestaurantTaskClaim.ReleasePlayer(group);
                ShowClaimWarning();
                return;
            }
        }
        else if (!RestaurantTaskClaim.TryClaimPlayer(group))
        {
            ShowClaimWarning();
            return;
        }

        ManagerPlayer manager = ManagerPlayer.Active;
        if (manager != null && manager.Can(ManagerPlayer.Capability.Host))
        {
            RoleBasedAssignController managerController = manager.GetComponent<RoleBasedAssignController>();
            if (managerController != null)
            {
                managerController.BeginAssignFromBubble(group);
                HostSpeechBubbleSpawner.Instance?.HideImmediate();
                return;
            }
        }

        RoleBasedAssignController[] controllers = FindObjectsByType<RoleBasedAssignController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            var controller = controllers[i];
            if (controller == null) continue;

            var staffRole = controller.GetComponent<StaffRole>();
            if (staffRole == null) continue;
            if (staffRole.role != StaffRole.Role.Host) continue;
            if (RoleManager.Instance != null && !RoleManager.Instance.IsActiveRole(controller.gameObject)) continue;

            controller.BeginAssignFromBubble(group);
            HostSpeechBubbleSpawner.Instance?.HideImmediate();
            return;
        }

        Debug.LogWarning("No active host RoleBasedAssignController found for assign action.");
        group.ReleasePlayerReceptionTask();
        RestaurantTaskClaim.ReleasePlayer(group);
    }

    private void ShowClaimWarning()
    {
        WarningSlideUI.Instance?.Show(RestaurantTaskClaim.PlayerHasActiveTask
            ? "Finish your current task first."
            : "The receptionist is already helping this group.");
    }

    internal static Transform FindClosestCustomer(CustomerGroup targetGroup, Vector3 from)
    {
        if (targetGroup == null || targetGroup.members == null)
            return null;

        Transform closest = null;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < targetGroup.members.Count; i++)
        {
            CustomerAgent member = targetGroup.members[i];
            if (member == null || !member.gameObject.activeInHierarchy)
                continue;

            float distance = (member.transform.position - from).sqrMagnitude;
            if (distance >= closestDistance)
                continue;

            closest = member.transform;
            closestDistance = distance;
        }

        return closest;
    }
}
