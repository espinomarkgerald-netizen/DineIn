using UnityEngine;

public class FinanceManager : MonoBehaviour
{
    public static FinanceManager Instance { get; private set; }
    public float totalCash;
    public float salaryBudget;

    void Awake()
    {
        if(Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public void PaySalaries()
    {
        totalCash -= salaryBudget;
    }
}
