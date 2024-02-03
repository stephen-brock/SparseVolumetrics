using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Screenshot : MonoBehaviour
{
    [SerializeField] private bool takeScreenshot;
    [SerializeField] private string id = "Screenshot.png";

    private void OnValidate()
    {
        if (takeScreenshot)
        {
            takeScreenshot = false;
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, id));
        }
    }
}
