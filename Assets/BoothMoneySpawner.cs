using UnityEngine;

public class BoothMoneySpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform moneySpawnPoint;
    [SerializeField] private GameObject moneyPrefab;

    [Header("Safety")]
    [SerializeField] private bool onlyOneMoneyAtATime = true;

    private MoneyPickup spawned;

    public Transform MoneySpawnPoint
    {
        get
        {
            if (moneySpawnPoint != null) return moneySpawnPoint;
            var t = transform.Find("TableMoneySpawn");
            if (t != null) return t;
            t = transform.Find("TableFoodSpawn");
            if (t != null) return t;
            return transform;
        }
    }

    public bool HasMoneySpawned => spawned != null;

    public MoneyPickup SpawnMoney(CustomerGroup group, int amount, Transform standPointForPickup = null)
    {
        if (moneyPrefab == null) return null;
        if (group == null) return null;

        if (onlyOneMoneyAtATime && spawned != null)
            return spawned;

        Transform sp = MoneySpawnPoint;

        var go = Instantiate(moneyPrefab, sp.position, sp.rotation, sp);
        var money = go.GetComponentInChildren<MoneyPickup>(true);
        if (money == null) money = go.GetComponent<MoneyPickup>();

        if (money == null)
        {
            Destroy(go);
            return null;
        }

        money.Init(group, amount, standPointForPickup);
        spawned = money;
        return spawned;
    }

    public void ClearSpawnedMoney()
    {
        if (spawned != null)
        {
            Destroy(spawned.gameObject);
            spawned = null;
        }
    }
}