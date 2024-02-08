using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEngine;

[ExecuteInEditMode]
public class Screenshot : MonoBehaviour
{
    [SerializeField] private bool takeScreenshot;
    [SerializeField] private string id = "Screenshot.png";

    [SerializeField] private float rollingMs;

    private void Update()
    {
        rollingMs = Mathf.Lerp(rollingMs, Time.unscaledDeltaTime * 1000.0f, 0.5f);
        if (takeScreenshot)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            GetComponent<Camera>().Render();
            sw.Stop();
            print("Render time: " + sw.Elapsed.TotalMilliseconds);
            takeScreenshot = false;
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, id));
        }
    }
}
