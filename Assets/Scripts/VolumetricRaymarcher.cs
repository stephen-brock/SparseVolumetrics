using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[ExecuteAlways]
public class VolumetricRaymarcher : MonoBehaviour
{
    [SerializeField, Space] private Material raymarcher;
    [SerializeField] private Camera cam;
    [SerializeField] private Light sun;
    [SerializeField] private float sunIntensity = 2;
    [SerializeField] private Color ambientColour = Color.white;
    [Header("Bounds")]
    [SerializeField] private float minHeight = 500;
    [SerializeField] private float maxHeight = 500;
    [SerializeField] private float width = 2000;

    [Header("Marching Parameters")] [SerializeField]
    private float bufferDistance = 10;
    
    [SerializeField, Range(-1, 1)] private float phase = 0.8f;
    [SerializeField, Range(-1, 1)] private float phase2 = 0.4f;
    [Header("Density Parameters")] [SerializeField]
    private Texture3D densityMap;
    [SerializeField] private Texture3D detailDensityMap;
    [SerializeField] private float density = 1;
    [SerializeField] private float lightDensity = 1;
    [SerializeField] private Vector3 scale;
    [SerializeField] private float detailAmount = -0.25f;
    [SerializeField] private float detailScale = 0.05f;

    private void Start()
    {
        UpdateVariables();
    }

    private void Update()
    {
        raymarcher.SetVector("_SunDirection", -sun.transform.forward);
        raymarcher.SetVector("_SunColour", sun.color * sunIntensity );
    }

    private void OnValidate()
    {
        UpdateVariables();
    }

    private void UpdateVariables()
    {
        cam.depthTextureMode |= DepthTextureMode.Depth;
        raymarcher.SetTexture("_DensityMap", densityMap);
        raymarcher.SetTexture("_DetailDensityMap", detailDensityMap);
        raymarcher.SetFloat("_Density", density);
        raymarcher.SetFloat("_LightDensity", lightDensity);
        raymarcher.SetVector("_Scale", scale);
        raymarcher.SetVector("_AmbientColour", ambientColour);
        raymarcher.SetFloat("_DetailScale", detailScale);
        raymarcher.SetFloat("_DetailAmount", detailAmount);
        raymarcher.SetFloat("_Phase", phase);
        raymarcher.SetFloat("_Phase2", phase2);
        raymarcher.SetFloat("_BufferDistance", bufferDistance);
        raymarcher.SetFloat("_MinHeight", minHeight);
        raymarcher.SetFloat("_MaxHeight", maxHeight);
        raymarcher.SetFloat("_Width", width);
        raymarcher.SetVector("_SunDirection", -sun.transform.forward);
        raymarcher.SetVector("_SunColour", sun.color * sunIntensity);
    }

}
