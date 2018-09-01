using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Water : MonoBehaviour {

    public Camera displacementCamera;
    private Material OceanMat;

    Mesh CreateUniformGrid()
    {
        Mesh mesh = new Mesh();
        return mesh;
    }

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RenderTexture normalMap = displacementCamera.GetComponent<OceanSimulation>().GetNormalMap();
        RenderTexture displacementMap = displacementCamera.GetComponent<OceanSimulation>().GetDisplacementMap();
        Graphics.Blit(normalMap, destination);
    }
}
