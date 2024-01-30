using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class VolumetricParams : ScriptableObject
{
    [Header("Visual")]
    public Color ambientColour = Color.white;
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
    public float lightDensity = 1;
    public Vector3 scale;
    public float detailAmount = -0.25f;
    public float detailScale = 0.05f;
}
