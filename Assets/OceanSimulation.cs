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
};

struct FFTParameter
{
    public int thread_count;
    public int istride;
    public int ostride;
    public int pstride;
    public float phase_base;
};

[ExecuteInEditMode]
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
    private ComputeBuffer TempBuffer;

    private RenderTexture DebugTexture;

    public ComputeShader Buf2TexShader;
    public ComputeShader UpdateSpectrumShader;
    public ComputeShader FFTShader;

    private FFTParameter[] fftParameter;

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
        float phillips = amplitude * Mathf.Exp(-1.0f / (Ksqr * l * l)) / (Ksqr * Ksqr) * (Kcos * Kcos);

        // 与风向大于90度的波,减弱
        if (Kcos < 0.0f) phillips *= dir_depend;

        return phillips * Mathf.Exp(-Ksqr * w * w);
    }

    void InitH0AndDispersionRelation(OceanParameter parameter, ref Vector2[] h0, ref float[] omega, ref Vector2[] phillip, ref Vector2[] Gauss_Data)
    {
        int size = parameter.displaceMap_dimension * parameter.displaceMap_dimension;
        phillip    = new Vector2[size];
        h0         = new Vector2[size];
        omega      = new float[size];
        Gauss_Data = new Vector2[size];

        Vector2 windDir = parameter.wind_dir.normalized;

        Vector2 K;
        for(int i = 0; i < parameter.displaceMap_dimension; i++)
        {
            K.y = (-parameter.displaceMap_dimension / 2.0f + i) * (2 * Mathf.PI / parameter.patch_size);

            for(int j = 0; j < parameter.displaceMap_dimension; j++)
            {
                K.x = (-parameter.displaceMap_dimension / 2.0f + j) * (2 * Mathf.PI / parameter.patch_size);

                float phillips = (K.x == 0 && K.y == 0) ? 0 : Mathf.Sqrt(Phillips(K, windDir, parameter.wind_speed, parameter.wave_amplitude * 1e-1f, parameter.wind_dependency));

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
        parameter.displaceMap_dimension = 512;
        parameter.patch_size            = 2000.0f;
        parameter.time_scale            = 0.8f;
        parameter.wave_amplitude        = 0.35f;
        parameter.wind_dir              = new Vector2(0.8f, 0.6f);
        parameter.wind_speed            = 600.0f;
        parameter.wind_dependency       = 0.07f;
    }

    void UpdateDisplacementMap(float t, OceanParameter parameter)
    {
        // H(0) -> H(t), D(x,t), D(y,t)
        UpdateSpectrumShader.SetBuffer(OceanConst.KERNEL_UPDATE_SPECTRUM, OceanConst.SHADER_H0, h0Buffer);
        UpdateSpectrumShader.SetBuffer(OceanConst.KERNEL_UPDATE_SPECTRUM, OceanConst.SHADER_OMEGA, omegaBuffer);
        UpdateSpectrumShader.SetBuffer(OceanConst.KERNEL_UPDATE_SPECTRUM, OceanConst.SHADER_HK, HKBuffer);
        UpdateSpectrumShader.SetBuffer(OceanConst.KERNEL_UPDATE_SPECTRUM, OceanConst.SHADER_DX, DxBuffer);
        UpdateSpectrumShader.SetBuffer(OceanConst.KERNEL_UPDATE_SPECTRUM, OceanConst.SHADER_DY, DyBuffer);
        UpdateSpectrumShader.SetInt(OceanConst.SHADER_DIMENSION, parameter.displaceMap_dimension);
        UpdateSpectrumShader.SetFloat(OceanConst.SHADER_CURRENT_TIME, Time.time);
        int GroupNum = parameter.displaceMap_dimension / OceanConst.THREAD_GROUP;
        UpdateSpectrumShader.Dispatch(OceanConst.KERNEL_UPDATE_SPECTRUM, GroupNum, GroupNum, 1);

        FFTShader.SetInt(OceanConst.FFT_SHADER_THREAD_COUNT, fftParameter[0].thread_count);
        FFTShader.SetInt(OceanConst.FFT_SHADER_ISTRIDE, fftParameter[0].istride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_OSTRIDE, fftParameter[0].ostride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_PSTRIDE, fftParameter[0].pstride);
        FFTShader.SetFloat(OceanConst.FFT_SHADER_PHASE_BASE, fftParameter[0].phase_base);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_SRC, HKBuffer);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_DST, TempBuffer);
        FFTShader.Dispatch(0, fftParameter[0].thread_count / 128, 1, 1);

        FFTShader.SetInt(OceanConst.FFT_SHADER_THREAD_COUNT, fftParameter[1].thread_count);
        FFTShader.SetInt(OceanConst.FFT_SHADER_ISTRIDE, fftParameter[1].istride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_OSTRIDE, fftParameter[1].ostride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_PSTRIDE, fftParameter[1].pstride);
        FFTShader.SetFloat(OceanConst.FFT_SHADER_PHASE_BASE, fftParameter[1].phase_base);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_SRC, TempBuffer);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_DST, HtBuffer);
        FFTShader.Dispatch(0, fftParameter[0].thread_count / 128, 1, 1);

        FFTShader.SetInt(OceanConst.FFT_SHADER_THREAD_COUNT, fftParameter[2].thread_count);
        FFTShader.SetInt(OceanConst.FFT_SHADER_ISTRIDE, fftParameter[2].istride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_OSTRIDE, fftParameter[2].ostride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_PSTRIDE, fftParameter[2].pstride);
        FFTShader.SetFloat(OceanConst.FFT_SHADER_PHASE_BASE, fftParameter[2].phase_base);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_SRC, HtBuffer);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_DST, TempBuffer);
        FFTShader.Dispatch(0, fftParameter[0].thread_count / 128, 1, 1);

        FFTShader.SetInt(OceanConst.FFT_SHADER_THREAD_COUNT, fftParameter[3].thread_count);
        FFTShader.SetInt(OceanConst.FFT_SHADER_ISTRIDE, fftParameter[3].istride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_OSTRIDE, fftParameter[3].ostride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_PSTRIDE, fftParameter[3].pstride);
        FFTShader.SetFloat(OceanConst.FFT_SHADER_PHASE_BASE, fftParameter[3].phase_base);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_SRC, TempBuffer);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_DST, HtBuffer);
        FFTShader.Dispatch(0, fftParameter[0].thread_count / 128, 1, 1);

        FFTShader.SetInt(OceanConst.FFT_SHADER_THREAD_COUNT, fftParameter[4].thread_count);
        FFTShader.SetInt(OceanConst.FFT_SHADER_ISTRIDE, fftParameter[4].istride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_OSTRIDE, fftParameter[4].ostride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_PSTRIDE, fftParameter[4].pstride);
        FFTShader.SetFloat(OceanConst.FFT_SHADER_PHASE_BASE, fftParameter[4].phase_base);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_SRC, HtBuffer);
        FFTShader.SetBuffer(0, OceanConst.FFT_SHADER_DST, TempBuffer);
        FFTShader.Dispatch(0, fftParameter[0].thread_count / 128, 1, 1);

        FFTShader.SetInt(OceanConst.FFT_SHADER_THREAD_COUNT, fftParameter[5].thread_count);
        FFTShader.SetInt(OceanConst.FFT_SHADER_ISTRIDE, fftParameter[5].istride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_OSTRIDE, fftParameter[5].ostride);
        FFTShader.SetInt(OceanConst.FFT_SHADER_PSTRIDE, fftParameter[5].pstride);
        FFTShader.SetFloat(OceanConst.FFT_SHADER_PHASE_BASE, fftParameter[5].phase_base);
        FFTShader.SetBuffer(1, OceanConst.FFT_SHADER_SRC, TempBuffer);
        FFTShader.SetBuffer(1, OceanConst.FFT_SHADER_DST, HtBuffer);
        FFTShader.Dispatch(1, fftParameter[0].thread_count / 128, 1, 1);
    }

    void CreateBufferAndTexture(int dimension, System.Array data, ref ComputeBuffer buffer)
    {
        buffer = new ComputeBuffer(dimension * dimension, Marshal.SizeOf(data.GetValue(0)));
        buffer.SetData(data);
    }

    void DFT(float[] _h, int N, ref Vector2[] Result)
    {
        Result = new Vector2[N * N];

        for(int z = 0; z < N; z++)
        {
            for(int x = 0; x < N; x++)
            {
                Vector2 result = Vector2.zero;

                for (int m = 0; m < N; m++)
                {
                    Vector2 sum = Vector2.zero;

                    for (int n = 0; n < N; n++)
                    {
                        sum.x += _h[m * N + n] * Mathf.Cos(2 * Mathf.PI * x * n / N);
                        sum.y += _h[m * N + n] * Mathf.Sin(2 * Mathf.PI * x * n / N);
                    }

                    result.x += sum.x * Mathf.Cos(2 * Mathf.PI * m * z / N) - sum.y * Mathf.Sin(2 * Mathf.PI * m * z / N);
                    result.y += sum.y * Mathf.Cos(2 * Mathf.PI * m * z / N) + sum.x * Mathf.Sin(2 * Mathf.PI * m * z / N);
                }

                Result[z * N + x] = result;
            }
        }
    }

    void DFT_Row(Vector2[] _h, int N, ref Vector2[] Result)
    {
        for(int x = 0; x < N; x++)
        {
            for(int z = 0; z < N; z++)
            {
                Vector2 result = Vector2.zero;
                for(int n = 0; n < N; n++)
                {
                    float cosf = Mathf.Cos(2 * Mathf.PI * x * n / N);
                    float sinf = Mathf.Sin(2 * Mathf.PI * x * n / N);
                    result.x += _h[n * N + z].x * cosf - _h[n * N + z].y * sinf;
                    result.y += _h[n * N + z].x * sinf + _h[n * N + z].y * cosf;
                }

                Result[x * N + z].x = result.x;
                Result[x * N + z].y = result.y;
            }
        }
    }

    int bitReverse(int i, int N)
    {
        int dst = 0;
        while((N >> 1) != 0)
        {
            dst = (dst << 1) + (i & 1);
            i >>= 1;
            N >>= 1;
        }
        return dst;
    }

    void FFT_CPU(float[] _h, int N, ref Vector2[] FFT_Result)
    {
        FFT_Result = new Vector2[N * N];

        for(int i = 0; i < N * N; i++)
        {
            FFT_Result[i].x = _h[i];
            FFT_Result[i].y = 0.0f;
        }

        int count = N * N / 2;

        // 列FFT
        int row_iteration_num = (int)(Mathf.Log(N) / Mathf.Log(2));

        for(int i = 0; i < row_iteration_num; i++)
        {
            int istride = N * N / (2 << i);

            for(int k = 0; k < count; k++)
            {
                int mod = k & (istride - 1);
                // 如果为8FFT，这里应该为<<3
                int addr = ((k - mod) << 1) + mod;

                // fetch complex number
                Vector2 t = FFT_Result[addr];
                Vector2 u = FFT_Result[addr + istride];

                // 该判定有问题   
                int w = (addr - mod) / (istride << 1);
                w = bitReverse(w, 1 << i);

                float buttfly_cos = Mathf.Cos(2 * Mathf.PI * w / (2 << i));
                float buttfly_sin = Mathf.Sin(2 * Mathf.PI * w / (2 << i));

                FFT_Result[addr].x = t.x + u.x * buttfly_cos - u.y * buttfly_sin;
                FFT_Result[addr].y = t.y + u.x * buttfly_sin + u.y * buttfly_cos;

                FFT_Result[addr + istride].x = t.x - u.x * buttfly_cos + u.y * buttfly_sin;
                FFT_Result[addr + istride].y = t.y - u.x * buttfly_sin - u.y * buttfly_cos;
            }
        }

        Vector2[] temp = new Vector2[N * N];
        for(int i = 0; i < N; i++)
        {
            for(int j = 0; j < N; j++)
            {
                temp[i * N + j] = FFT_Result[bitReverse(i, N) * N + j];
            }
        }

        // 转置
        for(int i = 0; i < N; i++)
        {
            for(int j = 0; j < N; j++)
            {
                FFT_Result[i * N + j] = temp[j * N + i];
            }
        }

        // 列行列式
        for (int i = 0; i < row_iteration_num; i++)
        {
            int istride = N * N / (2 << i);

            for (int k = 0; k < count; k++)
            {
                int mod = k & (istride - 1);
                // 如果为8FFT，这里应该为<<3
                int addr = ((k - mod) << 1) + mod;

                // fetch complex number
                Vector2 t = FFT_Result[addr];
                Vector2 u = FFT_Result[addr + istride];

                int w = (addr - mod) / (istride << 1);
                w = bitReverse(w, 1 << i);

                float buttfly_cos = Mathf.Cos(2 * Mathf.PI * w / (2 << i));
                float buttfly_sin = Mathf.Sin(2 * Mathf.PI * w / (2 << i));

                FFT_Result[addr].x = t.x + u.x * buttfly_cos - u.y * buttfly_sin;
                FFT_Result[addr].y = t.y + u.x * buttfly_sin + u.y * buttfly_cos;

                FFT_Result[addr + istride].x = t.x - u.x * buttfly_cos + u.y * buttfly_sin;
                FFT_Result[addr + istride].y = t.y - u.x * buttfly_sin - u.y * buttfly_cos;
            }
        }

        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                temp[i * N + j] = FFT_Result[bitReverse(i, N) * N + j];
            }
        }

        // 转置
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                FFT_Result[i * N + j] = temp[j * N + i];
            }
        }
    }

    // Use this for initialization
    void Start()
    {
        int N = 8;
        float[] _h = new float[N * N];
        for(int i = 0; i < N; i++)
        {
            for(int j = 0; j < N; j++)
            {
                _h[i * N + j] = Random.Range(0, 16);
            }
        }
        Vector2[] DFT_Result = null;
        DFT(_h, N, ref DFT_Result);

        Vector2[] FFT_Result = null;
        FFT_CPU(_h, N, ref FFT_Result);

        InitOceanParameter();

        bool same = true;
        for (int i = 0; i < N; i++)
        {
            for(int j = 0; j < N; j++)
            {
                float dft_x = DFT_Result[i * N + j].x;
                float dft_y = DFT_Result[i * N + j].y;

                float fft_x = FFT_Result[i * N + j].x;
                float fft_y = FFT_Result[i * N + j].y;

                if (!DFT_Result[i * N + j].Equals(FFT_Result[i * N + j]))
                {
                    same = false;
                    break;
                }
            }
        }
        Debug.Log("FFT result is " + (same ? "same" : "different") + "with DFT result");

        int size = parameter.displaceMap_dimension * parameter.displaceMap_dimension;

        Vector2[] phillips_Data = null;
        Vector2[] H0_Data       = null;
        float[] Omega_Data      = null;
        Vector2[] Gauss_Data    = null;
        InitH0AndDispersionRelation(parameter, ref H0_Data, ref Omega_Data, ref phillips_Data, ref Gauss_Data);

        if (HKBuffer == null) HKBuffer = new ComputeBuffer(size, sizeof(float) * 2);
        if (DxBuffer == null) DxBuffer = new ComputeBuffer(size, sizeof(float) * 2);
        if (DyBuffer == null) DyBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        PhillipBuffer = new ComputeBuffer(size, Marshal.SizeOf(phillips_Data.GetValue(0)));
        PhillipBuffer.SetData(phillips_Data);

        h0Buffer = new ComputeBuffer(size, Marshal.SizeOf(H0_Data.GetValue(0)));
        h0Buffer.SetData(H0_Data);

        omegaBuffer = new ComputeBuffer(size, Marshal.SizeOf(Omega_Data.GetValue(0)));
        omegaBuffer.SetData(Omega_Data);

        GaussBuffer = new ComputeBuffer(size, Marshal.SizeOf(Gauss_Data.GetValue(0)));
        GaussBuffer.SetData(Gauss_Data);

        DebugTexture                   = new RenderTexture(parameter.displaceMap_dimension, parameter.displaceMap_dimension, 0, RenderTextureFormat.ARGBFloat);
        DebugTexture.filterMode        = FilterMode.Bilinear;
        DebugTexture.wrapMode          = TextureWrapMode.Clamp;
        DebugTexture.enableRandomWrite = true;
        DebugTexture.Create();

        fftParameter = new FFTParameter[6];
        fftParameter[0].thread_count = 512 * 512 / 8;
        fftParameter[0].ostride      = 512 * 512 / 8;
        fftParameter[0].istride      = 512 * 512 / 8;
        fftParameter[0].phase_base   = -Mathf.PI * 2.0f / (512.0f * 512.0f);
        fftParameter[0].pstride      = 512;

        fftParameter[1].thread_count = fftParameter[0].thread_count;
        fftParameter[1].ostride      = fftParameter[0].ostride;
        fftParameter[1].istride      = fftParameter[0].istride / 8;
        fftParameter[1].phase_base   = fftParameter[0].phase_base * 8;
        fftParameter[1].pstride      = 512;

        fftParameter[2].thread_count = fftParameter[1].thread_count;
        fftParameter[2].ostride      = fftParameter[1].ostride;
        fftParameter[2].istride      = fftParameter[1].istride / 8;
        fftParameter[2].phase_base   = fftParameter[1].phase_base * 8;
        fftParameter[2].pstride      = 512;

        fftParameter[3].thread_count = fftParameter[2].thread_count;
        fftParameter[3].ostride      = fftParameter[2].ostride / 512;
        fftParameter[3].istride      = fftParameter[2].istride / 8;
        fftParameter[3].phase_base   = fftParameter[2].phase_base * 8;
        fftParameter[3].pstride      = 1;

        fftParameter[4].thread_count = fftParameter[3].thread_count;
        fftParameter[4].ostride      = fftParameter[3].ostride;
        fftParameter[4].istride      = fftParameter[3].istride / 8;
        fftParameter[4].phase_base   = fftParameter[3].phase_base * 8;
        fftParameter[4].pstride      = 1;

        fftParameter[5].thread_count = fftParameter[4].thread_count;
        fftParameter[5].ostride      = fftParameter[4].ostride;
        fftParameter[5].istride      = fftParameter[4].istride / 8;
        fftParameter[5].phase_base   = fftParameter[4].phase_base * 8;
        fftParameter[5].pstride      = 1;

        if (HtBuffer == null)   HtBuffer = new ComputeBuffer(size, sizeof(float) * 2);
        if (TempBuffer == null) TempBuffer = new ComputeBuffer(size, sizeof(float) * 2);

        UpdateDisplacementMap(0, parameter);
    }

    void VisibleBuffer(ref ComputeBuffer buffer, ref RenderTexture tex, RenderTexture destination)
    {
        Buf2TexShader.SetBuffer(OceanConst.BUF2TEX_KERNEL, OceanConst.BUF2TEX_INPUT_BUFFER, buffer);
        Buf2TexShader.SetTexture(OceanConst.BUF2TEX_KERNEL, OceanConst.BUF2TEX_OUTPUT_TEXTURE, tex);
        Buf2TexShader.SetInt(OceanConst.BUF2TEX_DIMENSION, parameter.displaceMap_dimension);
        tex.DiscardContents();
        Buf2TexShader.Dispatch(OceanConst.BUF2TEX_KERNEL, parameter.displaceMap_dimension / OceanConst.BUF2TEX_THREAD_NUM, parameter.displaceMap_dimension / OceanConst.BUF2TEX_THREAD_NUM, 1);

        Graphics.Blit(tex, destination);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        VisibleBuffer(ref HtBuffer, ref DebugTexture, destination);
    }

    
    // Update is called once per frame
    void Update()
    {
        UpdateDisplacementMap(Time.time, parameter);
    }

    void OnDestroy()
    {
        if (PhillipBuffer != null) PhillipBuffer.Release();
        if (omegaBuffer   != null) omegaBuffer.Release();
        if (h0Buffer      != null) h0Buffer.Release();
        if (HKBuffer      != null) HKBuffer.Release();
        if (DxBuffer      != null) DxBuffer.Release();
        if (DyBuffer      != null) DyBuffer.Release();

        if (DebugTexture != null) DebugTexture.Release();
    }
}
