using Photon.Pun;
using UnityEngine;

public class PlayerSetup : MonoBehaviourPun
{
    public GameObject cameraGameObject; // The child object with the Camera
    public PlayerMovement movement;

    private void Start()
    {
        if (photonView.IsMine)
        {
            cameraGameObject.SetActive(true);
            movement.enabled = true;

            // Get the Camera component from the child object
            Camera cam = cameraGameObject.GetComponent<Camera>();
            
            if (cam != null)
            {
                // This line will now work because PlayerMovement has SetCamera
                movement.SetCamera(cam);
            }
        }
        else
        {
            // For other players, turn their camera off so it doesn't fight yours
            cameraGameObject.SetActive(false);
            movement.enabled = false;
        }
    }
}