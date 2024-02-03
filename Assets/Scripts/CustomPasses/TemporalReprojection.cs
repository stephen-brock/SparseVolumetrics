using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class TemporalReprojection
{
    private ComputeShader reprojectCompute;
    private RTHandle reprojectionTarget;
    private RTHandle volumetricTargetRead;

    private VolumetricParams volumetricParams;

    private Matrix4x4 lastViewProject;
    private Matrix4x4 lastViewProjectInverse;

    private Vector4[] lastFrustrum;
    private Vector3 lastCameraPosition;

    private int pixelsAmount;
    public int PixelsAmount => pixelsAmount;

    private int counter = 0;
    
    private readonly int[][] dither = new int[][] {new [] {0}, new []{0, 2, 3, 1}, new [] {0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5}};

    public TemporalReprojection(ComputeShader reprojectComputeShader, ComputeShader rendererCompute, VolumetricParams volumetricParams, RTHandle volumetricTarget)
    {
        this.reprojectCompute = Object.Instantiate(reprojectComputeShader);
        this.volumetricParams = volumetricParams;
        pixelsAmount = (int)Mathf.Pow(2, volumetricParams.downsample);
        
        reprojectionTarget = RTHandles.Alloc(
            Vector2.one / Mathf.Pow(2, volumetricParams.downsample), TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R32_SFloat,
            useDynamicScale: true, name: "Reprojection Depth", enableRandomWrite:true
        );
        
        volumetricTargetRead = RTHandles.Alloc(
            Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            useDynamicScale: true, name: "Volumetrics Target", enableRandomWrite:true
        );
        reprojectCompute.SetFloat("_CloudHeight", volumetricParams.minHeight);
        reprojectCompute.SetTexture(0, "_ReprojectionPositions", reprojectionTarget);
        reprojectCompute.SetTexture(0, "_Read", volumetricTargetRead);
        reprojectCompute.SetTexture(0, "_Result", volumetricTarget);
        
        rendererCompute.SetTexture(0, "_ReprojectionPositions", reprojectionTarget);
        
        volumetricParams.SetVariables(reprojectCompute);
    }

    public Vector4 GetPixelOffset()
    {
        int index = dither[volumetricParams.downsample][counter++ % (pixelsAmount * pixelsAmount)];
        return new Vector4(index % pixelsAmount, (int)(index % (pixelsAmount * pixelsAmount) / pixelsAmount));
    }

    public void Execute(CustomPassContext ctx, RTHandle volumetricTarget, Vector4[] currentFrustrum)
    {
        var offset = GetPixelOffset();
        counter++;
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

    public void Cleanup()
    {
        reprojectionTarget.Release();
        volumetricTargetRead.Release();
        CoreUtils.Destroy(reprojectCompute);
    }
}
