using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

class VolumetricPass : RaymarcherPass
{
    //[SerializeField] private Shader volumetricRenderer;
    [SerializeField] private ComputeShader volumetricRendererCompute;
    [SerializeField] private ComputeShader conemarchComputeShader;
    [SerializeField] private ComputeShader reprojectComputeShader;
    [SerializeField] private ComputeShader volumetricCombine;

    [SerializeField] private int display = 0;
    // [SerializeField] private Shader volumetricCombine;

    [SerializeField] private VolumetricParams volumetricParams;
    
    [Header("Settings")]
    
    [SerializeField] private Light sun;
    [SerializeField] private float sunIntensity = 2;
    
    [SerializeField] private bool reprojection;

    [SerializeField] private Texture3D sdfMap;
    [SerializeField] private float sdfMarchDistance = 50;
    
    private RTHandle volumetricTarget;
    private RTHandle[] conemarchTargets;

    private ComputeShader rendererCompute;
    private ComputeShader conemarchCompute;
    // private Material combineMaterial;
    private ComputeShader combineCompute;

    private TemporalReprojection temporalReprojection;

    private Dictionary<string, float> customParameters = new Dictionary<string, float>();

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        volumetricTarget = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, volumetricParams.downsample), TextureXR.slices, dimension: TextureXR.dimension, 
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            useDynamicScale: true, name: "Volumetrics Target", enableRandomWrite:true, filterMode: FilterMode.Bilinear
        );

        conemarchTargets = new RTHandle[volumetricParams.conemarchIterations];
        if (volumetricParams.conemarchIterations > 0)
        {
            for (int i = 0; i < volumetricParams.conemarchIterations; i++)
            {
                conemarchTargets[i] = RTHandles.Alloc(
                    Vector2.one / Mathf.Pow(2, volumetricParams.conemarchIterations - i + volumetricParams.conemarchOffset + volumetricParams.downsample), TextureXR.slices, dimension: TextureXR.dimension, 
                    colorFormat: GraphicsFormat.R16_UNorm,
                    useDynamicScale: true, name: "Conemarch Targets " + i, enableRandomWrite:true, filterMode: FilterMode.Point, useMipMap:false
                );
            }

        }

        // combineMaterial = CoreUtils.CreateEngineMaterial(volumetricCombine);
        combineCompute = Object.Instantiate(volumetricCombine);
        rendererCompute = Object.Instantiate(volumetricRendererCompute);
        conemarchCompute = Object.Instantiate(conemarchComputeShader);
        
        rendererCompute.SetFloat("_OverStep", 100.0f);

        foreach (var customParameter in customParameters)
        {
            rendererCompute.SetFloat(customParameter.Key, customParameter.Value);
        }
        
        
        if (reprojection)
        {
            if (temporalReprojection != null)
            {
                temporalReprojection.Cleanup();
            }

            temporalReprojection = new TemporalReprojection(reprojectComputeShader, rendererCompute, volumetricParams, volumetricTarget);
            
        }
        
        rendererCompute.SetKeyword(new LocalKeyword(rendererCompute, "REPROJECT"), reprojection);
        // rendererCompute.SetKeyword(new LocalKeyword(rendererCompute, "CONEMARCH"), volumetricParams.conemarchIterations > 0);
        
        UpdateVariables();
        Debug.Log("Initialise");
    }


    private void UpdateVariables()
    {
        combineCompute.SetTexture(0, "_Volumetrics", volumetricTarget);
        combineCompute.SetFloat("_ScreenScale", 1.0f / Mathf.Pow(2, volumetricParams.downsample));
        // combineMaterial.SetTexture("_Volumetrics", volumetricTarget);
        // combineMaterial.SetFloat("_ScreenScale", 1.0f / Mathf.Pow(2, targetDownsample));
        
        rendererCompute.SetFloat("_SDFMarchDistance", Mathf.Max(10, sdfMarchDistance));
        rendererCompute.SetVector("_SunDirection", -sun.transform.forward);
        rendererCompute.SetVector("_SunColour", sun.color * sunIntensity);
        rendererCompute.SetTexture(0, "_Result", volumetricTarget);

        if (conemarchTargets.Length > 0)
        {
            rendererCompute.SetTexture(0, "_Conemarch", conemarchTargets[^1]);
        }
        // rendererCompute.SetTexture(0, "_SDFMap", sdfMap);
        
        volumetricParams.SetVariables(rendererCompute);
        volumetricParams.SetVariables(conemarchCompute);
    }
    
    
    private Vector4[] FrustumCorners(Camera cam)
    {
        Transform camtr = cam.transform;
 
        Vector3[] frustumCorners = new Vector3[4];
        cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1),
            1, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
        
        Vector3 bottomLeft = camtr.TransformVector(frustumCorners[1]);
        Vector3 bottomRight = camtr.TransformVector(frustumCorners[2]);
        Vector3 topLeft = camtr.TransformVector(frustumCorners[0]);
        Vector3 topRight = camtr.TransformVector(frustumCorners[3]);
 
        Vector4[] frustumVectorsArray = new Vector4[] {bottomLeft, bottomRight, topLeft, topRight};
 
        return frustumVectorsArray;
    }


    protected override void Execute(CustomPassContext ctx)
    {
        var currentFrustrum = FrustumCorners(ctx.hdCamera.camera);
        rendererCompute.SetVectorArray("_CamFrustrum", currentFrustrum);
        conemarchCompute.SetVectorArray("_CamFrustrum", currentFrustrum);

        int pixelsAmount = (int)Mathf.Pow(2, volumetricParams.downsample);
        Vector4 offset = Vector4.zero;
        if (reprojection)
        {
            offset = temporalReprojection.GetPixelOffset();
        }

        rendererCompute.SetFloat("_Downscale", pixelsAmount);
        rendererCompute.SetVector("_UpdateOffset", offset);
        
        rendererCompute.SetTexture(0, "_Result", volumetricTarget, 0, RenderTextureSubElement.Color);
        
        conemarchCompute.SetFloat("_Downscale", pixelsAmount);
        conemarchCompute.SetVector("_UpdateOffset", offset);

        if (conemarchTargets.Length > 0)
        {
            conemarchCompute.SetFloat("_TanFOV", Mathf.Tan(ctx.hdCamera.camera.fieldOfView * Mathf.Deg2Rad / 2.0f));
            ctx.cmd.SetComputeFloatParam(conemarchCompute, "_Mult", 0);
            ctx.cmd.SetComputeTextureParam(conemarchCompute,0, "_Previous", conemarchTargets[0]);
            for (int i = 0; i < conemarchTargets.Length; i++)
            {
                ctx.cmd.SetComputeTextureParam(conemarchCompute,0, "_Result", conemarchTargets[i]);
                ctx.cmd.DispatchCompute(conemarchCompute, 0, 1 + conemarchTargets[i].rt.width / 8,
                    1 + conemarchTargets[i].rt.height / 8, 1);
                ctx.cmd.SetComputeFloatParam(conemarchCompute, "_Mult", 1);
                ctx.cmd.SetComputeTextureParam(conemarchCompute,0, "_Previous", conemarchTargets[i]);
            }
        }
        
        ctx.cmd.DispatchCompute(rendererCompute, 0, 1 + volumetricTarget.rt.width / 8,
            1 + volumetricTarget.rt.height / 8, 1);

        if (reprojection)
        {
            temporalReprojection.Execute(ctx, volumetricTarget, currentFrustrum);
        }
        
        // combineCompute.SetTexture(0, "_Read", ctx.cameraColorBuffer);
        combineCompute.SetTexture(0, "_Result", ctx.cameraColorBuffer);
        ctx.cmd.DispatchCompute(combineCompute, 0, 1 + ctx.hdCamera.actualWidth / 8, 1 + ctx.hdCamera.actualWidth / 8, 1);
        // HDUtils.DrawFullScreen(ctx.cmd, combineMaterial, ctx.cameraColorBuffer, null, 0);
    }

    // Releases the GPU memory allocated for the half-resolution target. This is important otherwise the memory will leak.
    protected override void Cleanup()
    {
        Debug.Log("Cleanup");
        volumetricTarget.Release();
        RTHandles.Release(volumetricTarget);

        foreach (RTHandle conemarchTarget in conemarchTargets)
        {
            conemarchTarget.Release();
            RTHandles.Release(conemarchTarget);
        }
        CoreUtils.Destroy(combineCompute);
        CoreUtils.Destroy(conemarchCompute);
        // CoreUtils.Destroy(combineMaterial);
        CoreUtils.Destroy(rendererCompute);

        temporalReprojection?.Cleanup();
    }

    public override void SetVolumetricParams(VolumetricParams volumeParams)
    {
        volumetricParams = volumeParams;
        UpdateVariables();
    }

    public override void SetParameter(string id, float value)
    {
        if (!customParameters.TryAdd(id, value))
        {
            customParameters[id] = value;
        }
    }
}