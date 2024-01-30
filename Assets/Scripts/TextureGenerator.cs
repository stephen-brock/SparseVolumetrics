using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class TextureGenerator : MonoBehaviour
{
    [SerializeField] private ComputeShader textureCompute;
    [SerializeField, ReadOnly] private RenderTexture output;
    [SerializeField, Header("Parameters")] private int width = 128;
    [SerializeField] private int cellsPerWidth = 8;
    [Space] [SerializeField] private int octaves = 4;
    [SerializeField] private float persistance = 0.75f;
    [SerializeField] private float lacunarity = 0.5f;

    [Space] [SerializeField] private bool run;
    [SerializeField] private bool runAndSave;
    [SerializeField] private string name;
    private void OnValidate()
    {
        if (run)
        {
            run = false;
            if (output != null)
            {
                output.Release();
            }
            output = GenerateTexture();
        }

        if (runAndSave)
        {
            runAndSave = false;
            var tex = GenerateTexture();
            SaveRT3DToTexture3DAsset(tex, name);
            tex.Release();
        }
    }
    
    //https://forum.unity.com/threads/rendertexture-3d-to-texture3d.928362/
    void SaveRT3DToTexture3DAsset(RenderTexture rt3D, string pathWithoutAssetsAndExtension)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<byte>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, 0, (_) =>
        {
            Texture3D output = new Texture3D(width, height, depth, rt3D.graphicsFormat, TextureCreationFlags.None);
            output.SetPixelData(a, 0);
            output.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            AssetDatabase.CreateAsset(output, $"Assets/{pathWithoutAssetsAndExtension}.asset");
            AssetDatabase.SaveAssetIfDirty(output);
            a.Dispose();
            rt3D.Release();
        });
    }


    private RenderTexture GenerateTexture()
    {
        RenderTexture tex = new RenderTexture(width, width, 0, RenderTextureFormat.R8);
        tex.dimension = TextureDimension.Tex3D;
        tex.volumeDepth = width;
        tex.enableRandomWrite = true;
        tex.Create();
        
        RenderTexture tex2 = new RenderTexture(tex.descriptor);
        tex2.enableRandomWrite = true;
        tex2.Create();

        int currentCPW = cellsPerWidth;
        float amplitude = 1;
        for (int i = 0; i < octaves; i++)
        {
            textureCompute.SetFloat("_Amplitude", amplitude);
            textureCompute.SetFloat("_CellsPerWidth", currentCPW);
            textureCompute.SetTexture(0, "_Input", tex);
            textureCompute.SetTexture(0, "_Output", tex2);
            textureCompute.Dispatch(0, width / 4, width / 4, width / 4);
            amplitude *= persistance;
            currentCPW = (int)(currentCPW / lacunarity);
            var temp = tex2;
            tex2 = tex;
            tex = temp;
        }

        tex2.Release();
        
        return tex;
    }
}
