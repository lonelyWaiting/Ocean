using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OceanSystem;
using System.Runtime.InteropServices;

struct OceanParameter
{
    // 2次幂
    public int displaceMap_dimension;
    // 海面块大小
    public float patch_size;
    public float time_scale;
    public float wave_amplitude;
    public Vector2 wind_dir;
    public float wind_speed;
    public float wind_dependency;
    public float choppyScale;
};

public class OceanSimulation : MonoBehaviour
{
    private const float GRAV_ACCEL  = 981.0f;
    private const float HALF_SQRT_2 = 0.7071068f;

    private OceanParameter parameter;

    private ComputeBuffer PhillipBuffer;
    private ComputeBuffer GaussBuffer;
    private ComputeBuffer h0Buffer;
    private ComputeBuffer omegaBuffer;
    private ComputeBuffer HKBuffer;
    private ComputeBuffer DxBuffer;
    private ComputeBuffer DyBuffer;
    private ComputeBuffer HtBuffer;
    private ComputeBuffer DxtBuffer;
    private ComputeBuffer DytBuffer;

    public ComputeShader Buf2TexShader;
    public ComputeShader UpdateSpectrumShader;
    public ComputeShader Radix2FFT;
    public Shader        mUpdateDisplacement;
    private Material     mUpdateDisplacementMat;
    private RenderTexture mDisplacementMap;

    public Shader           mGenGradientFold;
    private Material        mGenGradientFoldMat;
    private RenderTexture   mNormalMap;

    private FFT mFFT;
    private BufferVisualization mBufferVisual;

    public Vector4 GetResolutionAndLength()
    {
        return new Vector4(parameter.displaceMap_dimension, parameter.displaceMap_dimension, parameter.patch_size, parameter.patch_size);
    }

    int GetMapSize()
    {
        return parameter.displaceMap_dimension;
    }

    public RenderTexture GetNormalMap()
    {
        return mNormalMap;
    }

    public RenderTexture GetDisplacementMap()
    {
        return mDisplacementMap;
    }

    float Gauss()
    {
        float u1 = Random.Range(0.0f, 1.0f);
        float u2 = Random.Range(0.0f, 1.0f);

        if (u1 < 1e-6f) u1 = 1e-6f;

        return Mathf.Sqrt(-2 * Mathf.Log(u1)) * Mathf.Cos(2 * Mathf.PI * u2);
    }

    float Phillips(Vector2 K, Vector2 windDir, float windSpeed, float amplitude, float dir_depend)
    {
        // largest possible waves arising from a continuous wind of speed V
        float l = windSpeed * windSpeed / GRAV_ACCEL;

        float w = l / 1000;

        float Ksqr = K.x * K.x + K.y * K.y;
        float Kcos = K.x * windDir.x + K.y * windDir.y;
        float phillips = amplitude * Mathf.Exp(-1.0f / (Ksqr * l * l)) / (Ksqr * Ksqr * Ksqr) * (Kcos * Kcos);

        // 与风向大于90度的波,减弱
        if (Kcos < 0.0f) phillips *= dir_depend;

        return phillips * Mathf.Exp(-Ksqr * w * w);
    }

    void InitH0AndDispersionRelation(OceanParameter parameter, ref Vector2[] h0, ref float[] omega, ref Vector2[] phillip, ref Vector2[] Gauss_Data)
    {
        Vector2 windDir = parameter.wind_dir.normalized;

        Vector2 K = Vector2.zero;
        for(int i = 0; i < parameter.displaceMap_dimension; i++)
        {
            K.y = (-parameter.displaceMap_dimension / 2.0f + i) * (2 * Mathf.PI / parameter.patch_size);

            for(int j = 0; j < parameter.displaceMap_dimension; j++)
            {
                K.x = (-parameter.displaceMap_dimension / 2.0f + j) * (2 * Mathf.PI / parameter.patch_size);

                float phillips = (K.x == 0 && K.y == 0) ? 0 : Mathf.Sqrt(Phillips(K, windDir, parameter.wind_speed, parameter.wave_amplitude * 1e-7f, parameter.wind_dependency));

                int index = i * parameter.displaceMap_dimension + j;

                float Gauss_x = Gauss() , Gauss_y = Gauss();
                h0[index].x = phillips * Gauss_x * HALF_SQRT_2;
                h0[index].y = phillips * Gauss_y * HALF_SQRT_2;

                omega[index] = Mathf.Sqrt(GRAV_ACCEL * Mathf.Sqrt(K.x * K.x + K.y * K.y));

                phillip[index].x = phillips;
                phillip[index].y = phillips;

                Gauss_Data[index].x = Gauss_x;
                Gauss_Data[index].y = Gauss_y;
            }
        }
    }

    void InitOceanParameter()
    {
        parameter.displaceMap_dimension = 256;
        parameter.patch_size            = 2000.0f;
        parameter.time_scale            = 0.8f;
        parameter.wave_amplitude        = 0.35f;
        parameter.wind_dir              = new Vector2(0.8f, 0.6f);
        parameter.wind_speed            = 600.0f;
        parameter.wind_dependency       = 0.07f;
        parameter.choppyScale           = 1.3f;
    }

    void UpdateDisplacementMap(float t, OceanParameter parameter)
    {
        if (h0Buffer == null || omegaBuffer == null || HKBuffer == null || DxBuffer == null || DyBuffer == null) return;

        // H(0) -> H(t), D(x,t), D(y,t)
        UpdateSpectrumShader.SetBuffer(0, "H0", h0Buffer);
        UpdateSpectrumShader.SetBuffer(0, "Omega", omegaBuffer);
        UpdateSpectrumShader.SetBuffer(0, "HK", HKBuffer);
        UpdateSpectrumShader.SetBuffer(0, "Dx", DxBuffer);
        UpdateSpectrumShader.SetBuffer(0, "Dy", DyBuffer);
        UpdateSpectrumShader.SetInt("Dimension", parameter.displaceMap_dimension);
        UpdateSpectrumShader.SetFloat("curTime", t);
        int GroupNum = parameter.displaceMap_dimension / OceanConst.THREAD_GROUP;
        UpdateSpectrumShader.Dispatch(0, GroupNum, GroupNum, 1);

        mFFT.EvaluteFFT(HKBuffer, ref HtBuffer);
        mFFT.EvaluteFFT(DxBuffer, ref DxtBuffer);
        mFFT.EvaluteFFT(DyBuffer, ref DytBuffer);
    }

    // Use this for initialization
    void Start()
    {
        InitOceanParameter();

        int size = parameter.displaceMap_dimension * parameter.displaceMap_dimension;

        Vector2[] phillips_Data = new Vector2[size];
        Vector2[] H0_Data       = new Vector2[size];
        Vector2[] Gauss_Data    = new Vector2[size];
        float[]   Omega_Data    = new float[size];
        InitH0AndDispersionRelation(parameter, ref H0_Data, ref Omega_Data, ref phillips_Data, ref Gauss_Data);

        if (PhillipBuffer != null) PhillipBuffer.Release();
        PhillipBuffer = new ComputeBuffer(size, Marshal.SizeOf(phillips_Data.GetValue(0)));
        PhillipBuffer.SetData(phillips_Data);

        if (h0Buffer != null) h0Buffer.Release();
        h0Buffer = new ComputeBuffer(size, Marshal.SizeOf(H0_Data.GetValue(0)));
        h0Buffer.SetData(H0_Data);

        if (omegaBuffer != null) omegaBuffer.Release();
        omegaBuffer = new ComputeBuffer(size, Marshal.SizeOf(Omega_Data.GetValue(0)));
        omegaBuffer.SetData(Omega_Data);

        if (GaussBuffer != null) GaussBuffer.Release();
        GaussBuffer = new ComputeBuffer(size, Marshal.SizeOf(Gauss_Data.GetValue(0)));
        GaussBuffer.SetData(Gauss_Data);

        if (HKBuffer != null) HKBuffer.Release();
        HKBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        if (DxBuffer != null) DxBuffer.Release();
        DxBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        if (DyBuffer != null) DyBuffer.Release();
        DyBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        if (HtBuffer != null) HtBuffer.Release();
        HtBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        if (DxtBuffer != null) DxtBuffer.Release();
        DxtBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        if (DytBuffer != null) DytBuffer.Release();
        DytBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        mFFT          = new FFT(Radix2FFT, parameter.displaceMap_dimension);
        mBufferVisual = new BufferVisualization(Buf2TexShader, parameter.displaceMap_dimension);

        mUpdateDisplacementMat = new Material(mUpdateDisplacement);

        mDisplacementMap            = new RenderTexture(parameter.displaceMap_dimension, parameter.displaceMap_dimension, 0, RenderTextureFormat.ARGBFloat);
        mDisplacementMap.filterMode = FilterMode.Point;
        mDisplacementMap.wrapMode   = TextureWrapMode.Clamp;
        mDisplacementMap.Create();

        mGenGradientFoldMat = new Material(mGenGradientFold);

        mNormalMap            = new RenderTexture(parameter.displaceMap_dimension, parameter.displaceMap_dimension, 0, RenderTextureFormat.ARGBFloat);
        mNormalMap.filterMode = FilterMode.Bilinear;
        mNormalMap.wrapMode   = TextureWrapMode.Clamp;
        mNormalMap.Create();

        UpdateDisplacementMap(0, parameter);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateDisplacementMap(Time.time, parameter);

        if (!mUpdateDisplacementMat) return;

        RenderTexture oldRT = RenderTexture.active;

        RenderTexture.active = mDisplacementMap;
        mUpdateDisplacementMat.SetBuffer("InputHt", HtBuffer);
        mUpdateDisplacementMat.SetBuffer("InputDx", DxtBuffer);
        mUpdateDisplacementMat.SetBuffer("InputDy", DytBuffer);
        mUpdateDisplacementMat.SetInt("width", parameter.displaceMap_dimension);
        mUpdateDisplacementMat.SetInt("height", parameter.displaceMap_dimension);
        mUpdateDisplacementMat.SetFloat("choppyScale", parameter.choppyScale);
        mUpdateDisplacementMat.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, 3);

        RenderTexture.active = mNormalMap;
        mGenGradientFoldMat.SetTexture("_MainTex", mDisplacementMap);
        mGenGradientFoldMat.SetInt("width", parameter.displaceMap_dimension);
        mGenGradientFoldMat.SetFloat("choppyScale", parameter.choppyScale);
        mGenGradientFoldMat.SetFloat("GridLen", parameter.displaceMap_dimension / parameter.patch_size);
        mGenGradientFoldMat.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, 3);
    }

    void OnDestroy()
    {
        if (PhillipBuffer != null) PhillipBuffer.Release();
        if (omegaBuffer   != null) omegaBuffer.Release();
        if (h0Buffer      != null) h0Buffer.Release();
        if (HKBuffer      != null) HKBuffer.Release();
        if (DxBuffer      != null) DxBuffer.Release();
        if (DyBuffer      != null) DyBuffer.Release();
        if (GaussBuffer   != null) GaussBuffer.Release();
        if (HtBuffer      != null) HtBuffer.Release();
        if (DxtBuffer     != null) DxtBuffer.Release();
        if (DytBuffer     != null) DytBuffer.Release();

        if (mFFT != null) mFFT.Cleanup();
    }
}
