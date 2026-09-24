using UnityEngine;
public sealed class FastFoodCookingDropTarget : MonoBehaviour
{
    [HideInInspector] public FastFoodCookingView view;
    [Tooltip("0 = cooking, 1 = preparation / assembly, 2 = discard")]
    [Range(0,2)] public int kind;
}
