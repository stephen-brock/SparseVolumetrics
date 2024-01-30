using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 1.0f;

    private float x, y;

    void Update()
    {
        x += -Input.GetAxis("Mouse Y") * Time.deltaTime * sensitivity;
        x = Mathf.Clamp(x, -89, 90);
        y += Input.GetAxis("Mouse X") * Time.deltaTime * sensitivity;
        transform.localEulerAngles = new Vector3(x, y, 0);
    }
}
