using System;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

class VolumetricPass : CustomPass
{
    //[SerializeField] private Shader volumetricRenderer;
    [SerializeField] private ComputeShader volumetricRendererCompute;
    [SerializeField] private ComputeShader reprojectComputeShader;
    [SerializeField] private Shader volumetricCombine;
    
    [Header("Settings")]
    
    [SerializeField] private Light sun;
    [SerializeField] private float sunIntensity = 2;
    [SerializeField] private Color ambientColour = Color.white;
    [Header("Bounds")]
    [SerializeField] private float minHeight = 500;
    [SerializeField] private float maxHeight = 500;
    [SerializeField] private float width = 2000;

    [Header("Marching Parameters")] [SerializeField]
    private float bufferDistance = 10;
    [SerializeField] private int downsample = 0;
    [SerializeField] private int targetDownsample = 0;
    [SerializeField] private bool reprojection;

    [SerializeField, Range(-1, 1)] private float phase = 0.8f;
    [SerializeField, Range(-1, 1)] private float phase2 = 0.4f;
    [Header("Density Parameters")] [SerializeField]
    private Texture3D densityMap;
    [SerializeField] private Texture3D detailDensityMap;
    [SerializeField] private Texture3D sdfMap;
    [SerializeField] private float sdfMarchDistance = 50;
    [SerializeField] private float density = 1;
    [SerializeField] private float lightDensity = 1;
    [SerializeField] private Vector3 scale;
    [SerializeField] private float detailAmount = -0.25f;
    [SerializeField] private float detailScale = 0.05f;

    [Header("Mesh")] [SerializeField] private bool useMesh = false;
    [SerializeField] private Mesh[] meshes;
    [SerializeField] private GameObject autoParent;
    [SerializeField] private bool runAuto = false;
    [SerializeField] private Material meshMaterial;
    
    private RTHandle volumetricTarget;
    private RTHandle volumetricTargetRead;
    private RTHandle reprojectionTarget;
    private RTHandle meshDepth;

    private ComputeShader rendererCompute;
    private ComputeShader reprojectCompute;
    private Material combineMaterial;

    private int counter = 0;

    private Matrix4x4 lastViewProject;
    private Matrix4x4 lastViewProjectInverse;

    private Vector4[] lastFrustrum;
    private Vector3 lastCameraPosition;

    private readonly int[][] dither = new int[][] {new [] {0}, new []{0, 2, 3, 1}, new [] {0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5}};

    private float[] meshDistances;
    private int[] meshDistancesIndex;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        volumetricTarget = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, targetDownsample), TextureXR.slices, dimension: TextureXR.dimension, 
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            useDynamicScale: true, name: "Volumetrics Target", enableRandomWrite:true
        );
        volumetricTargetRead = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, targetDownsample), TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            useDynamicScale: true, name: "Volumetrics Target", enableRandomWrite:true
        );
        
        meshDepth = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, targetDownsample), TextureXR.slices, dimension: TextureXR.dimension, depthBufferBits: DepthBits.Depth32,
            colorFormat: GraphicsFormat.None,
            useDynamicScale: true, name: "Volumetrics Target", enableRandomWrite:false
        );
        reprojectionTarget = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, downsample), TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R32_SFloat,
            useDynamicScale: true, name: "Reprojection Depth", enableRandomWrite:true
        );

        combineMaterial = CoreUtils.CreateEngineMaterial(volumetricCombine);
        rendererCompute = Object.Instantiate(volumetricRendererCompute);
        reprojectCompute = Object.Instantiate(reprojectComputeShader);

        meshDistances = new float[meshes.Length];
        meshDistancesIndex = new int[meshes.Length];
        
        UpdateVariables();
        Debug.Log("Initialise");
    }


    private void UpdateVariables()
    {
        if (runAuto)
        {
            runAuto = false;
            var list = autoParent.GetComponentsInChildren<MeshFilter>();
            meshes = new Mesh[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                meshes[i] = list[i].sharedMesh;
            }
        }
        combineMaterial.SetTexture("_Volumetrics", volumetricTarget);
        combineMaterial.SetFloat("_ScreenScale", 1.0f / Mathf.Pow(2, targetDownsample));
        
        rendererCompute.SetTexture(0, "_DensityMap", densityMap);
        rendererCompute.SetTexture(0, "_DetailDensityMap", detailDensityMap);
        rendererCompute.SetTexture(0, "_ReprojectionPositions", reprojectionTarget);
        rendererCompute.SetFloat("_Density", density);
        rendererCompute.SetFloat("_LightDensity", lightDensity);
        rendererCompute.SetVector("_Scale", scale);
        rendererCompute.SetVector("_AmbientColour", ambientColour);
        rendererCompute.SetFloat("_DetailScale", detailScale);
        rendererCompute.SetFloat("_DetailAmount", detailAmount);
        rendererCompute.SetFloat("_Phase", phase);
        rendererCompute.SetFloat("_Phase2", phase2);
        rendererCompute.SetFloat("_BufferDistance", bufferDistance);
        rendererCompute.SetFloat("_MinHeight", minHeight);
        rendererCompute.SetFloat("_MaxHeight", maxHeight);
        rendererCompute.SetFloat("_Width", width);
        rendererCompute.SetFloat("_SDFMarchDistance", Mathf.Max(10, sdfMarchDistance));
        rendererCompute.SetVector("_SunDirection", -sun.transform.forward);
        rendererCompute.SetVector("_SunColour", sun.color * sunIntensity);
        rendererCompute.SetTexture(0, "_Result", volumetricTarget);
        rendererCompute.SetTexture(0, "_SDFMap", sdfMap);
        
        meshMaterial.SetFloat("_Density", density);
        meshMaterial.SetFloat("_LightDensity", lightDensity);
        meshMaterial.SetVector("_Scale", scale);
        meshMaterial.SetVector("_AmbientColour", ambientColour);
        meshMaterial.SetFloat("_DetailScale", detailScale);
        meshMaterial.SetFloat("_DetailAmount", detailAmount);
        meshMaterial.SetFloat("_Phase", phase);
        meshMaterial.SetFloat("_Phase2", phase2);
        meshMaterial.SetFloat("_BufferDistance", bufferDistance);
        meshMaterial.SetFloat("_MinHeight", minHeight);
        meshMaterial.SetFloat("_MaxHeight", maxHeight);
        meshMaterial.SetFloat("_Width", width);
        meshMaterial.SetFloat("_SDFMarchDistance", Mathf.Max(10, sdfMarchDistance));
        meshMaterial.SetVector("_SunDirection", -sun.transform.forward);
        meshMaterial.SetVector("_SunColour", sun.color * sunIntensity);
        meshMaterial.SetTexture("_DensityMap", densityMap);
        meshMaterial.SetTexture("_DetailDensityMap", detailDensityMap);

        reprojectCompute.SetFloat("_CloudHeight", minHeight);
        reprojectCompute.SetTexture(0, "_ReprojectionPositions", reprojectionTarget);
        reprojectCompute.SetTexture(0, "_Read", volumetricTargetRead);
        reprojectCompute.SetTexture(0, "_Result", volumetricTarget);
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
        UpdateVariables();
        var currentFrustrum = FrustumCorners(ctx.hdCamera.camera);
        rendererCompute.SetVectorArray("_CamFrustrum", currentFrustrum);
        
        int pixelsAmount = (int)Mathf.Pow(2, downsample - targetDownsample);
        int index = dither[downsample - targetDownsample][counter++ % (pixelsAmount * pixelsAmount)];
        Vector4 offset = new Vector4(index % pixelsAmount, (int)(index % (pixelsAmount * pixelsAmount) / pixelsAmount));

        if (!useMesh)
        {
            rendererCompute.SetFloat("_Downscale", pixelsAmount);
            rendererCompute.SetVector("_UpdateOffset", offset);

            rendererCompute.SetTexture(0, "_Result", volumetricTarget);
            ctx.cmd.DispatchCompute(rendererCompute, 0, 1 + volumetricTarget.rt.width / (8 * pixelsAmount),
                1 + volumetricTarget.rt.height / (8 * pixelsAmount), volumetricTarget.rt.mipmapCount);
        }
        else
        {
            CustomPassUtils.Copy(ctx, ctx.cameraDepthBuffer, meshDepth);
            CoreUtils.SetRenderTarget(ctx.cmd, volumetricTarget, meshDepth, ClearFlag.Color, Color.clear);
            for (int i = 0; i < meshes.Length; i++)
            {
                meshDistances[i] = -(ctx.hdCamera.camera.transform.position - meshes[i].bounds.center).sqrMagnitude;
                meshDistancesIndex[i] = i;
            }
            Array.Sort(meshDistances, meshDistancesIndex);
            for (int i = 0; i < meshes.Length; i++)
            {
                ctx.cmd.DrawMesh(meshes[meshDistancesIndex[i]], Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one), meshMaterial, 0, 0);
            }
        }

        if (reprojection)
        {
            CustomPassUtils.Copy(ctx, volumetricTarget, volumetricTargetRead);
        
            var proj = ctx.hdCamera.camera.nonJitteredProjectionMatrix * ctx.hdCamera.camera.transform.worldToLocalMatrix;
            var projInverse = proj.inverse;
            reprojectCompute.SetMatrix("_ViewProject", proj);
            reprojectCompute.SetMatrix("_InvViewProject", projInverse);
            reprojectCompute.SetMatrix("_LastViewProject", lastViewProject);
            reprojectCompute.SetMatrix("_LastInvViewProject", lastViewProjectInverse);
            reprojectCompute.SetVectorArray("_LastCamFrustrum", lastFrustrum);
            reprojectCompute.SetVector("_UpdateOffset", offset);
            lastFrustrum = currentFrustrum;
            reprojectCompute.SetVectorArray("_CamFrustrum", lastFrustrum);
            reprojectCompute.SetFloat("_Downscale", pixelsAmount);
            reprojectCompute.SetVector("_LastWorldSpaceCameraPos", lastCameraPosition);
            
            ctx.cmd.DispatchCompute(reprojectCompute, 0, 1 + volumetricTarget.rt.width / 8, 1 + volumetricTarget.rt.height / 8, volumetricTarget.rt.mipmapCount);

            lastViewProject = proj;
            lastViewProjectInverse = projInverse;
            lastCameraPosition = ctx.hdCamera.camera.transform.position;
        }
        HDUtils.DrawFullScreen(ctx.cmd, combineMaterial, ctx.cameraColorBuffer, null, 0);
    }

    // Releases the GPU memory allocated for the half-resolution target. This is important otherwise the memory will leak.
    protected override void Cleanup()
    {
        Debug.Log("Cleanup");
        volumetricTarget.Release();
        volumetricTargetRead.Release();
        meshDepth.Release();
        reprojectionTarget.Release();
        CoreUtils.Destroy(combineMaterial);
        CoreUtils.Destroy(reprojectCompute);
        CoreUtils.Destroy(rendererCompute);
    }    
}