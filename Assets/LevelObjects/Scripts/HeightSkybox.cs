using UnityEngine;

[DisallowMultipleComponent]
public class HeightSkybox : MonoBehaviour
{
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private Transform player;
    [SerializeField, Min(1f)] private float heightForFullTransition = 100f;
    [SerializeField, Min(0f)] private float transitionSpeed = 3f;

    private static readonly int HeightProperty = Shader.PropertyToID("_Height01");
    private Material runtimeMaterial;
    private Material previousSkybox;
    private float startingHeight;

    public float HeightBlend => runtimeMaterial != null ? runtimeMaterial.GetFloat(HeightProperty) : 0f;

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement == null) movement = FindAnyObjectByType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }
        if (player == null || skyboxMaterial == null)
        {
            Debug.LogError("HeightSkybox requires a player and a skybox material.", this);
            enabled = false;
            return;
        }

        startingHeight = player.position.y;
        previousSkybox = RenderSettings.skybox;
        runtimeMaterial = new Material(skyboxMaterial) { name = "Height Skybox (Runtime)" };
        runtimeMaterial.SetFloat(HeightProperty, 0f);
        RenderSettings.skybox = runtimeMaterial;
    }

    private void LateUpdate()
    {
        if (runtimeMaterial == null || player == null) return;
        float target = Mathf.Clamp01((player.position.y - startingHeight) / Mathf.Max(1f, heightForFullTransition));
        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        runtimeMaterial.SetFloat(HeightProperty, Mathf.Lerp(HeightBlend, target, blend));
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null) return;
        if (RenderSettings.skybox == runtimeMaterial) RenderSettings.skybox = previousSkybox;
        Destroy(runtimeMaterial);
    }
}
