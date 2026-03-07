using UnityEngine;

public class SkyboxManager : MonoBehaviour
{
    [SerializeField] private Material skyboxMaterial;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        ApplySkybox();
    }

    private void ApplySkybox()
    {
        RenderSettings.skybox = skyboxMaterial;
        DynamicGI.UpdateEnvironment();
    }

}