using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OceanSystem;

public class FFT {
    private ComputeBuffer mTempBuffer       = null;
    private ComputeBuffer mBitReverseBuffer = null;
    private ComputeShader mRadix2FFT        = null;
    public int mSize = 0;

    private int bitReverse(int i, int N)
    {
        int dst = 0;
        while ((N >> 1) != 0)
        {
            dst = (dst << 1) + (i & 1);
            i >>= 1;
            N >>= 1;
        }
        return dst;
    }

    public FFT(ComputeShader fft, int _size)
    {
        mSize      = _size;
        mRadix2FFT = fft;

        mBitReverseBuffer  = new ComputeBuffer(mSize, sizeof(int));
        int[] bit_reverse = new int[mSize];
        for (int i = 0; i < mSize; i++)
        {
            bit_reverse[i] = bitReverse(i, mSize);
        }
        mBitReverseBuffer.SetData(bit_reverse);

        mTempBuffer = new ComputeBuffer(mSize * mSize, sizeof(float) * 2);
    }

    private void SwapBuffer(ref ComputeBuffer A, ref ComputeBuffer B)
    {
        ComputeBuffer temp = A;
        A = B;
        B = temp;
    }

    public void EvaluteFFT(ComputeBuffer srcBuffer, ref ComputeBuffer dstBuffer)
    {
        if (mTempBuffer == null || mBitReverseBuffer == null || dstBuffer == null || mRadix2FFT == null) return;

        ComputeBuffer[] swapBuffer = new ComputeBuffer[2];
        swapBuffer[0] = dstBuffer;
        swapBuffer[1] = mTempBuffer;

        int interation = (int)(Mathf.Log(mSize) / Mathf.Log(2));
        int thread_count = mSize * mSize / 2;
        int thread_group = thread_count / OceanConst.RADIX2FFT_THREAD_NUM;
        for (int i = 0; i < interation; i++)
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count / (1 << i));
            mRadix2FFT.SetInt("bit_count", i);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(0, "bit_reverse", mBitReverseBuffer);
            mRadix2FFT.SetBuffer(0, "input", i == 0 ? srcBuffer : swapBuffer[0]);
            mRadix2FFT.SetBuffer(0, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(0, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // bit reverse
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(3, "bit_reverse", mBitReverseBuffer);
            mRadix2FFT.SetBuffer(3, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(3, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(3, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // transpose
        {
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(1, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(1, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(1, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        for (int i = 0; i < interation; i++)
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count / (1 << i));
            mRadix2FFT.SetInt("bit_count", i);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(0, "bit_reverse", mBitReverseBuffer);

            mRadix2FFT.SetBuffer(0, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(0, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(0, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // bit reverse
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(3, "bit_reverse", mBitReverseBuffer);
            mRadix2FFT.SetBuffer(3, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(3, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(3, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // transpose
        {
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(1, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(1, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(1, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        if (dstBuffer != swapBuffer[0])
        {
            mRadix2FFT.SetInt("istride", thread_count);
            mRadix2FFT.SetBuffer(2, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(2, "output", dstBuffer);
            mRadix2FFT.Dispatch(2, thread_group, 1, 1);
        }
    }

    public void EvaluteFFTCPU(ComputeBuffer srcBuffer, ref ComputeBuffer dstBuffer)
    {
        if (srcBuffer == null || dstBuffer == null) return;

        Vector2[] srcData = new Vector2[mSize * mSize];
        srcBuffer.GetData(srcData);

        Vector2[] Result = new Vector2[mSize * mSize];
        Vector2[] cache  = new Vector2[mSize * mSize];

        for(int m = 0; m < mSize; m++)
        {
            for(int n = 0; n < mSize; n++)
            {
                cache[m * n].x = Mathf.Cos(2 * Mathf.PI * m * n / mSize);
                cache[m * n].y = Mathf.Sin(2 * Mathf.PI * m * n / mSize);
            }
        }

        for (int z = 0; z < mSize; z++)
        {
            for(int x = 0; x < mSize; x++)
            {
                Vector2 result = Vector2.zero;

                for (int m = 0; m < mSize; m++)
                {
                    Vector2 sum = Vector2.zero;

                    for (int n = 0; n < mSize; n++)
                    {
                        int index = m * mSize + n;

                        float _cos = cache[x * n].x;//Mathf.Cos(2 * Mathf.PI * x * n / mSize);
                        float _sin = cache[x * n].y;// Mathf.Sin(2 * Mathf.PI * x * n / mSize);

                        sum.x += srcData[index].x * _cos - srcData[index].y * _sin;
                        sum.y += srcData[index].x * _sin + srcData[index].y * _cos;
                    }

                    float cos = cache[z * m].x;// Mathf.Cos(2 * Mathf.PI * m * z / mSize);
                    float sin = cache[z * m].y;// Mathf.Sin(2 * Mathf.PI * m * z / mSize);

                    result.x += sum.x * cos - sum.y * sin;
                    result.y += sum.y * cos + sum.x * sin;
                }

                Result[z * mSize + x] = result;
            }
        }

        dstBuffer.SetData(Result);
    }

    public void Cleanup()
    {
        if (mTempBuffer != null) mTempBuffer.Release();
        if (mBitReverseBuffer != null) mBitReverseBuffer.Release();
    }
}