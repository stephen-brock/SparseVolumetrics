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
    [SerializeField] private int wait = 50;

    private IEnumerator Start()
    {
        yield return null;
        volume.enabled = false;
        yield return new WaitForSeconds(5);
        for (int i = 0; i < wait; i++)
        {
            yield return null;
        }
        

        double time = Time.unscaledTimeAsDouble;
        for (int i = 0; i < frameDelay; i++)
        {
            yield return null;
        }
        time = Time.unscaledTimeAsDouble - time;
        Debug.Log($"None screenshot taken, average frametime {1000.0 * time / frameDelay}");
            
        ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, $"{id}_none.png"));
        for (int i = 0; i < wait; i++)
        {
            yield return null;
        }
        volume.enabled = true;
        yield return null;

        for (int i = 0; i < volumetricParameters.Length; i++)
        {
            RaymarcherPass pass = (RaymarcherPass)volume.customPasses[0];
            pass.SetVolumetricParams(volumetricParameters[i]);
            volume.enabled = false;
            yield return null;
            volume.enabled = true;
            for (int j = 0; j < wait; j++)
            {
                yield return null;
            }
            time = Time.unscaledTimeAsDouble;
            for (int j = 0; j < frameDelay; j++)
            {
                yield return null;
            }

            time = Time.unscaledTimeAsDouble - time;
            Debug.Log($"{volumetricParameters[i].name} screenshot taken, average frametime {1000.0 * time / frameDelay}");
            
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, $"{id}_{volumetricParameters[i].name}.png"));
            for (int j = 0; j < wait; j++)
            {
                yield return null;
            }
        }

        Debug.Log("Done");
        for (int j = 0; j < frameDelay; j++)
        {
            yield return null;
        }

        Application.Quit();

    }
}
