using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Water : MonoBehaviour {

    public Camera       displacementCamera;
    public Shader       mOceanShader;
    private Material    mOceanMat;
    private static bool mCreate = false;

    private float   texelLengthX2;
    private Vector3 mWaterBodyColor;
    private Vector3 mSkyColor;
    private Vector3 mSunDir;
    private Vector3 mSunColor;
    private Vector3 mBendParam;
    private float   mSkyBlend;
    private float   mShineness;

    private const int   FRESNEL_TEX_SIZE = 256;
    private Texture2D   mFresnelMap;
    public Cubemap      mReflectionMap;

    // 菲涅尔:https://zh.wikipedia.org/wiki/%E8%8F%B2%E6%B6%85%E8%80%B3%E6%96%B9%E7%A8%8B
    // R_s = \left[\frac{sin(\theta_t - \theta_i)}{sin(\theta_t + \theta_i)}\right]^2
    // R_p = \left[\frac{tan(\theta_t - \theta_i)}{tan(\theta_t + \theta_i)}\right]^2
    // R = \frac{R_s + R_p}{2}
    // 推导为入射角与折射率的形式:https://docs.microsoft.com/en-us/windows/desktop/direct3d9/d3dxfresnelterm
    float FresnelTerm(float cosIndicentAngle, float refractionIdx)
    {
        float c = cosIndicentAngle;
        float g = Mathf.Sqrt(cosIndicentAngle * cosIndicentAngle + refractionIdx * refractionIdx - 1);
        float g_minus_c = g - c;
        float g_add_c   = g + c;

        float result = 0.5f * g_minus_c * g_minus_c / (g_add_c * g_add_c) * ((c * g_add_c - 1) * (c * g_add_c - 1) / ((c * g_minus_c + 1) * (c * g_minus_c + 1)) + 1);

        return result;
    }

    void CreateFresnelMap()
    {
        uint[] buffer = new uint[FRESNEL_TEX_SIZE];
        for(int i = 0; i < FRESNEL_TEX_SIZE; i++)
        {
            float cos_a = i / (float)FRESNEL_TEX_SIZE;

            // water refraction index using 1.3
            uint frensel = (uint)(FresnelTerm(cos_a, 1.33f) * 255);

            uint sky_blend = (uint)(Mathf.Pow(1 / (1 + cos_a), mSkyBlend) * 255);

            buffer[i] = (sky_blend << 8) | frensel;
        }

        mFresnelMap            = new Texture2D(FRESNEL_TEX_SIZE, 1, TextureFormat.ARGB32, false);
        mFresnelMap.filterMode = FilterMode.Bilinear;
        mFresnelMap.wrapMode   = TextureWrapMode.Clamp;
        mFresnelMap.name       = "FresnelMap";
    }

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
        CreateFresnelMap();

        mWaterBodyColor = new Vector3(0.07f, 0.15f, 0.2f);
        mSkyColor       = new Vector3(0.38f, 0.45f, 0.56f);
        mSunDir         = new Vector3(0.936016f, 0.0780013f, -0.343206f);
        mSunColor       = new Vector3(1.0f, 1.0f, 0.0f);
        mBendParam      = new Vector3(0.1f, -0.4f, 0.2f);
        mSkyBlend       = 16;
        mShineness      = 400.0f;
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
        mOceanMat.SetTexture("displacementMap", oceanSim.GetDisplacementMap());
        mOceanMat.SetTexture("NormalMap", oceanSim.GetNormalMap());
        mOceanMat.SetTexture("FresnelMap", mFresnelMap);
        mOceanMat.SetTexture("reflectCube", mReflectionMap);
        mOceanMat.SetFloat("texelLengthX2", texelLengthX2);
        mOceanMat.SetVector("WaterBodyColor", new Vector4(mWaterBodyColor.x, mWaterBodyColor.y, mWaterBodyColor.z, 0.0f));
        mOceanMat.SetVector("skyColor", new Vector4(mSkyColor.x, mSkyColor.y, mSkyColor.z, 0.0f));
        mOceanMat.SetVector("sunDir", new Vector4(mSunDir.x, mSunDir.y, mSunDir.z, 0.0f));
        mOceanMat.SetVector("sunColor", new Vector4(mSunColor.x, mSunColor.y, mSunColor.z, 0.0f));
        mOceanMat.SetVector("bendParam", new Vector4(mBendParam.x, mBendParam.y, mBendParam.z, 0.0f));
        mOceanMat.SetFloat("shineness", mShineness);
    }

    private void OnDestroy()
    {
        Destroy(gameObject.GetComponent<MeshFilter>());
        Destroy(gameObject.GetComponent<MeshRenderer>());
    }
}
