using UnityEngine;

public class RoleIndicator : MonoBehaviour
{
    [Header("Indicator")]
    [SerializeField] private GameObject indicatorVisual;

    [Header("Floating Burger")]
    [SerializeField] private Transform burgerVisual;
    [SerializeField] private float floatHeight = 0.15f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float spinSpeed = 90f;

    private Vector3 burgerStartLocalPos;
    private bool isSelected;
    private float spinAngle;
    [Header("Local player ground ring")]
    [SerializeField, Min(.1f)] private float playerRingRadius = .45f;
    [SerializeField, Range(.01f,.2f)] private float playerRingThickness = .055f;
    [SerializeField, Range(0f,1f)] private float playerRingOpacity = .9f;
    [SerializeField] private float playerRingGroundOffset = .025f;
    private GameObject playerRing;
    private Mesh ringMesh;
    private Material ringMaterial;
    private bool localPlayer;

    public void SetLocalPlayer(bool value)
    {
        localPlayer=value;
        if(value && playerRing==null)CreatePlayerRing();
        if(playerRing!=null)playerRing.SetActive(value);
    }
    private void CreatePlayerRing()
    {
        const int segments=48;
        var vertices=new Vector3[(segments+1)*2];var triangles=new int[segments*6];
        for(int i=0;i<=segments;i++) {
            float a=i*Mathf.PI*2/segments;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            vertices[i*2]=direction*playerRingRadius;vertices[i*2+1]=direction*Mathf.Max(.01f,playerRingRadius-playerRingThickness);
            if(i<segments){int t=i*6,v=i*2;triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;}
        }
        ringMesh=new Mesh{name="Local player ring"};ringMesh.vertices=vertices;ringMesh.triangles=triangles;ringMesh.RecalculateNormals();
        var shader=Shader.Find("Sprites/Default");if(shader==null){Destroy(ringMesh);return;}
        ringMaterial=new Material(shader){name="Local player ring",color=new Color(1,1,1,playerRingOpacity)};
        playerRing=new GameObject("Local player ground ring",typeof(MeshFilter),typeof(MeshRenderer));playerRing.transform.SetParent(transform,false);
        playerRing.GetComponent<MeshFilter>().sharedMesh=ringMesh;
        var renderer=playerRing.GetComponent<MeshRenderer>();renderer.sharedMaterial=ringMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    private void LateUpdate()
    {
        if(playerRing==null)return;
        var movement=GetComponent<PlayerMovement>();bool visible=localPlayer && movement!=null && movement.IsPlayerControlled();
        playerRing.SetActive(visible);if(!visible)return;
        playerRing.transform.position=transform.position+Vector3.up*playerRingGroundOffset;playerRing.transform.rotation=Quaternion.identity;
        var scale=transform.lossyScale;playerRing.transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(scale.x)),1/Mathf.Max(.001f,Mathf.Abs(scale.y)),1/Mathf.Max(.001f,Mathf.Abs(scale.z)));
    }
    private void OnDestroy(){if(ringMesh!=null)Destroy(ringMesh);if(ringMaterial!=null)Destroy(ringMaterial);}

    private void Awake()
    {
        if (burgerVisual != null)
            burgerStartLocalPos = burgerVisual.localPosition;
    }

    private void Update()
    {
        if (!isSelected || burgerVisual == null) return;

        float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        burgerVisual.localPosition = burgerStartLocalPos + new Vector3(0f, yOffset, 0f);

        spinAngle += spinSpeed * Time.deltaTime;
        burgerVisual.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
    }

    public void SetSelected(bool value)
    {
        isSelected = value;

        if (indicatorVisual != null)
            indicatorVisual.SetActive(value);

        if (burgerVisual != null)
        {
            burgerVisual.gameObject.SetActive(value);

            if (value)
            {
                burgerVisual.localPosition = burgerStartLocalPos;
                burgerVisual.localRotation = Quaternion.identity;
                spinAngle = 0f;
            }
        }
    }
}
