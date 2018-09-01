using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Water : MonoBehaviour {

    public Camera displacementCamera;
    private Material OceanMat;

    Mesh CreateUniformGrid(int resolutionX, int resolutionY, int width, int height)
    {
        int vertNumX = resolutionX + 1;
        int vertNumY = resolutionY + 1;

        float dx = width / resolutionX;
        float dy = height / resolutionY;

        float du = 1.0f / resolutionX;
        float dv = 1.0f / resolutionY;

        int areaSize = vertNumX * vertNumY;

        Vector3[] vertices  = new Vector3[areaSize];
        Vector2[] texcoords = new Vector2[areaSize];

        for(int y = 0; y < vertNumY; y++)
        {
            for(int x = 0; x < vertNumX; x++)
            {
                int index = y * vertNumX + x;
                vertices[index]  = new Vector3(x * dx, 0.0f, y * dy);
                texcoords[index] = new Vector2(x * du, y * dv);
            }
        }

        int[] indices = new int[resolutionX * resolutionY * 6];
        for(int y = 0, index = 0; y < resolutionY; y++)
        {
            for(int x = 0; x < resolutionX; x++)
            {
                indices[index++] = x + y * vertNumX;
                indices[index++] = x + (y + 1) * vertNumX;
                indices[index++] = (x + 1) + y * vertNumX;

                indices[index++] = x + (y + 1) * vertNumX;
                indices[index++] = (x + 1) + (y + 1) * vertNumX;
                indices[index++] = x + 1 + y * vertNumX;
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices  = vertices;
        mesh.uv        = texcoords;
        mesh.triangles = indices;

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
