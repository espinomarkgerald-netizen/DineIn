using TMPro;
using UnityEngine;

public class EatingBubbleUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string baseText = "Eating";
    [SerializeField] private float bounceHeight = 8f;
    [SerializeField] private float bounceSpeed = 8f;
    [SerializeField] private float letterDelay = 0.12f;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private bool meshReady;

    private void Awake()
    {
        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        SetBaseText(baseText);
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

        float time = Time.time * bounceSpeed;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            int vertexIndex = charInfo.vertexIndex;

            float charTime = time - (i * letterDelay);
            float bounce = Mathf.Max(0f, Mathf.Sin(charTime)) * bounceHeight;
            Vector3 offset = new Vector3(0f, bounce, 0f);

            vertices[vertexIndex + 0] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
        }

        mesh.vertices = vertices;
        label.canvasRenderer.SetMesh(mesh);
    }

    public void SetBaseText(string text)
    {
        baseText = text;

        if (label == null)
            return;

        label.text = baseText;
        PrepareText();
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