using System.Collections.Generic;
using UnityEngine;

public class AlmanacListPopulator : MonoBehaviour
{
    [SerializeField] private AlmanacCardUI cardPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private List<AlmanacEntryData> entries = new List<AlmanacEntryData>();
    [SerializeField] private bool rebuildOnEnable = true;

    private readonly List<AlmanacCardUI> spawnedCards = new List<AlmanacCardUI>();

    private void Reset()
    {
        contentRoot = transform;
    }

    private void OnEnable()
    {
        if (rebuildOnEnable)
            Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (cardPrefab == null || contentRoot == null)
            return;

        ClearCards();

        for (int i = 0; i < entries.Count; i++)
        {
            AlmanacEntryData entry = entries[i];
            if (entry == null)
                continue;

            AlmanacCardUI card = Instantiate(cardPrefab, contentRoot);
            card.name = $"Card_{entry.entryName}";
            card.Bind(entry);
            spawnedCards.Add(card);
        }
    }

    [ContextMenu("Clear Cards")]
    public void ClearCards()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        spawnedCards.Clear();
    }
}