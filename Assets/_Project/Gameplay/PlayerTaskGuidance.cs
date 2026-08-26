using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerTaskCategory
{
    None,
    Service,
    Restock
}

public readonly struct PlayerTaskView
{
    public readonly string Source;
    public readonly string Key;
    public readonly string Action;
    public readonly string Detail;
    public readonly int Priority;
    public readonly UnityEngine.Object Target;
    public readonly PlayerTaskCategory Category;

    public bool IsValid => !string.IsNullOrWhiteSpace(Key) &&
                           !string.IsNullOrWhiteSpace(Action);

    public PlayerTaskView(
        string source,
        string key,
        string action,
        string detail,
        int priority,
        UnityEngine.Object target,
        PlayerTaskCategory category)
    {
        Source = source;
        Key = key;
        Action = action;
        Detail = detail;
        Priority = priority;
        Target = target;
        Category = category;
    }
}

/// <summary>
/// Small presentation broker for the single task shown to the player. Gameplay
/// ownership remains in RestaurantTaskClaim and the restock ledger; this class
/// only selects the highest-priority description for the shared task button.
/// </summary>
public static class PlayerTaskGuidance
{
    private static readonly Dictionary<string, PlayerTaskView> Tasks =
        new Dictionary<string, PlayerTaskView>();

    private static PlayerTaskView current;

    public static event Action Changed;

    public static PlayerTaskView Current => current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Tasks.Clear();
        current = default;
        Changed = null;
    }

    public static void SetTask(
        string source,
        string key,
        string action,
        string detail,
        int priority,
        UnityEngine.Object target,
        PlayerTaskCategory category)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(action))
        {
            ClearTask(source);
            return;
        }

        Tasks[source] = new PlayerTaskView(
            source,
            key.Trim(),
            action.Trim(),
            string.IsNullOrWhiteSpace(detail) ? string.Empty : detail.Trim(),
            priority,
            target,
            category);
        SelectCurrent();
    }

    public static void ClearTask(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || !Tasks.Remove(source))
            return;

        SelectCurrent();
    }

    private static void SelectCurrent()
    {
        PlayerTaskView next = default;
        foreach (KeyValuePair<string, PlayerTaskView> pair in Tasks)
        {
            PlayerTaskView candidate = pair.Value;
            if (!candidate.IsValid)
                continue;

            if (!next.IsValid || candidate.Priority > next.Priority ||
                (candidate.Priority == next.Priority &&
                 string.CompareOrdinal(candidate.Source, next.Source) < 0))
            {
                next = candidate;
            }
        }

        if (SameTask(current, next))
            return;

        current = next;
        Changed?.Invoke();
    }

    private static bool SameTask(PlayerTaskView a, PlayerTaskView b)
    {
        return a.Source == b.Source &&
               a.Key == b.Key &&
               a.Action == b.Action &&
               a.Detail == b.Detail &&
               a.Priority == b.Priority &&
               a.Target == b.Target &&
               a.Category == b.Category;
    }
}
