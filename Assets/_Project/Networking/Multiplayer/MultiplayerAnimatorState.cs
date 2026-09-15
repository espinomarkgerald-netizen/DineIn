using System.Collections.Generic;
using UnityEngine;

// Animator.parameters allocates an array. Cache it until the controller changes.
public sealed class MultiplayerAnimatorState
{
    public Animator Animator { get; }
    private RuntimeAnimatorController controller;
    private readonly HashSet<int> parameters = new();
    public MultiplayerAnimatorState(GameObject root) { Animator = root.GetComponentInChildren<Animator>(true); }
    public bool Has(int hash)
    {
        if (Animator == null || Animator.runtimeAnimatorController == null) return false;
        if (controller != Animator.runtimeAnimatorController)
        {
            controller = Animator.runtimeAnimatorController; parameters.Clear();
            foreach (var parameter in Animator.parameters) parameters.Add(parameter.nameHash);
        }
        return parameters.Contains(hash);
    }
    public float Float(int hash) => Has(hash) ? Animator.GetFloat(hash) : 0f;
    public bool Bool(int hash) => Has(hash) && Animator.GetBool(hash);
    public void Set(int hash, float value) { if (Has(hash)) Animator.SetFloat(hash, value); }
    public void Set(int hash, bool value) { if (Has(hash)) Animator.SetBool(hash, value); }
}
