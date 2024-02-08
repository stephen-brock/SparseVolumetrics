using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = System.Object;

public class CoverageTest : MonoBehaviour
{
    [SerializeField] private Texture3D texture;
    [SerializeField] private bool test;

    [SerializeField] private Texture3D compTexture;
    [SerializeField] private bool compare;

    private void OnValidate()
    {
        if (test)
        {
            test = false;

            Texture3D copy = new Texture3D(texture.width, texture.height, texture.width, GraphicsFormat.R8_UNorm,
                TextureCreationFlags.DontInitializePixels);
            
            Graphics.CopyTexture(texture, copy);
            
            Color[] colours = copy.GetPixels();

            double sum = 0;
            int threshSum = 0;

            for (int i = 0; i < colours.Length; i++)
            {
                sum += colours[i].r;
                threshSum += colours[i].r > 0 ? 1 : 0;
            }
            
            Debug.Log($"Average Density: {(sum / colours.Length)}, Average Coverage: {threshSum / (double)colours.Length}");

            CoreUtils.Destroy(copy);
        }

        if (compare)
        {
            compare = false;
            Texture3D copy = new Texture3D(texture.width, texture.height, texture.width, GraphicsFormat.R8_UNorm,
                TextureCreationFlags.DontInitializePixels);
            
            Graphics.CopyTexture(texture, copy);
            
            Color[] colours = copy.GetPixels();

            Texture3D copy2 = new Texture3D(texture.width, texture.height, texture.width, GraphicsFormat.R8_UNorm,
                TextureCreationFlags.DontInitializePixels);
            
            Graphics.CopyTexture(compTexture, copy2);
            
            Color[] colours2 = copy2.GetPixels();

            double mse = 0;
            
            for (int i = 0; i < colours.Length; i++)
            {
                mse += Mathf.Pow(colours[i].r - colours2[i].r, 2);
            }

            if (mse == 0)
            {
                Debug.Log($"PSNR: 100 DB");
                return;
            }
            
            Debug.Log($"PSNR: {10 * Math.Log10(1.0 / Math.Sqrt(mse / colours.Length))} DB");
        }
    }
}
