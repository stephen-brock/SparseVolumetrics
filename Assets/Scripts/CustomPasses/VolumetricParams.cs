using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

[CreateAssetMenu()]
public class VolumetricParams : ScriptableObject
{
    [Header("Visual")]
    public Color ambientColour = Color.white;

    public float ambientMult = 0.001f;
    public Color totalAmbientColour = Color.white;
    [Range(-1, 1)] public float phase = 0.8f;
    [Range(-1, 1)] public float phase2 = 0.4f;
    [Header("Bounds")]
    public float minHeight = 500;
    public float maxHeight = 500;
    public float width = 2000;

    [Header("Marching Parameters")] 
    public float bufferDistance = 10;
    public int downsample = 0;
    [Header("Density Parameters")] 
    public Texture3D densityMap;
    public Texture3D detailDensityMap;
    public float density = 1;
    // public float lightDensity = 1;
    // public Vector3 scale;
    public float detailAmount = -0.25f;
    public float detailScale = 0.05f;

    public void SetVariables(ComputeShader rendererCompute)
    {
        rendererCompute.SetTexture(0, "_DensityMap", densityMap);
        rendererCompute.SetTexture(0, "_DetailDensityMap", detailDensityMap);
        rendererCompute.SetFloat("_Density", density);
        // rendererCompute.SetFloat("_LightDensity", lightDensity);
        // rendererCompute.SetVector("_Scale", scale);
        rendererCompute.SetVector("_AmbientColour", ambientColour * ambientMult);
        rendererCompute.SetVector("_TotalAmbientColour", totalAmbientColour);
        rendererCompute.SetFloat("_DetailScale", detailScale);
        rendererCompute.SetFloat("_DetailAmount", detailAmount);
        rendererCompute.SetFloat("_Phase", phase);
        rendererCompute.SetFloat("_Phase2", phase2);
        rendererCompute.SetFloat("_BufferDistance", bufferDistance);
        rendererCompute.SetFloat("_MinHeight", minHeight);
        rendererCompute.SetFloat("_MaxHeight", maxHeight);
        rendererCompute.SetFloat("_Width", width);
    }

    public void SetVariables(Material meshMaterial)
    {
        meshMaterial.SetFloat("_Density", density);
        // meshMaterial.SetFloat("_LightDensity", lightDensity);
        // meshMaterial.SetVector("_Scale", scale);
        meshMaterial.SetVector("_AmbientColour", ambientColour * ambientMult);
        meshMaterial.SetVector("_TotalAmbientColour", totalAmbientColour);
        meshMaterial.SetFloat("_DetailScale", detailScale);
        meshMaterial.SetFloat("_DetailAmount", detailAmount);
        meshMaterial.SetFloat("_Phase", phase);
        meshMaterial.SetFloat("_Phase2", phase2);
        meshMaterial.SetFloat("_BufferDistance", bufferDistance);
        meshMaterial.SetFloat("_MinHeight", minHeight);
        meshMaterial.SetFloat("_MaxHeight", maxHeight);
        meshMaterial.SetFloat("_Width", width);
        meshMaterial.SetTexture("_DensityMap", densityMap);
        meshMaterial.SetTexture("_DetailDensityMap", detailDensityMap);
        meshMaterial.SetFloat("_Downsample", Mathf.Pow(2, downsample));
    }
    
    
    
}
