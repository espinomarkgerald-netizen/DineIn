using UnityEngine;

public class UIBouncingArrow : MonoBehaviour {
    public float speed = 5f;
    public float height = 20f; // This moves it 20 pixels up and down!

    private RectTransform rect;
    private Vector2 startPos;

    void Start() {
        rect = GetComponent<RectTransform>();
        startPos = rect.anchoredPosition;
    }

    void Update() {
        float newY = startPos.y + Mathf.Sin(Time.time * speed) * height;
        rect.anchoredPosition = new Vector2(startPos.x, newY);
    }
}