using UnityEngine;

public class OrderNumberManager : MonoBehaviour
{
    public static OrderNumberManager Instance;

    [SerializeField] private int nextOrderNumber = 1;

    private void Awake()
    {
        Instance = this;
    }

    public int GetNextOrderNumber()
    {
        return nextOrderNumber++;
    }
}