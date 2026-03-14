using UnityEngine;

public class MobileOutlineManager : MonoBehaviour
{
    private GameObject currentOutlined;

    void Update()
    {
        if(Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if(touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved)
            {
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                if(Physics.Raycast(ray, out RaycastHit hit))
                {
                    Outline outline = hit.collider.GetComponent<Outline>();
                    if(outline != null)
                    {
                        if(currentOutlined != null && currentOutlined != hit.collider.gameObject)
                            currentOutlined.GetComponent<Outline>().enabled = false;

                        currentOutlined = hit.collider.gameObject;
                        outline.enabled = true;
                    }
                }
            }

            if(touch.phase == TouchPhase.Ended)
            {
                if(currentOutlined != null)
                {
                    currentOutlined.GetComponent<Outline>().enabled = false;
                    currentOutlined = null;
                }
            }
        }
    }
}