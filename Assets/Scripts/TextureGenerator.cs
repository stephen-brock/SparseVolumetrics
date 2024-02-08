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
    [SerializeField, Header("Parameters")] private int width = 512;
    [SerializeField] private int height = 128;
    [SerializeField] private Vector3 cellsPerWidth = Vector3.one;
    [Space] [SerializeField] private int octaves = 4;
    [SerializeField] private float persistance = 0.75f;
    [SerializeField] private float lacunarity = 0.5f;
    [SerializeField] private float mult = 0.4f;
    [SerializeField] private bool compress = false;

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
            if (output != null)
            {
                output.Release();
            }
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
            #if UNITY_EDITOR
            AssetDatabase.CreateAsset(output, $"Assets/{pathWithoutAssetsAndExtension}.asset");
            AssetDatabase.SaveAssetIfDirty(output);
            #endif
            a.Dispose();
            rt3D.Release();
        });
    }


    private RenderTexture GenerateTexture()
    {
        RenderTexture tex = new RenderTexture(width, height, 0, GraphicsFormat.R8_UNorm);
        tex.dimension = TextureDimension.Tex3D;
        tex.volumeDepth = width;
        tex.enableRandomWrite = true;
        tex.Create();
        
        
        RenderTexture texa = new RenderTexture(width, height, 0, GraphicsFormat.R32_SFloat);
        texa.dimension = TextureDimension.Tex3D;
        texa.volumeDepth = width;
        texa.enableRandomWrite = true;
        texa.Create();
        
        RenderTexture texb = new RenderTexture(texa.descriptor);
        texb.enableRandomWrite = true;
        texb.Create();

        Vector3 currentCPW = cellsPerWidth;
        float amplitude = 1;
        for (int i = 0; i < octaves; i++)
        {
            textureCompute.SetFloat("_Amplitude", amplitude);
            textureCompute.SetVector("_CellsPerWidth", currentCPW);
            textureCompute.SetTexture(0, "_Input", texa);
            textureCompute.SetTexture(0, "_Output", texb);
            textureCompute.Dispatch(0, width / 4, height / 4, width / 4);
            amplitude *= persistance;
            currentCPW = (currentCPW / lacunarity);
            currentCPW = new Vector3(Mathf.Floor(currentCPW.x), Mathf.Floor(currentCPW.y), Mathf.Floor(currentCPW.z));
            var temp = texb;
            texb = texa;
            texa = temp;
        }

        textureCompute.SetFloat("_Mult", mult);
        textureCompute.SetTexture(1, "_Input", texa);
        textureCompute.SetTexture(1, "_Output", tex);
        textureCompute.Dispatch(1, width / 4, height / 4, width / 4);

        texa.Release();
        texb.Release();
        
        return tex;
    }
}
