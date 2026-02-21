using Photon.Pun;
using UnityEngine;

public class PlayerSetup : MonoBehaviourPun
{
    public GameObject cameraGameObject; // The child object with the Camera
    public PlayerMovement movement;

    private void Start()
    {
        if (!photonView.IsMine)
        {
            cameraGameObject.SetActive(false);
            movement.enabled = false;
            return;
        }

        EnableLocalPlayer();
    }

    private void EnableLocalPlayer()
    {
        if (!cameraGameObject) return;

        cameraGameObject.SetActive(true);
        movement.enabled = true;

        Camera cam = cameraGameObject.GetComponent<Camera>();
        if (cam != null)
        {
            movement.SetCamera(cam);
        }
    }

}