using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        Mesh mesh     = new Mesh();
        mesh.vertices = vertices;
        mesh.uv       = texcoords;

        List<int> indices = new List<int>();

        int offset = 0, submesh = 0;
        for(int z = 0; z < resolutionZ; z++)
        {
            for(int x = 0; x < resolutionX; x++)
            {
                int baseVertIdx = x + z * vertNumX;

                if(baseVertIdx - offset + vertNumX + 1 >= (1 << 16) - 1)
                {
                    mesh.subMeshCount = submesh + 1;
                    mesh.SetTriangles(indices, submesh++, false, offset);
                    offset = baseVertIdx;
                    indices.Clear();
                }

                indices.Add(baseVertIdx - offset);
                indices.Add(baseVertIdx - offset + vertNumX);
                indices.Add(baseVertIdx - offset + 1);

                indices.Add(baseVertIdx - offset + vertNumX);
                indices.Add(baseVertIdx - offset + vertNumX + 1);
                indices.Add(baseVertIdx - offset + 1 );
            }
        }

        if (indices.Count > 0)
        {
            mesh.subMeshCount = submesh + 1;
            mesh.SetTriangles(indices, submesh, false, offset);
        }

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
            meshFilter = gameObject.AddComponent<MeshFilter>();

            Vector4 resolutionAndLength = displacementCamera.GetComponent<OceanSimulation>().GetResolutionAndLength();
            Mesh grid = CreateUniformGrid((int)resolutionAndLength.x, (int)resolutionAndLength.y, (int)resolutionAndLength.z, (int)resolutionAndLength.w);
            meshFilter.mesh = grid;

            mOceanMat = new Material(mOceanShader);
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null) Destroy(renderer);
            renderer = gameObject.AddComponent<MeshRenderer>();
            Material[] materials = new Material[grid.subMeshCount];
            for (int i = 0; i < grid.subMeshCount; i++) materials[i] = mOceanMat;
            renderer.sharedMaterials = materials;

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
