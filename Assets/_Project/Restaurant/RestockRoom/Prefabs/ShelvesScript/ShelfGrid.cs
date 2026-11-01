using UnityEngine;

public class ShelfGrid : MonoBehaviour
{
    [Header("Storage")]
    [SerializeField] private RestockStorageType storageType = RestockStorageType.Dry;
    [Tooltip("Optional authored save ID. When empty, a deterministic hierarchy path is used.")]
    [SerializeField] private string shelfID;

    [Header("Grid Size")]
    [Min(1)] public int columns = 4;
    [Min(1)] public int rows = 1;

    [Header("Cell Spacing")]
    public float cellWidth = 0.6f;
    public float cellDepth = 0.6f;

    [Header("Position")]
    public Vector3 localOffset = Vector3.zero;

    [Header("Debug")]
    public bool showGrid = true;

    [Header("Placement feedback")]
    [Range(.05f, 1f)] public float placementHighlightSeconds = .22f;
    public Color placementHighlight = new Color(.65f, 1f, .72f, 1f);
    private Renderer[] highlighted;
    private MaterialPropertyBlock[] originalBlocks;
    private Coroutine highlightRoutine;
    public void PresentPlacement()
    {
        GameplaySfx.CratePlaced();
        if (!isActiveAndEnabled) return;
        if (highlightRoutine != null) StopCoroutine(highlightRoutine);
        RestoreHighlight();
        highlightRoutine = StartCoroutine(HighlightPlacement());
    }
    private System.Collections.IEnumerator HighlightPlacement()
    {
        highlighted = GetComponentsInChildren<Renderer>();
        originalBlocks = new MaterialPropertyBlock[highlighted.Length];
        for (int i = 0; i < highlighted.Length; i++)
        {
            var renderer = highlighted[i];
            originalBlocks[i] = new MaterialPropertyBlock(); renderer.GetPropertyBlock(originalBlocks[i]);
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            var material = renderer.sharedMaterial;
            if (material == null) continue;
            string property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (!material.HasProperty(property)) continue;
            block.SetColor(property, Color.Lerp(material.GetColor(property), placementHighlight, .35f));
            renderer.SetPropertyBlock(block);
        }
        // A steady brief tint also respects reduced motion: no movement or pulsing.
        yield return new WaitForSecondsRealtime(placementHighlightSeconds);
        RestoreHighlight(); highlightRoutine = null;
    }
    private void RestoreHighlight()
    {
        if (highlighted != null)
            for (int i = 0; i < highlighted.Length; i++)
                if (highlighted[i] != null) highlighted[i].SetPropertyBlock(originalBlocks[i]);
        highlighted = null; originalBlocks = null;
    }
    private void OnDisable()
    {
        if (highlightRoutine != null) StopCoroutine(highlightRoutine);
        highlightRoutine = null; RestoreHighlight();
    }
    private GameObject[,] occupiedCells;

    public RestockStorageType StorageType => storageType;
    public string StableShelfId => string.IsNullOrWhiteSpace(shelfID)
        ? BuildHierarchyID()
        : shelfID.Trim();

    private void Awake()
    {
        EnsureGridAllocated();
    }

    public void ConfigureStorageType(RestockStorageType configuredType)
    {
        storageType = configuredType;
    }

    public Vector3 GetCellWorldPosition(int column, int row)
    {
        float totalWidth = (columns - 1) * cellWidth;
        float totalDepth = (rows - 1) * cellDepth;

        float x = column * cellWidth - totalWidth / 2f;
        float z = row * cellDepth - totalDepth / 2f;

        Vector3 localPosition =
            new Vector3(x, 0f, z) + localOffset;

        return transform.TransformPoint(localPosition);
    }

    public bool IsCellFree(int column, int row)
    {
        EnsureGridAllocated();
        if (!IsValidCell(column, row))
            return false;

        return occupiedCells[column, row] == null;
    }

    // Finds the closest cell even if that cell is occupied.
    // Used by the placement ghost.
    public bool TryGetClosestCell(
        Vector3 worldPosition,
        out int closestColumn,
        out int closestRow)
    {
        closestColumn = -1;
        closestRow = -1;

        float closestDistance = float.MaxValue;

        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                Vector3 cellPosition =
                    GetCellWorldPosition(x, z);

                float distance =
                    Vector3.SqrMagnitude(
                        worldPosition - cellPosition
                    );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestColumn = x;
                    closestRow = z;
                }
            }
        }

        return closestColumn != -1;
    }

    public bool TryGetClosestFreeCell(
        Vector3 worldPosition,
        out int closestColumn,
        out int closestRow)
    {
        closestColumn = -1;
        closestRow = -1;

        float closestDistance = float.MaxValue;

        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                if (!IsCellFree(x, z))
                    continue;

                Vector3 cellPosition =
                    GetCellWorldPosition(x, z);

                float distance =
                    Vector3.SqrMagnitude(
                        worldPosition - cellPosition
                    );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestColumn = x;
                    closestRow = z;
                }
            }
        }

        return closestColumn != -1;
    }

    public bool PlaceObject(
        GameObject objectToPlace,
        int column,
        int row)
    {
        EnsureGridAllocated();
        if (!IsCellFree(column, row))
            return false;

        occupiedCells[column, row] = objectToPlace;

        return true;
    }

    public void RemoveObject(
        GameObject objectToRemove,
        int column,
        int row)
    {
        EnsureGridAllocated();
        if (!IsValidCell(column, row))
            return;

        if (occupiedCells[column, row] == objectToRemove)
            occupiedCells[column, row] = null;
    }

    private bool IsValidCell(int column, int row)
    {
        return column >= 0 &&
               column < columns &&
               row >= 0 &&
               row < rows;
    }

    public bool TryGetFirstFreeCell(out int column, out int row)
    {
        EnsureGridAllocated();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (!IsCellFree(x, y))
                    continue;
                column = x;
                row = y;
                return true;
            }
        }
        column = -1;
        row = -1;
        return false;
    }

    private void EnsureGridAllocated()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        if (occupiedCells == null ||
            occupiedCells.GetLength(0) != columns ||
            occupiedCells.GetLength(1) != rows)
        {
            occupiedCells = new GameObject[columns, rows];
        }
    }

    private string BuildHierarchyID()
    {
        System.Text.StringBuilder path = new System.Text.StringBuilder();
        Transform current = transform;
        while (current != null)
        {
            if (path.Length > 0)
                path.Insert(0, '/');
            path.Insert(0, current.name + "[" + current.GetSiblingIndex() + "]");
            current = current.parent;
        }
        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Scene";
        return sceneName + ":" + path;
    }

    private void OnValidate()
    {
        shelfID = shelfID != null ? shelfID.Trim() : string.Empty;
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
    }

    private void OnDrawGizmos()
    {
        if (!showGrid)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;

        float totalWidth = (columns - 1) * cellWidth;
        float totalDepth = (rows - 1) * cellDepth;

        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                float xPosition =
                    x * cellWidth - totalWidth / 2f;

                float zPosition =
                    z * cellDepth - totalDepth / 2f;

                Vector3 position =
                    new Vector3(
                        xPosition,
                        0f,
                        zPosition
                    ) + localOffset;

                Gizmos.DrawWireCube(
                    position,
                    new Vector3(
                        cellWidth * 0.9f,
                        0.05f,
                        cellDepth * 0.9f
                    )
                );
            }
        }
    }
}
