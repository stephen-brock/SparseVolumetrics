using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class Compress3D : MonoBehaviour
{
    [SerializeField] private Texture3D[] compressArr;
    [SerializeField] private bool compress;

    private void OnValidate()
    {
        if (compress)
        {
            compress = false;

            foreach (var t in compressArr)
            {
                CompressTexture(t);
            }
        }
    }

    private void CompressTexture(Texture3D toCompress)
    {
        Texture3D newTex = new Texture3D(toCompress.width, toCompress.height, toCompress.depth, GraphicsFormat.R_BC4_UNorm, TextureCreationFlags.None);

        for (int i = 0; i < toCompress.depth; i++)
        {
            Texture2D texture = new Texture2D(toCompress.width, toCompress.height, GraphicsFormat.R8_UNorm, TextureCreationFlags.None);
            Graphics.CopyTexture(toCompress, i, texture, 0);
            EditorUtility.CompressTexture(texture, TextureFormat.BC4, TextureCompressionQuality.Best);
            Graphics.CopyTexture(texture, 0, newTex, i);
            CoreUtils.Destroy(texture);
        }
            
        SaveTexture(newTex, toCompress.name + "_c");
        CoreUtils.Destroy(toCompress);
        CoreUtils.Destroy(newTex);
        print("Done");
    }

    private void SaveTexture(Texture3D output, string pathWithoutAssetsAndExtension)
    {
    #if UNITY_EDITOR
            AssetDatabase.CreateAsset(output, $"Assets/{pathWithoutAssetsAndExtension}.asset");
            AssetDatabase.SaveAssetIfDirty(output);
    #endif
    }
}
