using System;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

class VolumetricMeshPass : RaymarcherPass
{
    [SerializeField] private ComputeShader reprojectComputeShader;
    [SerializeField] private ComputeShader volumetricCombine;

    [SerializeField] private VolumetricParams volumetricParams;
    
    [Header("Settings")]
    
    [SerializeField] private Light sun;
    [SerializeField] private float sunIntensity = 2;

    [SerializeField] private bool reprojection;

    [SerializeField] private Texture3D sdfMap;
    [SerializeField] private float sdfMarchDistance = 50;

    [SerializeField] private Mesh[] meshes;
    [SerializeField] private GameObject autoParent;
    [SerializeField] private bool runAuto = false;
    [SerializeField] private Material meshMaterial;

    [SerializeField] private bool useCamera;
    [SerializeField] private Camera cam;
    
    private RTHandle volumetricTarget;
    private RTHandle meshDepth;

    private ComputeShader combineCompute;
    private Mesh[] meshesSorted;
    private float[] meshDistances;
    private int[] meshDistancesIndex;
    private int[] subsetIndicies;
    private Matrix4x4[] meshMatricies;

    private TemporalReprojection temporalReprojection;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        volumetricTarget = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, volumetricParams.downsample), TextureXR.slices, dimension: TextureXR.dimension, 
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            useDynamicScale: true, name: "Volumetrics Target", enableRandomWrite:true, filterMode: FilterMode.Bilinear
        );
        
        meshDepth = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, volumetricParams.downsample), TextureXR.slices, dimension: TextureXR.dimension, depthBufferBits: DepthBits.Depth32,
            colorFormat: GraphicsFormat.None,
            useDynamicScale: true, name: "Volumetrics Target", enableRandomWrite:false
        );


        combineCompute = Object.Instantiate(volumetricCombine);
        
        
        if (reprojection)
        {
            if (temporalReprojection != null)
            {
                temporalReprojection.Cleanup();
            }

            temporalReprojection = new TemporalReprojection(reprojectComputeShader, reprojectComputeShader, volumetricParams, volumetricTarget);
            
        }
        
        // rendererCompute.SetKeyword(new LocalKeyword(rendererCompute, "REPROJECT"), reprojection);

        meshesSorted = new Mesh[meshes.Length];
        meshDistances = new float[meshes.Length];
        meshDistancesIndex = new int[meshes.Length];
        meshMatricies = new Matrix4x4[meshes.Length];
        subsetIndicies = new int[meshes.Length];
        for (int i = 0; i < meshes.Length; i++)
        {
            meshMatricies[i] = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
            subsetIndicies[i] = 0;
        }
        
        UpdateVariables();
        Debug.Log("Initialise");

        autoParent.SetActive(useCamera);
        cam.gameObject.SetActive(useCamera);
        
        if (useCamera)
        {
            cam.targetTexture = volumetricTarget;
        }
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
        
        combineCompute.SetTexture(0, "_Volumetrics", volumetricTarget);
        combineCompute.SetFloat("_ScreenScale", 1.0f / Mathf.Pow(2, volumetricParams.downsample));
        
        meshMaterial.SetFloat("_SDFMarchDistance", Mathf.Max(10, sdfMarchDistance));
        meshMaterial.SetVector("_SunDirection", -sun.transform.forward);
        meshMaterial.SetVector("_SunColour", sun.color * sunIntensity);
        
        volumetricParams.SetVariables(meshMaterial);
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
        // UpdateVariables();
        var currentFrustrum = FrustumCorners(ctx.hdCamera.camera);

        int pixelsAmount = (int)Mathf.Pow(2, volumetricParams.downsample);
        Vector4 offset = Vector4.zero;
        if (reprojection)
        {
            offset = temporalReprojection.GetPixelOffset();
        }

        if (!useCamera)
        {
            CustomPassUtils.Copy(ctx, ctx.cameraDepthBuffer, meshDepth);
            CoreUtils.SetRenderTarget(ctx.cmd, volumetricTarget, meshDepth, ClearFlag.Color, Color.clear);
            for (int i = 0; i < meshes.Length; i++)
            {
                meshDistances[i] = -(ctx.hdCamera.camera.transform.position - meshes[i].bounds.center).sqrMagnitude;
                meshDistancesIndex[i] = i;
            }
            Array.Sort(meshDistances, meshes);
            // for (int i = 0; i < meshes.Length; i++)
            // {
            //     meshesSorted[i] = meshes[meshDistancesIndex[i]];
            // }
            ctx.cmd.DrawMultipleMeshes(meshMatricies, meshes, subsetIndicies, meshes.Length, meshMaterial, 0, new MaterialPropertyBlock());
            // for (int i = 0; i < meshes.Length; i++)
            // {
            //     ctx.cmd.DrawMesh(meshes[i], Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one), meshMaterial, 0, 0);
            // }
        }

        if (reprojection)
        {
            temporalReprojection.Execute(ctx, volumetricTarget, currentFrustrum);
        }
        combineCompute.SetTexture(0, "_Result", ctx.cameraColorBuffer);
        ctx.cmd.DispatchCompute(combineCompute, 0, 1 + ctx.hdCamera.actualWidth / 8, 1 + ctx.hdCamera.actualWidth / 8, 1);
    }

    // Releases the GPU memory allocated for the half-resolution target. This is important otherwise the memory will leak.
    protected override void Cleanup()
    {
        Debug.Log("Cleanup");
        if (cam != null)
        {
            cam.targetTexture = null;
        }
        volumetricTarget.Release();
        RTHandles.Release(volumetricTarget);
        meshDepth.Release();
        CoreUtils.Destroy(combineCompute);

        temporalReprojection?.Cleanup();

        autoParent.SetActive(false);
        cam.gameObject.SetActive(false);
    }

    public override void SetVolumetricParams(VolumetricParams volumeParams)
    {
        volumetricParams = volumeParams;
        UpdateVariables();
    }
}