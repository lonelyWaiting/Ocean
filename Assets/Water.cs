using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[ExecuteInEditMode]
public class Water : MonoBehaviour {

    public Camera displacementCamera;
    public Shader   mOceanShader;
    private Material mOceanMat;
    private static bool mCreate = false;

    private float texelLengthX2;

    Mesh CreateUniformGrid(int resolutionX, int resolutionZ, int width, int height)
    {
        int vertNumX = resolutionX + 1;
        int vertNumZ = resolutionZ + 1;

        float dx = (float)(width / resolutionX);
        float dz = (float)(height / resolutionZ);

        float du = 1.0f / resolutionX;
        float dv = 1.0f / resolutionZ;

        int areaSize = vertNumX * vertNumZ;

        Vector3[] vertices  = new Vector3[areaSize];
        Vector2[] texcoords = new Vector2[areaSize];

        for(int z = 0; z < vertNumZ; z++)
        {
            for(int x = 0; x < vertNumX; x++)
            {
                int index = z * vertNumX + x;
                vertices[index]  = new Vector3((x - resolutionX / 2) * dx, 0.0f, (z - resolutionZ / 2) * dz);
                texcoords[index] = new Vector2(x * du, z * dv);
            }
        }

        int[] indices = new int[resolutionX * resolutionZ * 6];
        for(int z = 0, index = 0; z < resolutionZ; z++)
        {
            for(int x = 0; x < resolutionX; x++)
            {
                indices[index++] = x + z * vertNumX;
                indices[index++] = x + (z + 1) * vertNumX;
                indices[index++] = (x + 1) + z * vertNumX;

                indices[index++] = x + (z + 1) * vertNumX;
                indices[index++] = (x + 1) + (z + 1) * vertNumX;
                indices[index++] = x + 1 + z * vertNumX;
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
        mCreate = false;
    }
	
	// Update is called once per frame
	void Update () {
        if(!mCreate)
        {
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null) Destroy(meshFilter);
            gameObject.AddComponent<MeshFilter>();

            Vector4 resolutionAndLength = displacementCamera.GetComponent<OceanSimulation>().GetResolutionAndLength();
            Mesh grid = CreateUniformGrid((int)resolutionAndLength.x, (int)resolutionAndLength.y, (int)resolutionAndLength.z, (int)resolutionAndLength.w);
            gameObject.GetComponent<MeshFilter>().mesh = grid;

            mOceanMat = new Material(mOceanShader);
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null) Destroy(renderer);
            gameObject.AddComponent<MeshRenderer>();
            gameObject.GetComponent<MeshRenderer>().material = mOceanMat;

            texelLengthX2 = resolutionAndLength.z / resolutionAndLength.x * 2;

            mCreate = true;
        }

        OceanSimulation oceanSim = displacementCamera.GetComponent<OceanSimulation>();
        mOceanMat.SetTexture("NormalMap", oceanSim.GetNormalMap());
        mOceanMat.SetTexture("displacementMap", oceanSim.GetDisplacementMap());
        mOceanMat.SetFloat("texelLengthX2", texelLengthX2);
    }

    private void OnDestroy()
    {
        Destroy(gameObject.GetComponent<MeshFilter>());
        Destroy(gameObject.GetComponent<MeshRenderer>());
    }
}
