using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class VolumeToBricks : MonoBehaviour
{
    [SerializeField] private Texture3D tex;
    [SerializeField] private ComputeShader bricker;
    [SerializeField] private int brickSize = 8;

    [SerializeField] private bool brick;
    [SerializeField] private string saveName;
    [SerializeField] private RenderTexture output;
    [SerializeField] private RenderTexture outputBricks;
    
    private void OnValidate()
    {
        if (brick)
        {
            brick = false;
            if (output != null)
            {
                output.Release();
                output = null;
                outputBricks.Release();
                outputBricks = null;
            }
            
            ComputeBuffer appendBuffer =
                new ComputeBuffer(tex.width * tex.height * tex.depth / (brickSize * brickSize * brickSize),
                    sizeof(int) * 3, ComputeBufferType.Append);
            ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

            
            appendBuffer.SetCounterValue(0);
            countBuffer.SetCounterValue(0);

            bricker.SetInt("_BrickSize", brickSize);
            bricker.SetTexture(0, "_Bricks", tex);
            bricker.SetBuffer(0, "_BrickMap", appendBuffer);
            
            bricker.Dispatch(0, (tex.width / brickSize) / 4, (tex.height / brickSize) / 4, (tex.depth / brickSize) / 4);

            ComputeBuffer.CopyCount(appendBuffer, countBuffer, 0);
            int[] counter = new int[1] { 0 };
            countBuffer.GetData(counter);
            int count = (counter[0]);

            // int width = Mathf.CeilToInt(Mathf.Pow(count, 1.0f / 3.0f));
            RenderTexture map = new RenderTexture(tex.width / brickSize, tex.height / brickSize, 0, GraphicsFormat.R16G16B16A16_SNorm);
            map.dimension = TextureDimension.Tex3D;
            map.volumeDepth = tex.depth / brickSize;
            map.enableRandomWrite = true;
            map.Create();
            
            int widthBricks = Mathf.CeilToInt(Mathf.Pow(count * brickSize * brickSize * brickSize, 1.0f / 3.0f));
            RenderTexture bricks = new RenderTexture(widthBricks, widthBricks, 0, GraphicsFormat.R8_UNorm);
            bricks.dimension = TextureDimension.Tex3D;
            bricks.volumeDepth = widthBricks;
            bricks.enableRandomWrite = true;
            bricks.Create();

            bricker.SetBuffer(1, "_UnbuiltBricks", appendBuffer);
            bricker.SetTexture(1, "_Map", map);
            bricker.SetTexture(1, "_Bricks", tex);
            bricker.SetTexture(1, "_BricksBuilt", bricks);
            bricker.SetInt("_Count", count);
            bricker.Dispatch(1, count, 1, 1);
            
            output = map;
            outputBricks = bricks;
            
            SaveRT3DToTexture3DAsset(map, saveName + "_map");
            SaveRT3DToTexture3DAsset2(bricks, saveName + "_bricks");
            
            appendBuffer.Dispose();
            countBuffer.Dispose();
        }
    }

    struct rgb
    {
        private float r, g, b,a;
    }
    
    //https://forum.unity.com/threads/rendertexture-3d-to-texture3d.928362/
    void SaveRT3DToTexture3DAsset(RenderTexture rt3D, string pathWithoutAssetsAndExtension)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<rgb>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
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
    //https://forum.unity.com/threads/rendertexture-3d-to-texture3d.928362/
    void SaveRT3DToTexture3DAsset2(RenderTexture rt3D, string pathWithoutAssetsAndExtension)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<float>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
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
}
