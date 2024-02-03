using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CopyDepth : MonoBehaviour
{
    [SerializeField] private Camera cam;

    private void Start()
    {
        var tex = GetComponent<Camera>().targetTexture;
        GetComponent<Camera>().SetTargetBuffers(tex.colorBuffer, cam.targetTexture.depthBuffer);
    }
}
