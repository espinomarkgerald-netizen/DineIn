using TMPro;
using UnityEngine;

public class EatingBubbleUI : MonoBehaviour
{
    // Legacy visual contract: this is the original single-mesh animation.
    // Keep its timing and motion unchanged; only the bounds guard below is a
    // permitted safety fix for TMP glyphs outside mesh 0.
    [SerializeField] private TMP_Text label;
    // Although the prefab once serialized "Eating..", CustomerGroup always
    // changed the live bubble to this exact text before the bounds bug.
    private const string LegacyText = "Eating";
    private const float LegacyBounceHeight = 8f;
    private const float LegacyBounceSpeed = 5f;
    private const float LegacyLetterDelay = 0.08f;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private bool meshReady;

    private void Awake()
    {
        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = LegacyText;

        PrepareText();
    }

    private void OnEnable()
    {
        PrepareText();
    }

    private void LateUpdate()
    {
        if (!meshReady || label == null)
            return;

        TMP_TextInfo textInfo = label.textInfo;
        if (textInfo.characterCount == 0)
            return;

        Vector3[] vertices = mesh.vertices;

        if (vertices == null || baseVertices == null || vertices.Length != baseVertices.Length)
            return;

        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = baseVertices[i];

        float time = Time.time * LegacyBounceSpeed;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            int vertexIndex = charInfo.vertexIndex;

            // Preserve the original single-mesh animation exactly. Some TMP
            // fallback glyphs report an index outside mesh 0; ignoring only
            // that invalid quad prevents the exception without changing the
            // timing or submission path for the original animated letters.
            if (vertexIndex < 0 || vertexIndex + 3 >= vertices.Length)
                continue;

            float charTime = time - (i * LegacyLetterDelay);
            float bounce = Mathf.Max(0f, Mathf.Sin(charTime)) * LegacyBounceHeight;
            Vector3 offset = new Vector3(0f, bounce, 0f);

            vertices[vertexIndex + 0] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
        }

        mesh.vertices = vertices;
        label.canvasRenderer.SetMesh(mesh);
    }

    private void PrepareText()
    {
        if (label == null)
            return;

        label.ForceMeshUpdate();

        TMP_TextInfo textInfo = label.textInfo;
        if (textInfo.meshInfo == null || textInfo.meshInfo.Length == 0)
        {
            meshReady = false;
            return;
        }

        mesh = textInfo.meshInfo[0].mesh;
        if (mesh == null || mesh.vertices == null || mesh.vertices.Length == 0)
        {
            meshReady = false;
            return;
        }

        baseVertices = mesh.vertices.Clone() as Vector3[];
        meshReady = true;
    }
}
