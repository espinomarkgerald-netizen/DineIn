using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class RestaurantSelector : MonoBehaviour
{
    private const string SelectedRestaurantKey = "GameMenu_SelectedRestaurantIndex";

    [Header("References")]
    [Tooltip("The character GameObject (Chef) that will move.")]
    public Transform character;
    
    [Tooltip("Array or list of travel points corresponding to each restaurant (Diner 1, Diner 2, Diner 3).")]
    public Transform[] travelPoints;

    [Header("Movement Settings")]
    [Tooltip("Speed at which the character moves to the travel point.")]
    public float moveSpeed = 10f;

    [Header("Game Menu presentation only")]
    [SerializeField] private bool animatePresentation;
    [SerializeField] private Animator menuAnimator;
    [SerializeField] private Camera menuCamera;
    [SerializeField, Min(30)] private float turnSpeed = 360f;
    [SerializeField] private float facingOffset;
    [SerializeField, Min(.01f)] private float poseBlendSeconds = .18f;
    [SerializeField] private AnimationClip idleClip, runClip, waveClip;
    [Tooltip("Optional existing humanoid clips. Empty slots keep idle; they never invent a gesture.")]
    [SerializeField] private AnimationClip bodyReaction, headReaction, faceReaction, applyReaction;
    private bool customizing;
    private PlayableGraph presentationGraph;
    private AnimationMixerPlayable poseMixer;
    private AnimationClipPlayable actionPlayable;
    private AnimationClip activeClip;
    private float actionRemaining, actionWeight;
    private bool continuousAction;

    [Header("Selection Persistence")]
    [Tooltip("Restores the restaurant the player last previewed when GameMenu opens again.")]
    [SerializeField] private bool restoreLastSelectedRestaurant = true;

    private int currentIndex = 0;
    private Coroutine moveCoroutine;

    /// <summary>Zero-based index for code: 0 = Restaurant 1, 1 = Restaurant 2, and so on.</summary>
    public int SelectedRestaurantIndex => currentIndex;

    /// <summary>Player-facing number: 1 = Restaurant 1, 2 = Restaurant 2, and so on.</summary>
    public int SelectedRestaurantNumber => currentIndex + 1;

    /// <summary>Raised whenever the player changes restaurant through the next/previous buttons.</summary>
    public event Action<int> OnRestaurantSelected;

    void Start()
    {
        if (restoreLastSelectedRestaurant && travelPoints.Length > 0)
        {
            int savedIndex = PlayerPrefs.GetInt(SelectedRestaurantKey, 0);
            currentIndex = Mathf.Clamp(savedIndex, 0, travelPoints.Length - 1);
        }

        // Snap to the saved/initial restaurant position at start.
        if (travelPoints.Length > 0 && character != null)
        {
            character.position = travelPoints[currentIndex].position;
        }
        if (animatePresentation && character != null) moveCoroutine = StartCoroutine(FaceCamera());
    }

    private void Update()
    {
        if (!presentationGraph.IsValid()) return;
        if (!continuousAction) actionRemaining = Mathf.Max(0, actionRemaining - Time.unscaledDeltaTime);
        float target = continuousAction || actionRemaining > 0 ? 1 : 0;
        actionWeight = Mathf.MoveTowards(actionWeight, target, Time.unscaledDeltaTime / Mathf.Max(.01f, poseBlendSeconds));
        poseMixer.SetInputWeight(0, 1 - actionWeight); poseMixer.SetInputWeight(1, actionWeight);
    }

    private void EnsurePresentation()
    {
        if (!animatePresentation || menuAnimator == null || idleClip == null || presentationGraph.IsValid()) return;
        presentationGraph = PlayableGraph.Create("Game Menu Character");
        presentationGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        poseMixer = AnimationMixerPlayable.Create(presentationGraph, 2);
        var idle = AnimationClipPlayable.Create(presentationGraph, idleClip);
        presentationGraph.Connect(idle, 0, poseMixer, 0); poseMixer.SetInputWeight(0, 1);
        var output = AnimationPlayableOutput.Create(presentationGraph, "Menu Pose", menuAnimator);
        output.SetSourcePlayable(poseMixer); presentationGraph.Play();
    }

    private void Pose(AnimationClip clip, bool loop = false)
    {
        EnsurePresentation();
        if (!presentationGraph.IsValid()) return;
        if (clip == null) { continuousAction = false; actionRemaining = 0; return; }
        if (activeClip != clip || !actionPlayable.IsValid())
        {
            if (actionPlayable.IsValid()) { poseMixer.DisconnectInput(1); presentationGraph.DestroyPlayable(actionPlayable); }
            actionPlayable = AnimationClipPlayable.Create(presentationGraph, clip);
            presentationGraph.Connect(actionPlayable, 0, poseMixer, 1); activeClip = clip;
        }
        actionPlayable.SetTime(0); continuousAction = loop;
        actionRemaining = Mathf.Max(0, clip.length - poseBlendSeconds);
    }

    private void CancelPresentation()
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = null; Pose(null);
    }

    public void BeginCustomization()
    {
        customizing = true; CancelPresentation();
        if (animatePresentation) moveCoroutine = StartCoroutine(FaceCamera());
    }
    public void InterruptPreviewPose() { if (customizing) CancelPresentation(); }
    public void ReactToSelection(int category)
    {
        if (!customizing) return;
        // Rotation is deliberately untouched: only opening or an explicit drag owns it.
        Pose(category == 2 ? faceReaction : category == 3 || category == 4 || category == 6 ? headReaction : bodyReaction);
    }
    public void EndCustomization(bool applied)
    {
        customizing = false; CancelPresentation();
        if (character == null) return;
        if (travelPoints.Length > currentIndex && travelPoints[currentIndex] != null && Vector3.Distance(character.position, travelPoints[currentIndex].position) > .01f)
            MoveToCurrentPoint();
        else if (animatePresentation) moveCoroutine = StartCoroutine(FaceCamera(applied ? applyReaction : null));
    }

    private IEnumerator FaceCamera(AnimationClip arrival = null)
    {
        Pose(null);
        if (menuCamera != null)
        {
            var direction = -menuCamera.transform.forward; direction.y = 0;
            if (direction.sqrMagnitude > .001f) yield return TurnTo(Quaternion.LookRotation(direction) * Quaternion.Euler(0, facingOffset, 0));
        }
        Pose(arrival); moveCoroutine = null;
    }
    private IEnumerator TurnTo(Quaternion rotation)
    {
        while (Quaternion.Angle(character.rotation, rotation) > .5f)
        {
            character.rotation = Quaternion.RotateTowards(character.rotation, rotation, Mathf.Max(30, turnSpeed) * Time.unscaledDeltaTime);
            yield return null;
        }
        character.rotation = rotation;
    }
    private void OnDisable()
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = null; customizing = false;
        if (presentationGraph.IsValid()) presentationGraph.Destroy();
        activeClip = null; actionWeight = actionRemaining = 0; continuousAction = false;
    }

    /// <summary>
    /// Call this method from your '>' button to go to the next restaurant.
    /// </summary>
    public void NextRestaurant()
    {
        if (customizing || travelPoints.Length == 0) return;

        currentIndex = (currentIndex + 1) % travelPoints.Length;
        SaveCurrentSelection();
        MoveToCurrentPoint();
    }

    /// <summary>
    /// Call this method from your '<' button to go to the previous restaurant.
    /// </summary>
    public void PreviousRestaurant()
    {
        if (customizing || travelPoints.Length == 0) return;

        currentIndex = (currentIndex - 1 + travelPoints.Length) % travelPoints.Length;
        SaveCurrentSelection();
        MoveToCurrentPoint();
    }

    private void SaveCurrentSelection()
    {
        PlayerPrefs.SetInt(SelectedRestaurantKey, currentIndex);
        PlayerPrefs.Save();
        OnRestaurantSelected?.Invoke(currentIndex);
    }

    private void MoveToCurrentPoint()
    {
        if (character == null || travelPoints[currentIndex] == null) return;

        CancelPresentation();

        moveCoroutine = StartCoroutine(SmoothMove(travelPoints[currentIndex].position));
    }

    private IEnumerator DestinationMove(Vector3 targetPosition)
    {
        // Kept for legacy reference, using SmoothMove below
        yield return null;
    }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        if (animatePresentation)
        {
            var direction = targetPosition - character.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) yield return TurnTo(Quaternion.LookRotation(direction) * Quaternion.Euler(0, facingOffset, 0));
            Pose(runClip, true);
        }
        while (Vector3.Distance(character.position, targetPosition) > 0.01f)
        {
            character.position = Vector3.MoveTowards(character.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }

        character.position = targetPosition; // Snap exact final position
        if (animatePresentation) yield return FaceCamera(waveClip);
        moveCoroutine = null;
    }
}
