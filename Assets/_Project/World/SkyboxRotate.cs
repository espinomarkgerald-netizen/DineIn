using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public class SkyboxRotate : MonoBehaviour
{
    [SerializeField]
    float RotateSpeed = 1.2f;
  
    void Update()
    {
        RenderSettings.skybox.SetFloat("_Rotation", Time.time * RotateSpeed);
    }
}
