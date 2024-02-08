using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.HighDefinition;
using Random = UnityEngine.Random;

public class CloudTesting : MonoBehaviour
{
    [SerializeField] private CustomPassVolume volume;
    [SerializeField] private VolumetricParams[] volumetricParameters;
    [SerializeField] private Transform camera;
    [SerializeField] private string id = "test_";

    [SerializeField] private int frameDelay = 200;
    [SerializeField] private Vector3 fromPosition;
    [SerializeField] private Vector3 toPosition;
    [SerializeField] private int numberOfFrames = 1000;
    
    private const int MaxFrames = 32;

    int index = 0;
    private IEnumerator Start()
    {
        // Screen.SetResolution(1920, 1080, FullScreenMode.ExclusiveFullScreen);
        while (true)
        {
            yield return null;
            if (index == volumetricParameters.Length)
            {
                index = -1;
                volume.enabled = false;
            }
            else
            {
                volume.enabled = true;
                yield return null;
                RaymarcherPass pass = (RaymarcherPass)volume.customPasses[0];
                pass.SetVolumetricParams(volumetricParameters[index]);
            }
            yield return null;
            yield return RunTest();
            
            index++;
        }
    }

    private IEnumerator RunTest()
    {
        camera.position = fromPosition;
        
        for (int i = 0; i < frameDelay; i++)
        {
            yield return null;
        }

        double time = Time.unscaledTimeAsDouble;
        // ProfilerRecorder mainRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        // ProfilerRecorder renderRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "_VolumetricPass", options: ProfilerRecorderOptions.GpuRecorder | ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        //
        // double cpuSum = 0;
        // double gpuSum = 0;
        //
        // FrameTiming[] buffer = new FrameTiming[MaxFrames];
        // List<FrameTiming> timings = new List<FrameTiming>();
        
        
        for (int i = 0; i < numberOfFrames; i++)
        {
            camera.position = Vector3.Lerp(fromPosition, toPosition, (float) i / (numberOfFrames - 1));
            // cpuSum += mainRecorder.LastValueAsDouble / (1000 * 1000);
            // gpuSum += renderRecorder.LastValueAsDouble / (1000 * 1000);
            //
            // if (i % (numberOfFrames / MaxFrames) == 0)
            // {
            //     FrameTimingManager.CaptureFrameTimings();
            //     uint amount = FrameTimingManager.GetLatestTimings(MaxFrames, buffer);
            //     for (int j = 0; j < amount; j++)
            //     {
            //         timings.Add(buffer[j]);
            //     }
            // }
            yield return null;
        }
        
        Debug.Log($"AVERAGE Frame Time: {1000.0f * (Time.unscaledTimeAsDouble - time) / numberOfFrames}");

        
        // Debug.Log($"AVERAGE CPU: {cpuSum / numberOfFrames}");
        // Debug.Log($"AVERAGE RENDER: {gpuSum / numberOfFrames}");
        //
        // cpuSum = 0;
        // gpuSum = 0;
        // for (int i = 0; i < timings.Count; i++)
        // {
        //     cpuSum += timings[i].cpuFrameTime;
        //     gpuSum += timings[i].gpuFrameTime;
        // }
        //
        // Debug.Log($"AVERAGE CPU 2: {cpuSum / timings.Count}");
        // Debug.Log($"AVERAGE GPU 2: {gpuSum / timings.Count}");
        //
        // mainRecorder.Dispose();
        // renderRecorder.Dispose();
        
        WriteTimings(1000.0 * (Time.unscaledTimeAsDouble - time) / numberOfFrames);
        
        for (int i = 0; i < frameDelay; i++)
        {
            yield return null;
        }
    }

    private void WriteTimings(double time)
    {
        string paramsId = "";
        if (index >= 0)
        {
            paramsId = volumetricParameters[index].name;
        }
        else
        {
            paramsId = "none";
        }
        
        File.AppendAllText(Path.Combine(Application.persistentDataPath, $"{id}_{paramsId}.csv"), $", {time}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawSphere(fromPosition, 50);
        Gizmos.DrawSphere(toPosition, 50);
    }
}
