using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class MomentumIndicator : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float predictionTime = 0.45f;
    [SerializeField, Min(0f)] private float minimumMomentum = 1f;
    [SerializeField, Range(0.05f, 0.8f)] private float opacity = 0.6f;
    [SerializeField] private Color indicatorTint = new Color(0.2f, 0.9f, 1f, 1f);

    private readonly List<Material> indicatorMaterials = new List<Material>();
    private PlayerMovement playerMovement;
    private Transform indicator;
    private Renderer[] indicatorRenderers;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        Transform sourceVisual = transform.Find("PlayerVisual");
        if (sourceVisual == null)
        {
            Renderer sourceRenderer = GetComponentInChildren<Renderer>();
            if (sourceRenderer != null)
            {
                sourceVisual = sourceRenderer.transform;
            }
        }

        if (sourceVisual == null)
        {
            Debug.LogWarning("MomentumIndicator could not find a player visual to duplicate.", this);
            enabled = false;
            return;
        }

        GameObject indicatorObject = Instantiate(sourceVisual.gameObject, sourceVisual.parent);
        indicatorObject.name = "Momentum Indicator";
        indicator = indicatorObject.transform;
        indicatorRenderers = indicatorObject.GetComponentsInChildren<Renderer>();
        ConfigureRenderers();
    }

    private void LateUpdate()
    {
        Vector3 momentum = playerMovement.MomentumVelocity;
        float horizontalMomentum = Vector3.ProjectOnPlane(momentum, Vector3.up).magnitude;
        float verticalMomentum = playerMovement.IsGrounded ? 0f : Mathf.Abs(momentum.y);
        bool showIndicator = Mathf.Max(horizontalMomentum, verticalMomentum) >= minimumMomentum;
        indicator.gameObject.SetActive(showIndicator);
        if (!showIndicator)
        {
            return;
        }

        indicator.position = playerMovement.PredictMomentumPosition(predictionTime);
        indicator.rotation = transform.rotation;
    }

    private void OnDestroy()
    {
        foreach (Material material in indicatorMaterials)
        {
            Destroy(material);
        }
    }

    private void ConfigureRenderers()
    {
        Shader indicatorShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (indicatorShader == null)
        {
            indicatorShader = Shader.Find("Sprites/Default");
        }

        foreach (Renderer indicatorRenderer in indicatorRenderers)
        {
            indicatorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            indicatorRenderer.receiveShadows = false;

            Material[] sourceMaterials = indicatorRenderer.sharedMaterials;
            int materialCount = Mathf.Max(1, sourceMaterials.Length);
            Material[] translucentMaterials = new Material[materialCount];
            for (int index = 0; index < materialCount; index++)
            {
                Material sourceMaterial = sourceMaterials.Length > 0 ? sourceMaterials[index] : null;
                Shader shader = indicatorShader != null ? indicatorShader : sourceMaterial != null ? sourceMaterial.shader : null;
                if (shader == null)
                {
                    Debug.LogWarning("MomentumIndicator could not find a shader for its preview material.", this);
                    continue;
                }

                Material material = new Material(shader);
                if (sourceMaterial != null && sourceMaterial.HasProperty("_BaseMap") && material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", sourceMaterial.GetTexture("_BaseMap"));
                }
                SetTransparent(material);
                translucentMaterials[index] = material;
                indicatorMaterials.Add(material);
            }

            if (translucentMaterials[0] == null)
            {
                indicatorRenderer.enabled = false;
                continue;
            }
            indicatorRenderer.materials = translucentMaterials;
        }
    }

    private void SetTransparent(Material material)
    {
        Color tint = indicatorTint;
        tint.a = opacity;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", tint);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", tint);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}