using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class ParameterTesting : MonoBehaviour
{
    [SerializeField] private CustomPassVolume volume;
    [SerializeField] private string id = "test_";
    [SerializeField] private string paramId = "_TransmissionThreshold";
    [SerializeField] private float fromValue = 0;
    [SerializeField] private float toValue = 0.25f;
    [SerializeField] private int numberOfValues = 20;
    [SerializeField] private int throwawayIterations = 10;
    [SerializeField] private bool exp = false;
    [SerializeField] private bool constStep = false;
    [SerializeField] private bool printValues = false;
    
    [SerializeField] private int frameDelay = 200;
    [SerializeField] private int wait = 50;

    private void OnValidate()
    {
        if (printValues)
        {
            printValues = false;
            float value = fromValue;
            for (int i = 0; i < numberOfValues; i++)
            {
                value *= toValue;
                print(value);
            }
        }
    }

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
        string write = "";
        write += (1000.0 * time / frameDelay) + ", ";
        string values = "-1" + ", ";
        yield return null;


        float value = fromValue;
        
        
        for (int i = 0; i < throwawayIterations; i++)
        {
            RaymarcherPass pass = (RaymarcherPass)volume.customPasses[0];
            
            pass.SetParameter(paramId, value);
            volume.enabled = false;
            yield return null;
            volume.enabled = true;
            for (int j = 0; j < wait; j++)
            {
                yield return null;
            }
            time = Time.unscaledTimeAsDouble;
            yield return null;
            for (int j = 0; j < frameDelay; j++)
            {
                yield return null;
            }

            time = Time.unscaledTimeAsDouble - time;
            for (int j = 0; j < wait; j++)
            {
                yield return null;
            }
        }
        
        for (int i = 0; i < numberOfValues; i++)
        {
            RaymarcherPass pass = (RaymarcherPass)volume.customPasses[0];
            if (exp)
            {
                value *= toValue;
            }
            else if (constStep)
            {
                value += toValue;
            }
            else
            {
                value = Mathf.Lerp(fromValue, toValue, (float)i / (numberOfValues));
            }

            values += value + ", ";
            pass.SetParameter(paramId, value);
            volume.enabled = false;
            yield return null;
            volume.enabled = true;
            for (int j = 0; j < wait; j++)
            {
                yield return null;
            }
            time = Time.unscaledTimeAsDouble;
            yield return null;
            for (int j = 0; j < frameDelay; j++)
            {
                yield return null;
            }

            time = Time.unscaledTimeAsDouble - time;
            write += (1000.0 * time / frameDelay) + ", ";
            Debug.Log($"{value} screenshot taken, average frametime {1000.0 * time / frameDelay}");
            
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, $"{id}_{value}.png"));
            for (int j = 0; j < wait; j++)
            {
                yield return null;
            }
        }

        Debug.Log("Done");
        
        File.WriteAllText(Path.Combine(Application.persistentDataPath, $"{id}.csv"), values + "\n" + write);
        
        for (int j = 0; j < frameDelay; j++)
        {
            yield return null;
        }

        Application.Quit();

    }
}
