using System.Collections.Generic;
using UnityEngine;

public class RoleRowUI : MonoBehaviour
{
    public EmployeeRole roleType;

    public Transform contentParent;
    public EmployeeCard cardPrefab;

    private List<EmployeeCard> spawnedCards = new List<EmployeeCard>();

    public void Populate(List<EmployeeData> employees, HRManager hrManager)
    {
        foreach (var card in spawnedCards)
            Destroy(card.gameObject);

        spawnedCards.Clear();

        Debug.Log($"Spawning {employees.Count} cards under {contentParent.name}");
        foreach (var emp in employees)
        {
            EmployeeCard card = Instantiate(cardPrefab, contentParent);
            card.Setup(emp);
            card.hrManager = hrManager;
            Debug.Log($"Spawned card for {emp.employeeName}");

            spawnedCards.Add(card);
        }
    }
}