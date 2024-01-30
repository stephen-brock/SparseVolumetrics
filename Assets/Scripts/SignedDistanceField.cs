using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class SignedDistanceField : MonoBehaviour
{
    [SerializeField] private ComputeShader shader;
    [SerializeField] private RenderTexture output;
    [Space, SerializeField] private int iterations = 16;
    [SerializeField] private Texture3D input;
    [SerializeField] private float decay = 0.8f;
    [Space, SerializeField] private bool run;
    [SerializeField] private bool runAndSave;
    [SerializeField] private bool runAndSaveCombined;
    [SerializeField] private string name;

    RenderTexture GenerateTexture()
    {
        RenderTexture temp1 = new RenderTexture(input.width, input.height, 0, GraphicsFormat.R8_UNorm);
        temp1.dimension = TextureDimension.Tex3D;
        temp1.volumeDepth = input.depth;
        temp1.enableRandomWrite = true;
        temp1.Create();

        RenderTexture temp2 = new RenderTexture(temp1.descriptor);
        temp2.Create();

        shader.SetFloat("_Decay", decay);

        for (int i = 0; i < iterations; i++)
        {
            shader.SetTexture(1, "_Input", input);
            shader.SetTexture(1, "_Result", temp1);
            shader.Dispatch(1, 1 + input.width / 4, 1 + input.height / 4, 1 + input.depth / 4);
                
            shader.SetTexture(0, "_Input", temp1);
            shader.SetTexture(0, "_Result", temp2);
            shader.Dispatch(0, 1 + input.width / 4, 1 + input.height / 4, 1 + input.depth / 4);

            var tmp = temp2;
            temp2 = temp1;
            temp1 = tmp;
        }
        shader.SetTexture(1, "_Input", input);
        shader.SetTexture(1, "_Result", temp1);
        shader.Dispatch(1, 1 + input.width / 4, 1 + input.height / 4, 1 + input.depth / 4);
            
        temp2.Release();
        return temp1;
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

    struct TwoByte
    {
        private byte r;
        private byte g;
    }
    
    //https://forum.unity.com/threads/rendertexture-3d-to-texture3d.928362/
    void SaveRT3DToTexture3DAssetRG(RenderTexture rt3D, string pathWithoutAssetsAndExtension)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<TwoByte>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
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

        if (runAndSaveCombined)
        {
            runAndSaveCombined = false;
            var tex = GenerateTexture();
            var comb = new RenderTexture(tex.descriptor);
            comb.graphicsFormat = GraphicsFormat.R8G8_UNorm;
            comb.enableRandomWrite = true;
            comb.Create();
            shader.SetTexture(2, "_Input", input);
            shader.SetTexture(2, "_SDF", tex);
            shader.SetTexture(2, "_Combined", comb);
            shader.Dispatch(2, 1 + input.width / 4, 1 + input.height / 4, 1 + input.depth / 4);
            SaveRT3DToTexture3DAssetRG(comb, name);
            tex.Release();
            comb.Release();
        }
    }
}
