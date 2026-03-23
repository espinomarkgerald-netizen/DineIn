using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoleRowUI : MonoBehaviour
{
    public EmployeeRole roleType;

    public Transform contentParent;
    public EmployeeCard cardPrefab;

    private List<EmployeeCard> spawnedCards = new List<EmployeeCard>();

    public void Populate(List<EmployeeData> employees, HRManager hrManager)
    {
        // Destroy old cards
        for (int i = spawnedCards.Count - 1; i >= 0; i--)
        {
            Destroy(spawnedCards[i].gameObject);
        }
        spawnedCards.Clear();

        Debug.Log($"Spawning {employees.Count} cards under {contentParent.name}");
        RectTransform contentRect = contentParent.GetComponent<RectTransform>();

        // Spawn new cards
        foreach (var emp in employees)
        {
            EmployeeCard card = Instantiate(cardPrefab, contentParent);
            card.Setup(emp);
            card.hrManager = hrManager;
            Debug.Log($"Spawned card for {emp.employeeName}");
            
            spawnedCards.Add(card);
        }

        // Force layout rebuild once after all cards are spawned
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }
}