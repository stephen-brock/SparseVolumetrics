using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class VolumeToBricks : MonoBehaviour
{
    [SerializeField] private Texture3D tex;
    [SerializeField] private ComputeShader bricker;
    [SerializeField] private int brickSize = 8;

    [SerializeField] private bool brick;
    
    private void OnValidate()
    {
        if (brick)
        {
            RenderTexture map = new RenderTexture(tex.width / brickSize, tex.height / brickSize, 0,
                GraphicsFormat.R16G16B16_UInt);
            map.dimension = TextureDimension.Tex3D;
            map.volumeDepth = tex.depth / brickSize;

            bricker.SetInt("_BrickSize", brickSize);
            bricker.SetTexture(0, "_Bricks", tex);
            bricker.SetTexture(0, "_BrickMap", map);
        }
    }
}
