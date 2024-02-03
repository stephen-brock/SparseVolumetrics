using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class ScreenshotTesting : MonoBehaviour
{
    [SerializeField] private CustomPassVolume volume;
    [SerializeField] private VolumetricParams[] volumetricParameters;
    [SerializeField] private string id = "test_";

    [SerializeField] private int frameDelay = 200;

    private IEnumerator Start()
    {
        yield return null;
        volume.enabled = false;
        for (int i = 0; i < frameDelay; i++)
        {
            yield return null;
        }
        
        Debug.Log("None screenshot taken");
        ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, $"{id}_none.png"));
        for (int i = 0; i < frameDelay; i++)
        {
            yield return null;
        }
        volume.enabled = true;
        yield return null;

        for (int i = 0; i < volumetricParameters.Length; i++)
        {
            RaymarcherPass pass = (RaymarcherPass)volume.customPasses[0];
            pass.SetVolumetricParams(volumetricParameters[i]);
            for (int j = 0; j < frameDelay; j++)
            {
                yield return null;
            }
            Debug.Log($"{volumetricParameters[i].id} screenshot taken");
            
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, $"{id}_{volumetricParameters[i].id}.png"));
        }

        Debug.Log("Done");

    }
}
