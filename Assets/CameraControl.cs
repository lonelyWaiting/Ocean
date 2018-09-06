using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour {

    private float xSpeed = 250.0f;
    private float ySpeed = 120.0f;

    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        //w键前进
        if (Input.GetKey(KeyCode.W))
        {
            this.gameObject.transform.Translate(new Vector3(0, 0, 200 * Time.deltaTime));
        }
        //s键后退
        if (Input.GetKey(KeyCode.S))
        {
            this.gameObject.transform.Translate(new Vector3(0, 0, -200 * Time.deltaTime));
        }
        //a键后退
        if (Input.GetKey(KeyCode.A))
        {
            this.gameObject.transform.Translate(new Vector3(-30, 0, 0 * Time.deltaTime));
        }
        //d键后退
        if (Input.GetKey(KeyCode.D))
        {
            this.gameObject.transform.Translate(new Vector3(30, 0, 0 * Time.deltaTime));
        }

        if(Input.GetMouseButton(1))
        {
            Vector3 EulerAngle = transform.rotation.eulerAngles;

            EulerAngle.x -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
            EulerAngle.y += Input.GetAxis("Mouse X") * xSpeed * 0.02f;

            Quaternion rot = Quaternion.Euler(EulerAngle);
            transform.rotation = rot;
        }
    }

    private void OnPreRender()
    {
        //GL.wireframe = true;
    }

    private void OnPostRender()
    {
        //GL.wireframe = false;
    }
}