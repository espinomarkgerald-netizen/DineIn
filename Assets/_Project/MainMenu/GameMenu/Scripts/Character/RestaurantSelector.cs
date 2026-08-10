using System.Collections;
using System;
using UnityEngine;

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
    }

    /// <summary>
    /// Call this method from your '>' button to go to the next restaurant.
    /// </summary>
    public void NextRestaurant()
    {
        if (travelPoints.Length == 0) return;

        currentIndex = (currentIndex + 1) % travelPoints.Length;
        SaveCurrentSelection();
        MoveToCurrentPoint();
    }

    /// <summary>
    /// Call this method from your '<' button to go to the previous restaurant.
    /// </summary>
    public void PreviousRestaurant()
    {
        if (travelPoints.Length == 0) return;

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

        // Stop any ongoing movement so they don't stutter
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        moveCoroutine = StartCoroutine(SmoothMove(travelPoints[currentIndex].position));
    }

    private IEnumerator DestinationMove(Vector3 targetPosition)
    {
        // Kept for legacy reference, using SmoothMove below
        yield return null;
    }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        while (Vector3.Distance(character.position, targetPosition) > 0.01f)
        {
            character.position = Vector3.MoveTowards(character.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }

        character.position = targetPosition; // Snap exact final position
    }
}
